# JarvisNet ESP32-CAM (motion → Telegram)

Firmware per **AI-Thinker ESP32-CAM**: confronto frame per rilevare movimento, invio JPEG a JarvisNet (`POST /api/camera/motion`), che inoltra la foto via bot Telegram.

## Alimentazione (importante)

La ESP32-CAM + sensore assorbe picchi **> 500 mA**. Molti adattatori **USB‑TTL (CP2102)** non reggono: vedi `Brownout detector was triggered` o `FB-SIZE: 0`.

- Usa un **alimentatore 5 V ≥ 2 A** sui pin **5V** e **GND** della ESP32-CAM.
- Tieni **GND in comune** con il CP2102 (nero), ma puoi **staccare il rosso 5V del CP2102** se alimenti solo dal brick 5V.
- Dopo il flash, **GPIO0 non deve restare collegato a GND**.

## Collegamenti CP2102 ↔ ESP32-CAM

Cavo **USB‑TTL CP2102** (4 fili + eventuale jumper) verso i pin della scheda **AI-Thinker** (header laterale):

| Filo CP2102 | Pin ESP32-CAM |
|-------------|---------------|
| 5V (rosso) | **5V** |
| GND (nero) | **GND** |
| TX (verde, «D») | **U0T** |
| RX (bianco, «R») | **U0R** |
| *(solo per flash)* | **GPIO0** → **GND** |

- **Alimentazione consigliata:** brick **5 V ≥ 2 A** su **5V/GND** della ESP32-CAM; **GND in comune** con il CP2102. Puoi **non collegare** il 5V rosso del CP2102 se alimenti solo dal brick.
- **GPIO0 → GND** serve **solo** in fase di upload (modalità bootloader). A runtime togli il ponticello: altrimenti la scheda non parte col firmware.

### Come fare l’upload (PlatformIO)

1. Collega **GND, U0T, U0R** (e 5V dal brick o dal CP2102, come sopra).
2. **Metti GPIO0 a GND** (jumper o filo: pin **GPIO0** sullo stesso header → **GND**).
3. **Reset** la ESP32-CAM (pulsante **RST** sulla scheda, oppure stacca/riattacca l’alimentazione).
4. Da PC, nella cartella del progetto:

```bash
pio run -t upload
```

5. Quando PlatformIO ha finito (**Success**), **scollega GPIO0 da GND**.
6. Premi **RST** (o power-cycle) e apri il monitor seriale:

```bash
pio device monitor
```

Se `upload` fallisce con timeout o «Failed to connect», controlla che GPIO0 sia ancora a GND al reset, che `upload_port` in `platformio.ini` sia la COM giusta e che TX/RX siano su **U0T/U0R** come in tabella.

## Prerequisiti

- [PlatformIO](https://platformio.org/) (CLI o estensione VS Code/Cursor)
- ESP32-CAM collegata al PC (es. **COM3** per flash e monitor seriale)
- **JarvisNet.UI** in esecuzione sulla LAN (URL in `secrets.h` = IP del PC, es. `http://192.168.1.10:5194`)
- Telegram configurato (`config/jarvisnet.telegram.secrets.json`) e `JarvisNet:Camera:Enabled=true`

Su Windows, avvia l’host così (firewall: consenti **5194** in ingresso, rete privata):

```powershell
dotnet run --project C:\Projects\JarvisNet\src\JarvisNet.UI\JarvisNet.UI.csproj --urls "http://0.0.0.0:5194"
```

## Configurazione

1. Copia `src/secrets.example.h` → `src/secrets.h` e imposta Wi‑Fi, URL Jarvis e `JARVIS_API_KEY` (stesso valore di `IngestApiKey` in `config/jarvisnet.camera.secrets.json`).
2. In `platformio.ini` verifica `upload_port` / `monitor_port` (es. `COM3`).
3. Su JarvisNet:
   - `config/jarvisnet.camera.json` → `Enabled: true`
   - `NotifyChatIds` oppure usa gli stessi `AllowedChatIds` di Telegram
   - Cooldown default 30 s tra un alert e l’altro

## Upload firmware

Vedi [Collegamenti CP2102](#collegamenti-cp2102--esp32-cam) per cablaggio e sequenza GPIO0. Dalla root del repo:

```bash
cd esp32/jarvisnet-esp32-cam
pio run -t upload
pio device monitor
```

## Note

- **COM3** serve solo per programmare e debug seriale; le foto viaggiano su **Wi‑Fi** verso il PC.
- Su Windows servono **`--urls "http://0.0.0.0:5194"`** (vedi Prerequisiti) e regola firewall in ingresso sulla porta **5194** (rete privata).
- Regola `MOTION_THRESHOLD` e `MOTION_PIXELS_MIN` in `main.cpp` se hai troppi falsi positivi.
