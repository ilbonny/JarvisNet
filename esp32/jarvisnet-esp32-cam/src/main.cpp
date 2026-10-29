#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <WebServer.h>
#include <ArduinoOTA.h>
#include "esp_camera.h"
#include "soc/soc.h"
#include "soc/rtc_cntl_reg.h"
#if __has_include("secrets.h")
#include "secrets.h"
#else
#error "Crea src/secrets.h da secrets.example.h"
#endif

#ifndef OTA_HOSTNAME
#define OTA_HOSTNAME "esp32-cam"
#endif

#ifndef OTA_PASSWORD
#define OTA_PASSWORD ""
#endif

#ifndef MOTION_BYTE_DIFF_MIN
#define MOTION_BYTE_DIFF_MIN 1200
#endif

#ifndef FRAME_INTERVAL_MS
#define FRAME_INTERVAL_MS 300
#endif

#ifndef COOLDOWN_MS
#define COOLDOWN_MS 30000
#endif

#ifndef HEARTBEAT_INTERVAL_MS
#define HEARTBEAT_INTERVAL_MS 45000
#endif

#ifndef CONTROL_HTTP_PORT
#define CONTROL_HTTP_PORT 80
#endif

#ifndef CLIP_FPS
#define CLIP_FPS 2
#endif

#ifndef CAPTURE_FRAME_SIZE
#define CAPTURE_FRAME_SIZE FRAMESIZE_VGA
#endif

#ifndef CAPTURE_JPEG_QUALITY
#define CAPTURE_JPEG_QUALITY 12
#endif

#ifndef FLASH_LED_GPIO
#define FLASH_LED_GPIO 4
#endif

#ifndef FLASH_READY_BLINKS
#define FLASH_READY_BLINKS 3
#endif

#ifndef FLASH_WARMUP_MS
#define FLASH_WARMUP_MS 80
#endif

static WebServer controlServer(CONTROL_HTTP_PORT);

static void initFlashLed();
static void setFlashLed(bool on);
static void blinkReadySignal();
static camera_fb_t* captureJpegWithFlash();
static camera_config_t cameraConfigForEsp32Cam();
static void startWifiOnce();
static void startOtaOnce();
static void startControlServerOnce();
static bool sendHeartbeatToJarvis();
static bool sendJpegToJarvis(const uint8_t* data, size_t len);
static bool storePrevJpeg(const uint8_t* data, size_t len);
static void applyMotionFlagFromHeartbeatBody(const String& body);

static uint8_t* prevJpeg = nullptr;
static size_t prevJpegLen = 0;
static unsigned long lastMotionMs = 0;
static bool wifiStarted = false;
static bool otaStarted = false;
static bool controlServerStarted = false;
static unsigned long lastHeartbeatMs = 0;
static bool motionDetectionEnabled = false;

static String trimCopy(const String& value)
{
  String copy = value;
  copy.trim();
  return copy;
}

static String readProvidedApiKey()
{
  if (controlServer.hasHeader("X-Api-Key"))
  {
    return trimCopy(controlServer.header("X-Api-Key"));
  }

  if (controlServer.hasHeader("x-api-key"))
  {
    return trimCopy(controlServer.header("x-api-key"));
  }

  if (controlServer.hasArg("apiKey"))
  {
    return trimCopy(controlServer.arg("apiKey"));
  }

  return String();
}

static bool authorizeControlRequest()
{
  if (JARVIS_API_KEY[0] == '\0')
  {
    return true;
  }

  String provided = readProvidedApiKey();
  if (provided.length() == 0)
  {
    return false;
  }

  return provided.equals(trimCopy(String(JARVIS_API_KEY)));
}

static void sendUnauthorized()
{
  controlServer.send(401, "application/json", "{\"error\":\"unauthorized\"}");
}

static void handleCapture()
{
  Serial.println("HTTP /capture");
  if (!authorizeControlRequest())
  {
    Serial.println("HTTP /capture unauthorized");
    sendUnauthorized();
    return;
  }

  camera_fb_t* shot = captureJpegWithFlash();
  if (shot == nullptr || shot->len == 0)
  {
    controlServer.send(503, "application/json", "{\"error\":\"capture failed\"}");
    return;
  }

  controlServer.send_P(200, "image/jpeg", (const char*)shot->buf, shot->len);
  esp_camera_fb_return(shot);
}

static void writeClipU16(uint8_t* out, size_t& offset, uint16_t value)
{
  out[offset++] = (uint8_t)(value & 0xFF);
  out[offset++] = (uint8_t)((value >> 8) & 0xFF);
}

static void writeClipU32(uint8_t* out, size_t& offset, uint32_t value)
{
  out[offset++] = (uint8_t)(value & 0xFF);
  out[offset++] = (uint8_t)((value >> 8) & 0xFF);
  out[offset++] = (uint8_t)((value >> 16) & 0xFF);
  out[offset++] = (uint8_t)((value >> 24) & 0xFF);
}

static void handleClip()
{
  Serial.printf("HTTP /clip seconds=%s\n", controlServer.arg("seconds").c_str());
  if (!authorizeControlRequest())
  {
    Serial.println("HTTP /clip unauthorized");
    sendUnauthorized();
    return;
  }

  int seconds = controlServer.arg("seconds").toInt();
  if (seconds < 1)
  {
    seconds = 3;
  }
  if (seconds > 10)
  {
    seconds = 10;
  }

  const int fps = CLIP_FPS > 0 ? CLIP_FPS : 2;
  const int maxFrames = seconds * fps;
  const int frameDelayMs = 1000 / fps;

  size_t capacity = 64 * 1024 + (size_t)maxFrames * 32 * 1024;
  uint8_t* buffer = (uint8_t*)heap_caps_malloc(capacity, MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT);
  if (buffer == nullptr)
  {
    buffer = (uint8_t*)malloc(capacity);
  }
  if (buffer == nullptr)
  {
    controlServer.send(503, "application/json", "{\"error\":\"out of memory\"}");
    return;
  }

  size_t offset = 0;
  memcpy(buffer + offset, "JNETCLIP", 8);
  offset += 8;
  buffer[offset++] = 1;
  uint16_t frameCount = 0;
  size_t frameCountOffset = offset;
  offset += 2;

  for (int i = 0; i < maxFrames; i++)
  {
    camera_fb_t* fb = esp_camera_fb_get();
    if (fb == nullptr || fb->len == 0)
    {
      if (fb != nullptr)
      {
        esp_camera_fb_return(fb);
      }
      delay(frameDelayMs);
      continue;
    }

    if (offset + 4 + fb->len > capacity)
    {
      esp_camera_fb_return(fb);
      break;
    }

    writeClipU32(buffer, offset, (uint32_t)fb->len);
    memcpy(buffer + offset, fb->buf, fb->len);
    offset += fb->len;
    esp_camera_fb_return(fb);
    frameCount++;
    delay(frameDelayMs);
  }

  writeClipU16(buffer, frameCountOffset, frameCount);

  if (frameCount == 0)
  {
    free(buffer);
    controlServer.send(503, "application/json", "{\"error\":\"no frames\"}");
    return;
  }

  controlServer.send_P(200, "application/octet-stream", (const char*)buffer, offset);
  free(buffer);
}

static void handleMotionPut()
{
  if (!authorizeControlRequest())
  {
    sendUnauthorized();
    return;
  }

  if (controlServer.hasArg("plain"))
  {
    String body = controlServer.arg("plain");
    motionDetectionEnabled = body.indexOf("true") >= 0;
  }
  else if (controlServer.hasArg("enabled"))
  {
    motionDetectionEnabled = controlServer.arg("enabled") == "1" || controlServer.arg("enabled") == "true";
  }

  String response = String("{\"enabled\":") + (motionDetectionEnabled ? "true" : "false") + "}";
  controlServer.send(200, "application/json", response);
}

static void startControlServerOnce()
{
  if (controlServerStarted)
  {
    return;
  }

  controlServerStarted = true;
  controlServer.on("/capture", HTTP_GET, handleCapture);
  controlServer.on("/clip", HTTP_GET, handleClip);
  controlServer.on("/motion", HTTP_PUT, handleMotionPut);
  controlServer.begin();
  Serial.printf("Control HTTP on port %d (/capture, /clip, /motion)\n", CONTROL_HTTP_PORT);
}

static int countJpegByteDiffs(const uint8_t* a, size_t lenA, const uint8_t* b, size_t lenB)
{
  size_t n = lenA < lenB ? lenA : lenB;
  int changed = 0;
  for (size_t i = 0; i < n; i++)
  {
    if (abs((int)a[i] - (int)b[i]) > 8)
    {
      changed++;
      if (changed >= MOTION_BYTE_DIFF_MIN)
      {
        return changed;
      }
    }
  }
  return changed;
}

static void addDeviceHeaders(HTTPClient& http)
{
  http.addHeader("X-Api-Key", JARVIS_API_KEY);
  http.addHeader("X-Device-Name", OTA_HOSTNAME);
  http.addHeader("X-Device-Ip", WiFi.localIP().toString());
  http.addHeader("X-Device-Id", OTA_HOSTNAME);
}

static void applyMotionFlagFromHeartbeatBody(const String& body)
{
  if (body.indexOf("\"motionDetectionEnabled\":true") >= 0)
  {
    motionDetectionEnabled = true;
  }
  else if (body.indexOf("\"motionDetectionEnabled\":false") >= 0)
  {
    motionDetectionEnabled = false;
  }
}

static bool sendHeartbeatToJarvis()
{
  if (WiFi.status() != WL_CONNECTED)
  {
    return false;
  }

  HTTPClient http;
  String url = String(JARVIS_BASE_URL) + "/api/camera/heartbeat";
  if (!http.begin(url))
  {
    Serial.println("Heartbeat HTTP begin failed");
    return false;
  }

  http.addHeader("Content-Type", "application/json");
  addDeviceHeaders(http);
  http.setTimeout(8000);

  String ip = WiFi.localIP().toString();
  String body = String("{\"type\":\"camera\",\"name\":\"") + OTA_HOSTNAME + "\",\"ip\":\"" + ip + "\"}";
  int code = http.POST(body);
  String response = http.getString();
  http.end();
  Serial.printf("Jarvis heartbeat %d\n", code);
  if (code >= 200 && code < 300)
  {
    applyMotionFlagFromHeartbeatBody(response);
  }

  return code >= 200 && code < 300;
}

static bool sendJpegToJarvis(const uint8_t* data, size_t len)
{
  if (WiFi.status() != WL_CONNECTED || data == nullptr || len == 0)
  {
    return false;
  }

  HTTPClient http;
  String url = String(JARVIS_BASE_URL) + "/api/camera/motion";
  if (!http.begin(url))
  {
    Serial.println("HTTP begin failed");
    return false;
  }

  http.addHeader("Content-Type", "image/jpeg");
  addDeviceHeaders(http);
  http.setTimeout(15000);

  int code = http.POST(const_cast<uint8_t*>(data), len);
  Serial.printf("Jarvis POST %d (%u bytes)\n", code, (unsigned)len);
  http.end();
  return code >= 200 && code < 300;
}

static void startWifiOnce()
{
  if (wifiStarted)
  {
    return;
  }

  wifiStarted = true;
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);
  WiFi.setTxPower(WIFI_POWER_8_5dBm);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  Serial.print("WiFi");
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    Serial.print(".");
  }
  Serial.println();
  Serial.print("IP: ");
  Serial.println(WiFi.localIP());

  startOtaOnce();
  startControlServerOnce();
}

static void startOtaOnce()
{
  if (otaStarted)
  {
    return;
  }

  otaStarted = true;
  ArduinoOTA.setHostname(OTA_HOSTNAME);
  if (OTA_PASSWORD[0] != '\0')
  {
    ArduinoOTA.setPassword(OTA_PASSWORD);
  }

  ArduinoOTA.onStart([]() {
    Serial.println("OTA update start");
    setFlashLed(false);
  });
  ArduinoOTA.onEnd([]() { Serial.println("\nOTA update end"); });
  ArduinoOTA.onProgress([](unsigned int progress, unsigned int total) {
    Serial.printf("OTA progress: %u%%\r", total == 0 ? 0 : (progress * 100) / total);
  });
  ArduinoOTA.onError([](ota_error_t error) {
    Serial.printf("OTA error: %u\n", error);
  });

  ArduinoOTA.begin();
  Serial.printf("OTA ready (host %s). Use: pio run -e esp32cam_ota -t upload --upload-port %s\n",
                OTA_HOSTNAME,
                WiFi.localIP().toString().c_str());

  sendHeartbeatToJarvis();
  lastHeartbeatMs = millis();
}

static bool storePrevJpeg(const uint8_t* data, size_t len)
{
  uint8_t* copy = (uint8_t*)realloc(prevJpeg, len);
  if (copy == nullptr)
  {
    return false;
  }
  prevJpeg = copy;
  memcpy(prevJpeg, data, len);
  prevJpegLen = len;
  return true;
}

static void initFlashLed()
{
  pinMode(FLASH_LED_GPIO, OUTPUT);
  digitalWrite(FLASH_LED_GPIO, LOW);
}

static void setFlashLed(bool on)
{
  digitalWrite(FLASH_LED_GPIO, on ? HIGH : LOW);
}

static void blinkReadySignal()
{
  for (int i = 0; i < FLASH_READY_BLINKS; i++)
  {
    setFlashLed(true);
    delay(120);
    setFlashLed(false);
    delay(120);
  }
}

static camera_fb_t* captureJpegWithFlash()
{
  setFlashLed(true);
  delay(FLASH_WARMUP_MS);
  camera_fb_t* shot = esp_camera_fb_get();
  setFlashLed(false);
  return shot;
}

static camera_config_t cameraConfigForEsp32Cam()
{
  camera_config_t config{};
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer = LEDC_TIMER_0;
  config.pin_d0 = 5;
  config.pin_d1 = 18;
  config.pin_d2 = 19;
  config.pin_d3 = 21;
  config.pin_d4 = 36;
  config.pin_d5 = 39;
  config.pin_d6 = 34;
  config.pin_d7 = 35;
  config.pin_xclk = 0;
  config.pin_pclk = 22;
  config.pin_vsync = 25;
  config.pin_href = 23;
  config.pin_sccb_sda = 26;
  config.pin_sccb_scl = 27;
  config.pin_pwdn = 32;
  config.pin_reset = -1;
  config.xclk_freq_hz = 10000000;
  config.pixel_format = PIXFORMAT_JPEG;
  config.frame_size = CAPTURE_FRAME_SIZE;
  config.jpeg_quality = CAPTURE_JPEG_QUALITY;
  config.grab_mode = CAMERA_GRAB_LATEST;
  config.fb_count = 2;

  if (psramFound())
  {
    config.fb_location = CAMERA_FB_IN_PSRAM;
  }
  else
  {
    config.frame_size = FRAMESIZE_QQVGA;
    config.fb_location = CAMERA_FB_IN_DRAM;
    config.fb_count = 1;
  }

  return config;
}

void setup()
{
  Serial.begin(115200);
  delay(1000);

  WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0);

  Serial.println();
  Serial.println("JarvisNet ESP32-CAM (control + optional motion)");
  Serial.printf("PSRAM: %s\n", psramFound() ? "yes" : "no");
  Serial.println("Motion detection default: OFF (enable via Jarvis).");

  initFlashLed();

  camera_config_t config = cameraConfigForEsp32Cam();
  esp_err_t camErr = esp_camera_init(&config);
  if (camErr != ESP_OK)
  {
    Serial.printf("Camera init failed: 0x%x\n", camErr);
    while (true)
    {
      delay(1000);
    }
  }

  delay(300);

  for (int attempt = 0; attempt < 8; attempt++)
  {
    camera_fb_t* probe = esp_camera_fb_get();
    if (probe != nullptr && probe->len > 0)
    {
      Serial.printf("Camera OK, JPEG %ux%u (%u bytes)\n", probe->width, probe->height, probe->len);
      storePrevJpeg(probe->buf, probe->len);
      esp_camera_fb_return(probe);
      startWifiOnce();
      Serial.println("Ready — 3 flash LED");
      blinkReadySignal();
      return;
    }
    if (probe != nullptr)
    {
      esp_camera_fb_return(probe);
    }
    Serial.printf("Camera warmup attempt %d/8...\n", attempt + 1);
    delay(400);
  }

  Serial.println("Camera probe failed. Check: ribbon, GPIO0 not tied to GND, 5V supply.");
  startWifiOnce();
}

void loop()
{
  ArduinoOTA.handle();
  controlServer.handleClient();

  unsigned long nowLoop = millis();
  if (WiFi.status() == WL_CONNECTED
      && (nowLoop - lastHeartbeatMs) >= (unsigned long)HEARTBEAT_INTERVAL_MS)
  {
    sendHeartbeatToJarvis();
    lastHeartbeatMs = nowLoop;
  }

  if (!motionDetectionEnabled)
  {
    delay(FRAME_INTERVAL_MS);
    return;
  }

  camera_fb_t* fb = esp_camera_fb_get();
  if (fb == nullptr || fb->len == 0)
  {
    Serial.println("Empty JPEG frame");
    delay(500);
    return;
  }

  if (prevJpeg == nullptr || prevJpegLen == 0)
  {
    storePrevJpeg(fb->buf, fb->len);
    esp_camera_fb_return(fb);
    delay(FRAME_INTERVAL_MS);
    return;
  }

  int changed = countJpegByteDiffs(prevJpeg, prevJpegLen, fb->buf, fb->len);
  storePrevJpeg(fb->buf, fb->len);

  unsigned long now = millis();
  bool motion = changed >= MOTION_BYTE_DIFF_MIN && (now - lastMotionMs) >= (unsigned long)COOLDOWN_MS;

  if (motion)
  {
    Serial.printf("Motion (jpeg diff): %d\n", changed);
    esp_camera_fb_return(fb);
    fb = nullptr;

    camera_fb_t* alert = captureJpegWithFlash();
    if (alert != nullptr && alert->len > 0)
    {
      if (sendJpegToJarvis(alert->buf, alert->len))
      {
        lastMotionMs = now;
      }
      storePrevJpeg(alert->buf, alert->len);
    }
    else
    {
      Serial.println("Flash capture failed");
    }
    if (alert != nullptr)
    {
      esp_camera_fb_return(alert);
    }
    delay(FRAME_INTERVAL_MS);
    return;
  }

  esp_camera_fb_return(fb);
  delay(FRAME_INTERVAL_MS);
}
