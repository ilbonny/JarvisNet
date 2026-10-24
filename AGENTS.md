# JarvisNet — Agent Instructions

## 🏗️ Architecture Overview

**Monorepo structure**:
- **Solution**: [`JarvisNet.slnx`](JarvisNet.slnx) (project reference file, not `.sln`)
- **Host**: [`src/JarvisNet.UI/`](src/JarvisNet.UI/) — ASP.NET Core MinimalAPI, public entrypoint `dotnet run --project src/JarvisNet.UI/JarvisNet.UI.csproj`
- **Core libs** in `src/`: `.csproj` files build independently
- **Tests** in `tests/`: Unit tests (`NUnit`) vs Integration tests (require Ollama/Piper)

```
JarvisNet/
├── JarvisNet.slnx                 ← Solution file (.slnx, not .sln)
├── config/                        ← Shared configuration JSON files
│   ├── jarvisnet.audio.json       ← TTS voice settings (it-IT es_ES)
│   ├── jarvisnet.engine.json      ← Ollama endpoint + model qwen3.5:9b
│   └── ... (audio locale variants)
├── scripts/                       ← PowerShell model download scripts
├── models/tts/                    ← TTS ONNX models (.onnx, gitignored but present in repo)
├── piper/                         ← Piper runtime DLLs (gitignored)
└── src/
    ├── JarvisNet.Core/           ← Shared types, contracts
    ├── JarvisNet.Engine/         ← Orchestrator: SK + Ollama connector
    ├── JarvisNet.Audio/          ← NAudio + Sherpa-ONNX TTS/StT
    ├── JarvisNet.Plugins.Sdk/    ← Plugin ABI for extending system
    ├── JarvisNet.Plugins.System/ ← Built-in plugins (SearchWeb, etc.)
    └── JarvisNet.UI/             ← Host API (public entrypoint)
```

## 🛠️ Commands You Need to Know

### Restore & Build
```bash
# Full restore + build entire solution
dotnet restore JarvisNet.slnx
dotnet build JarvisNet.slnx --no-restore  # after first restore

# Build single package (src/)
dotnet build src/JarvisNet.SearchWeb/JarvisNet.SearchWeb.csproj

# Run unit tests only
dotnet test JarvisNet.slnx               # runs all packages' tests
dotnet test src/JarvisNet.Core/           # specific project tests
```

### Test Categories
| Package | Framework | Notes |
|---------|-----------|-------|
| `tests/JarvisNet.UnitTests` | NUnit | Logic-only, no external deps |
| `tests/JarvisNet.IntegrationTests` | NUnit | Requires Ollama running |
| `tests/JarvisNet.Audio.UnitTests` | NUnit | Audio pipeline logic |
| `tests/JarvisNet.SearchWeb.UnitTests` | NUnit | Search plugins (DuckDuckGo, Wikipedia) |

### Test App Entrypoints (no web UI needed)
```bash
# Voice conversation: STT → Ollama → TTS
dotnet run --project tests/JarvisNet.Engine.TestApp/JarvisNet.Engine.TestApp.csproj

# Audio pipeline test (microphone → Piper)
dotnet run --project tests/JarvisNet.Audio.TestApp/JarvisNet.Audio.TestApp.csproj

# Web UI minimal API demo
dotnet run --project tests/JarvisNet.UI.TestApp/JarvisNet.UI.TestApp.csproj
```

### Model/Plugin Setup
```bash
# Pull Ollama model first (required for integration tests)
ollama pull qwen3.5:9b  # or any model in config/jarvisnet.engine.json

# Download TTS models and Piper runtime
powershell -ExecutionPolicy Bypass -File scripts/Download-Models.ps1          # STT + TTS ONNX
powershell -ExecutionPolicy Bypass -File scripts/Download-Piper.ps1            # Piper binaries
powershell -ExecutionPolicy Bypass -File scripts/PiperDownloadTool/program.exe  ← Alternative tool

# Playwright (optional for MCP integration tests)
powershell -ExecutionPolicy Bypass -File scripts/Install-PlaywrightBrowsers.ps1
```

### Running the Application
```bash
# Default port from launchSettings.json: HTTP 5194, HTTPS 7077
cd C:\projects\JarvisNet
dotnet run --project src/JarvisNet.UI/JarvisNet.UI.csproj

# API endpoints after startup:
GET /openapi/v1.json           ← OpenAPI JSON spec
POST /chat                      ← Main conversation endpoint
```

## ⚠️ Common Pitfalls & Gotchas

### 1. Missing Ollama Model
**Symptom**: Integration tests fail with connection refused to Ollama.  
**Fix**: Run `ollama pull qwen3.5:9b` (or whatever model is in `config/jarvisnet.engine.json.ModelName`). Keep service running during test execution.

### 2. TTS Model Path Issues
**Symptom**: Audio tests crash with "File not found" for ONNX models.  
**Fix**: Models are in `models/tts/` — ensure they're checked in or cloned separately per `scripts/Download-Models.ps1`. Gitignore them if you want to reduce repo size.

### 3. Piper Runtime Files
**Symptom**: TTS fails with "Module not found".  
**Fix**: Run `Download-Piper.ps1` to grab `piper.exe`, DLLs from official release to `piper/` folder. Not tracked in git but needed at runtime.

### 4. SearchWeb Plugin Configuration
**Context**: Added DuckDuckGo + Wikipedia fallback chain in `src/JarvisNet.SearchWeb`.  
**Important**: Service registered in DI requires proper HttpClient pattern (singleton per client name). Use:
```csharp
services.AddHttpClient(duckDuckGoClientName, ConfigureClient)
    .AddTransient(sp => new DuckDuckGoSearchService(
        sp.GetRequiredService<HttpClient>(duckDuckGoClientName), // ← DI-managed singleton
        logger, culture));
```
NOT instantiating HttpClient inside your factory lambda.

### 5. Configuration Loading Pattern
**Location**: Both [`src/JarvisNet.UI/appsettings.json`](src/JarvisNet.UI/appsettings.json) and shared configs in `config/`.  
**Pattern**: Share audio/engine config via extension methods:
```csharp
services.AddJarvisNetSharedAudioConfiguration(...);  // from config/jarvisnet.audio.json
services.AddJarvisNetSharedEngineConfiguration(...); // from config/jarvisnet.engine.json
```

### 6. Testing Locale Settings
**Context**: TTS models are region-specific (it_IT, es_ES).  
**Fix**: Set environment variable before running test apps:
```bash
$env:JARVISNET_AUDIO_PROFILE="es"  # or "it" for default Italian
dotnet run --project tests/JarvisNet.Audio.TestApp/JarvisNet.Audio.TestApp.csproj
```

## 📦 Package Boundaries & Ownership
- **JarvisNet.Core**: Shared types, enums, contracts across modules — avoid adding business logic here.
- **JarvisNet.Engine**: Main orchestrator using Semantic Kernel + Ollama connector. Where plugin calls land.
- **JarvisNet.Audio**: TTS/STT pipeline (NAudio + Sherpa-ONNX + PiperSharp). No HTTP deps here.
- **JarvisNet.Plugins.Sdk**: Public API for writing new plugins (.NET SDK style). Extend this when you add custom integrations.
- **JarvisNet.SearchWeb** (part of Plugins.System): Web search plugin (DuckDuckGo, Wikipedia, NewsRSS). Managed via DI with HttpClient pattern above.

## 🧪 Test Best Practices

1. **Test order matters**: Some integration tests rely on services started in other test fixtures.
2. **Unit tests are safe**: Run `JarvisNet.slnx` without any prerequisites (no Ollama required).
3. **Integration tests need**: Running Ollama service + models present in `models/`.
4. **Search tests**: Use mocked HttpClient or Dockerized DuckDuckGo if production API fails intermittently.

## 🌐 Configuration Files Reference

| File | Purpose | Edit When... |
|------|---------|--------------|
| `config/jarvisnet.engine.json` | Ollama endpoint + model name + prompts | Adding new LLM backend or changing model |
| `config/jarvisnet.audio.json` | Italian TTS voice keys (it_IT-paola-medium etc.) | Changing voices or locales |
| `src/JarvisNet.UI/appsettings.json` | App name, UI settings, SignalR hubs | Customizing runtime UI behavior |

## 🔨 Contribution Workflow

1. **Branch**: Create from main, not dev
2. **Code style**: Follow existing C# coding conventions (use IDE formatting)
3. **Test**: Run `dotnet test` on solution OR your modified package only
4. **Commit**: Small atomic commits with Italian/English mix OK (repo is Italian-focused but multilingual codebase)
5. **PR**: Link to feature description in commit message

## 📝 Quick Reference Commands Cheat Sheet

```bash
# Build everything fresh from scratch
dotnet restore JarvisNet.slnx && dotnet build JarvisNet.slnx

# Run all tests (skip Ollama-dependent ones by commenting out integration test projects)
dotnet test -p:CollectCoverage=true

# Run specific unit test project
dotnet test tests/JarvisNet.SearchWeb.UnitTests/ --filter "FullyQualifiedName~DuckDuckGoSearchService"

# Build and run engine test app with Spanish voice
$env:JARVISNET_AUDIO_PROFILE="es"; dotnet run --project tests/JarvisNet.Engine.TestApp/

# Start API service (for local development/debugging)
dotnet run --project src/JarvisNet.UI/ --urls "http://localhost:5194"
```
