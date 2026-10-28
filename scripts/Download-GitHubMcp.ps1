# Downloads the official github/github-mcp-server release binary (no Docker).
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$installDir = Join-Path $repoRoot "github-mcp"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

# Win32_Processor.Architecture: 0=x86, 5=ARM32, 9=x64, 12=ARM64 (see MSDN).
$procArch = $env:PROCESSOR_ARCHITECTURE
$arch = if ($procArch -eq "ARM64") {
    "arm64"
} else {
    switch ((Get-CimInstance Win32_Processor | Select-Object -First 1).Architecture) {
        12 { "arm64" }
        0 { "i386" }
        default { "x86_64" }
    }
}

$headers = @{ "User-Agent" = "JarvisNet-Download-GitHubMcp" }
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/github/github-mcp-server/releases/latest" -Headers $headers
$tag = $release.tag_name -replace '^v', ''
$assetName = "github-mcp-server_Windows_$arch.zip"
$asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
if (-not $asset) {
    throw "Asset not found: $assetName (release $($release.tag_name))"
}

$zipPath = Join-Path $env:TEMP "github-mcp-server_$tag.zip"
Write-Host "Downloading $($asset.browser_download_url) ..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -Headers $headers

$extractRoot = Join-Path $env:TEMP "github-mcp-server_extract_$tag"
if (Test-Path $extractRoot) {
    Remove-Item -Recurse -Force $extractRoot
}
Expand-Archive -Path $zipPath -DestinationPath $extractRoot -Force

$exe = Get-ChildItem -Path $extractRoot -Recurse -Filter "github-mcp-server.exe" | Select-Object -First 1
if (-not $exe) {
    throw "github-mcp-server.exe not found inside archive."
}

$dest = Join-Path $installDir "github-mcp-server.exe"
Copy-Item -Path $exe.FullName -Destination $dest -Force
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item $extractRoot -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Installed: $dest (release v$tag)"
Write-Host "Set GITHUB_PERSONAL_ACCESS_TOKEN in config/jarvisnet.mcp.secrets.json and enable McpServers:GitHub."
