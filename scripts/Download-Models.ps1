# Scarica modelli condivisi in <repo>/models (STT + TTS)
param(
    [ValidateSet("it", "es", "en", "all")]
    [string]$Locale = "all"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$models = Join-Path $repoRoot "models"
$tts = Join-Path $models "tts"

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

function Get-SherpaKroko {
    param([string]$LangFolder, [string]$HfRepo)
    $target = Join-Path $models "stt\$LangFolder"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $base = "https://huggingface.co/$HfRepo/resolve/main"
    Get-HfFile "$base/encoder.onnx" (Join-Path $target "encoder.onnx")
    Get-HfFile "$base/decoder.onnx" (Join-Path $target "decoder.onnx")
    Get-HfFile "$base/joiner.onnx"  (Join-Path $target "joiner.onnx")
    Get-HfFile "$base/tokens.txt"   (Join-Path $target "tokens.txt")
}

function Get-PiperVoice {
    param([string]$HfPath, [string]$OnnxName)
    $base = "https://huggingface.co/rhasspy/piper-voices/resolve/main/$HfPath"
    Get-HfFile "$base/$OnnxName.onnx"      (Join-Path $tts "$OnnxName.onnx")
    Get-HfFile "$base/$OnnxName.onnx.json" (Join-Path $tts "$OnnxName.onnx.json")
}

function Get-PiperVoiceFromRepo {
    param(
        [string]$HfRepo,
        [string]$OnnxName,
        [string]$OnnxRemoteName = "model.onnx",
        [string]$JsonRemoteName = "model.onnx.json"
    )
    $base = "https://huggingface.co/$HfRepo/resolve/main"
    Get-HfFile "$base/$OnnxRemoteName" (Join-Path $tts "$OnnxName.onnx")
    Get-HfFile "$base/$JsonRemoteName" (Join-Path $tts "$OnnxName.onnx.json")
}

if ($Locale -eq "it" -or $Locale -eq "all") {
    Write-Host "=== Italiano (STT + TTS) ==="
    Get-SherpaKroko -LangFolder "it-kroko" -HfRepo "kouhxp/sherpa-onnx-streaming-zipformer-it-kroko"
    Get-PiperVoice -HfPath "it/it_IT/paola/medium" -OnnxName "it_IT-paola-medium"
    Get-PiperVoice -HfPath "it/it_IT/serena/high" -OnnxName "it_IT-serena-high"
    # Voci community kirys79 — addestrate su Multilingual LibriSpeech (CC BY 4.0)
    Get-PiperVoiceFromRepo -HfRepo "kirys79/piper_italiano" -OnnxName "it_IT-leonardo-medium" `
        -OnnxRemoteName "Leonardo/leonardo-epoch%3D2024-step%3D996300.onnx" `
        -JsonRemoteName "Leonardo/leonardo-epoch%3D2024-step%3D996300.json"
    Get-PiperVoiceFromRepo -HfRepo "kirys79/piper_italiano" -OnnxName "it_IT-giorgio-medium" `
        -OnnxRemoteName "Giorgio/giorgio-epoch%3D5028-step%3D1098436.onnx" `
        -JsonRemoteName "Giorgio/giorgio-epoch%3D5028-step%3D1098436.json"
}

if ($Locale -eq "es" -or $Locale -eq "all") {
    Write-Host "=== Espanol (STT + TTS) ==="
    Get-SherpaKroko -LangFolder "es-kroko" -HfRepo "kouhxp/sherpa-onnx-streaming-zipformer-es-kroko"
    Get-PiperVoice -HfPath "es/es_ES/sharvard/medium" -OnnxName "es_ES-sharvard-medium"
}

if ($Locale -eq "en" -or $Locale -eq "all") {
    Write-Host "=== English (STT + TTS) ==="
    Get-SherpaKroko -LangFolder "en-kroko" -HfRepo "kouhxp/sherpa-onnx-streaming-zipformer-en-kroko"
    Get-PiperVoice -HfPath "en/en_US/ljspeech/high" -OnnxName "en_US-ljspeech-high"
    Get-PiperVoice -HfPath "en/en_US/ryan/high" -OnnxName "en_US-ryan-high"
}

Write-Host "Done. Modelli in: $models"
Write-Host "Profilo IT: config/jarvisnet.audio.json | Profilo ES: config/jarvisnet.audio.es.json (JARVISNET_AUDIO_PROFILE=es)"
