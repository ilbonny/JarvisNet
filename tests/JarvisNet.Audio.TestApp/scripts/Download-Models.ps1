# Scarica modelli condivisi in <repo>/models (STT + TTS)
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$models = Join-Path $repoRoot "models"
$kroko = Join-Path $models "stt\it-kroko"
$tts = Join-Path $models "tts"

New-Item -ItemType Directory -Force -Path $kroko | Out-Null
New-Item -ItemType Directory -Force -Path $tts | Out-Null

function Get-HfFile {
    param([string]$Url, [string]$OutFile)
    if (Test-Path $OutFile) {
        Write-Host "OK (exists): $OutFile"
        return
    }
    Write-Host "Downloading: $OutFile"
    curl.exe -L --fail --retry 3 --retry-delay 5 -o $OutFile $Url
}

$hfKroko = "https://huggingface.co/kouhxp/sherpa-onnx-streaming-zipformer-it-kroko/resolve/main"
Get-HfFile "$hfKroko/encoder.onnx" (Join-Path $kroko "encoder.onnx")
Get-HfFile "$hfKroko/decoder.onnx" (Join-Path $kroko "decoder.onnx")
Get-HfFile "$hfKroko/joiner.onnx"  (Join-Path $kroko "joiner.onnx")
Get-HfFile "$hfKroko/tokens.txt"   (Join-Path $kroko "tokens.txt")

$hfPaola = "https://huggingface.co/rhasspy/piper-voices/resolve/main/it/it_IT/paola/medium"
Get-HfFile "$hfPaola/it_IT-paola-medium.onnx"      (Join-Path $tts "it_IT-paola-medium.onnx")
Get-HfFile "$hfPaola/it_IT-paola-medium.onnx.json" (Join-Path $tts "it_IT-paola-medium.onnx.json")

$hfRiccardo = "https://huggingface.co/rhasspy/piper-voices/resolve/main/it/it_IT/riccardo/x_low"
Get-HfFile "$hfRiccardo/it_IT-riccardo-x_low.onnx"      (Join-Path $tts "it_IT-riccardo-x_low.onnx")
Get-HfFile "$hfRiccardo/it_IT-riccardo-x_low.onnx.json" (Join-Path $tts "it_IT-riccardo-x_low.onnx.json")

Write-Host "Done. Modelli condivisi in: $models"
Write-Host "Voci TTS: it_IT-paola-medium (donna), it_IT-riccardo-x_low (uomo). Imposta JarvisNet:Audio:PiperVoiceKey in config."
