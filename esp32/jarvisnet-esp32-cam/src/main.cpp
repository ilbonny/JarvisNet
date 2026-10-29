#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include "esp_camera.h"
#include "soc/soc.h"
#include "soc/rtc_cntl_reg.h"
#if __has_include("secrets.h")
#include "secrets.h"
#else
#error "Crea src/secrets.h da secrets.example.h"
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
  config.frame_size = FRAMESIZE_QVGA;
  config.jpeg_quality = 15;
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

static uint8_t* prevJpeg = nullptr;
static size_t prevJpegLen = 0;
static unsigned long lastMotionMs = 0;
static bool wifiStarted = false;

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
  http.addHeader("X-Api-Key", JARVIS_API_KEY);
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
  WiFi.setSleep(true);
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

void setup()
{
  Serial.begin(115200);
  delay(1000);

  WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0);

  Serial.println();
  Serial.println("JarvisNet ESP32-CAM motion");
  Serial.printf("PSRAM: %s\n", psramFound() ? "yes" : "no");
  Serial.println("Power: use stable 5V >= 2A (USB-TTL alone is often too weak).");

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
    if (sendJpegToJarvis(fb->buf, fb->len))
    {
      lastMotionMs = now;
    }
  }

  esp_camera_fb_return(fb);
  delay(FRAME_INTERVAL_MS);
}
