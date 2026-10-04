$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$tool = Join-Path $PSScriptRoot "PiperDownloadTool\PiperDownloadTool.csproj"
dotnet run --project $tool -- $repoRoot
