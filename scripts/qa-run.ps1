<#
.SYNOPSIS
  Builds TaskFlow, runs it as one app on this PC in the QA configuration and (optionally) exposes it at a free public
  HTTPS address through a Cloudflare quick tunnel. No cloud account or payment is needed.

.PARAMETER Port
  Local HTTP port the API (and the compiled client) listens on.

.PARAMETER DataRoot
  Folder that keeps everything that must survive a restart: database, attachments, key ring and the JWT signing key.

.PARAMETER NoTunnel
  Run locally only, without a public address.

.PARAMETER DisableMfa
  Do not require the authenticator-app setup for sign-in. Convenient for a live demo, but anyone with the public address and the  default passwords can then sign in, so only use it while presenting..PARAMETER SkipBuild
  Reuse the previous build in <DataRoot>\app instead of rebuilding the client and the API.

.EXAMPLE
  .\scripts\qa-run.ps1
#>
[CmdletBinding()]
param(
    [int]$Port = 5180,
    [string]$DataRoot = (Join-Path $env:LOCALAPPDATA 'TaskFlowQA'),
    [switch]$NoTunnel,
    [switch]$DisableMfa,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$appRoot = Join-Path $repoRoot 'TaskFlow Proj'
$frontend = Join-Path $appRoot 'frontend'
$apiProject = Join-Path $appRoot 'backend\src\TaskFlow.Api'
$publishDir = Join-Path $DataRoot 'app'
$storageDir = Join-Path $DataRoot 'data'
$logDir = Join-Path $DataRoot 'logs'

function Require-Tool([string]$name, [string]$hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "'$name' was not found. $hint"
    }
}

function Stop-Tree($process) {
    if ($process -and -not $process.HasExited) {
        & taskkill /PID $process.Id /T /F | Out-Null
    }
}

# 1. Tools
Require-Tool 'dotnet' 'Install the .NET SDK from https://dotnet.microsoft.com/download'
Require-Tool 'node' 'Install Node.js from https://nodejs.org'
Require-Tool 'npm' 'Install Node.js from https://nodejs.org'
if (-not $NoTunnel) {
    Require-Tool 'cloudflared' 'Install it with: winget install --id Cloudflare.cloudflared   (then open a new terminal). Or run with -NoTunnel.'
}

New-Item -ItemType Directory -Force -Path $DataRoot, $storageDir, $logDir | Out-Null

# 2. Build the client and the API into one folder
if (-not $SkipBuild) {
    Write-Host '==> Building the client' -ForegroundColor Cyan
    Push-Location $frontend
    try {
        if (-not (Test-Path 'node_modules')) { npm ci; if ($LASTEXITCODE) { throw 'npm ci failed' } }
        npx ng build --configuration production
        if ($LASTEXITCODE) { throw 'The client build failed' }
    }
    finally { Pop-Location }

    Write-Host '==> Publishing the API' -ForegroundColor Cyan
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    dotnet publish $apiProject -c Release -o $publishDir
    if ($LASTEXITCODE) { throw 'dotnet publish failed' }

    $clientOutput = Join-Path $frontend 'dist\taskflow-frontend\browser'
    if (-not (Test-Path (Join-Path $clientOutput 'index.html'))) { throw "No index.html in $clientOutput" }
    $webRoot = Join-Path $publishDir 'wwwroot'
    if (Test-Path $webRoot) { Remove-Item $webRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $webRoot | Out-Null
    Copy-Item (Join-Path $clientOutput '*') $webRoot -Recurse -Force
}
if (-not (Test-Path (Join-Path $publishDir 'wwwroot\index.html'))) {
    throw "$publishDir has no client build. Run again without -SkipBuild."
}

# 3. Signing key: generated once per machine, kept beside the data and never in the repository
$keyFile = Join-Path $DataRoot 'jwt.key'
if (-not (Test-Path $keyFile)) {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [Convert]::ToBase64String($bytes) | Set-Content -Path $keyFile -NoNewline -Encoding ASCII
}

$tunnel = $null
$api = $null
try {
    # 4. Tunnel first, so the public address is known before the API starts
    $publicUrl = $null
    if (-not $NoTunnel) {
        Write-Host '==> Starting the Cloudflare quick tunnel' -ForegroundColor Cyan
        $tunnelLog = Join-Path $logDir 'tunnel.log'
        Remove-Item $tunnelLog -ErrorAction SilentlyContinue
        $tunnel = Start-Process cloudflared -ArgumentList "tunnel --url http://localhost:$Port --no-autoupdate" `
            -RedirectStandardError $tunnelLog -RedirectStandardOutput (Join-Path $logDir 'tunnel.out.log') `
            -WindowStyle Hidden -PassThru
        for ($i = 0; $i -lt 60 -and -not $publicUrl; $i++) {
            Start-Sleep -Seconds 1
            if ($tunnel.HasExited) { throw "cloudflared stopped. See $tunnelLog" }
            if (Test-Path $tunnelLog) {
                $match = Select-String -Path $tunnelLog -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' | Select-Object -First 1
                if ($match) { $publicUrl = $match.Matches[0].Value }
            }
        }
        if (-not $publicUrl) { throw "No public address appeared. See $tunnelLog" }
    }
    $baseUrl = if ($publicUrl) { $publicUrl } else { "http://localhost:$Port" }

    # 5. API (serves the client too)
    Write-Host '==> Starting TaskFlow' -ForegroundColor Cyan
    $env:ASPNETCORE_ENVIRONMENT = 'QA'
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    $env:Jwt__Key = (Get-Content $keyFile -Raw).Trim()
    $env:Storage__Root = $storageDir
    $env:Cors__AllowedOrigins__0 = $baseUrl
    $env:Mail__AppBaseUrl = $baseUrl
    if ($DisableMfa) { $env:Mfa__Required = "false" } else { Remove-Item Env:Mfa__Required -ErrorAction SilentlyContinue }
    $apiLog = Join-Path $logDir 'api.log'
    $api = Start-Process dotnet -ArgumentList 'TaskFlow.Api.dll' -WorkingDirectory $publishDir `
        -RedirectStandardOutput $apiLog -RedirectStandardError (Join-Path $logDir 'api.err.log') `
        -WindowStyle Hidden -PassThru

    $ready = $false
    for ($i = 0; $i -lt 90 -and -not $ready; $i++) {
        Start-Sleep -Seconds 1
        if ($api.HasExited) { throw "The API stopped. See $apiLog and api.err.log in $logDir" }
        try { $ready = (Invoke-WebRequest "http://localhost:$Port/" -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 } catch { }
    }
    if (-not $ready) { throw "The API did not answer on port $Port. See $apiLog" }

    Write-Host ''
    Write-Host "TaskFlow is running:  $baseUrl" -ForegroundColor Green
    Write-Host '  admin@taskflow.com / manager@taskflow.com / employee@taskflow.com   (password 123456)'
    Write-Host "  Data: $storageDir"
    Write-Host '  Demo accounts use default passwords: share the address only while presenting.'
    Write-Host '  Press Ctrl+C to stop.'
    while (-not $api.HasExited) { Start-Sleep -Seconds 2 }
    Write-Host 'The API stopped. See the logs in' $logDir
}
finally {
    Write-Host 'Stopping...' -ForegroundColor Yellow
    Stop-Tree $api
    Stop-Tree $tunnel
}
