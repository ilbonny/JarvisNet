# Installs Chromium for @playwright/mcp (run once after enabling Playwright MCP).
# Prerequisite: Node 20+ — run `nvm use 24.13.0` before this script.

$ErrorActionPreference = "Stop"

$nodeVersion = (node -v) -replace '^v', ''
$major = [int]($nodeVersion.Split('.')[0])
if ($major -lt 20) {
    Write-Error "Node $nodeVersion rilevato. Serve Node 20+ (es. nvm use 24.13.0)."
}

Write-Host "Installing Playwright Chromium (same stack as @playwright/mcp)..."
npx -y --package=playwright --package=@playwright/mcp playwright install chrome chromium
Write-Host "Browsers folder: $env:LOCALAPPDATA\ms-playwright"
Write-Host "Done."
