Param(
    [switch]$WhatIf
)

# start-local.ps1
# Convenience helper to publish the Blazor client and start the ClientHost and API for local testing.
# Usage:
#   ./start-local.ps1            # publish + start clienthost + start api (starts background dotnet processes)
#   ./start-local.ps1 -WhatIf    # dry-run: print the steps without executing them

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

$publishCmd = 'dotnet publish Client/Client.csproj -c Debug -o Client\publish'
$clientHostArgs = 'run --project Tools/ClientHost/ClientHost.csproj'
$apiArgs = 'run --project Api/Api.csproj'

function Write-Step([string]$msg) {
    Write-Host "[start-local] $msg"
}

Write-Step "Working directory: $PWD"

if ($WhatIf) {
    Write-Step "DRY-RUN mode (no commands will be run)"
    Write-Host "Would run: $publishCmd"
    Write-Host "Would start (background): dotnet $clientHostArgs"
    Write-Host "Would start (background): dotnet $apiArgs"
    exit 0
}

# 1) Publish client
Write-Step "Publishing client to Client/publish..."
& dotnet publish Client/Client.csproj -c Debug -o Client\publish
if ($LASTEXITCODE -ne 0) {
    Write-Error "Client publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
Write-Step "Publish complete. Files are in: $(Resolve-Path Client\publish)"

# 2) Start ClientHost (background)
Write-Step "Starting ClientHost (serves publish/wwwroot on https://localhost:5161)..."
$ch = Start-Process -FilePath 'dotnet' -ArgumentList $clientHostArgs -NoNewWindow -PassThru
Write-Step "ClientHost started (PID: $($ch.Id))"

# 3) Start API (background)
Write-Step "Starting API (Kestrel)..."
$api = Start-Process -FilePath 'dotnet' -ArgumentList $apiArgs -NoNewWindow -PassThru
Write-Step "API started (PID: $($api.Id))"

Write-Step "Done. Use the browser to open https://localhost:5161 (ClientHost) and https://localhost:7067 (API)."
Write-Host "To stop the background processes you can run: Stop-Process -Id $($ch.Id), Stop-Process -Id $($api.Id)"
