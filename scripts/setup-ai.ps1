<#
.SYNOPSIS
  Sets up the free, local AI model for TFA (the TaskFlow assistant) through Ollama.

.DESCRIPTION
  Installs Ollama if it is missing (via winget), makes sure the Ollama server is running, downloads the model if it
  is not already there, and checks that the model answers. Safe to run again: it skips whatever is already done.

.PARAMETER Model
  The Ollama model to install. Default: llama3.2:3b (about 2 GB, runs on most laptops, supports tools).

.PARAMETER BaseUrl
  Where the Ollama server listens. Default: http://localhost:11434

.EXAMPLE
  .\scripts\setup-ai.ps1
  .\scripts\setup-ai.ps1 -Model llama3.1:8b
#>
param(
  [string]$Model = 'llama3.2:3b',
  [string]$BaseUrl = 'http://localhost:11434'
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

function Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Ok($text) { Write-Host "    $text" -ForegroundColor Green }

function Find-Ollama {
  $command = Get-Command ollama -ErrorAction SilentlyContinue
  if ($command) { return $command.Source }
  $default = Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama.exe'
  if (Test-Path $default) { return $default }
  return $null
}

function Test-Server {
  try {
    Invoke-RestMethod "$BaseUrl/api/version" -TimeoutSec 3 | Out-Null
    return $true
  } catch {
    return $false
  }
}

# 1. Ollama itself
Step 'Checking for Ollama'
$ollama = Find-Ollama
if (-not $ollama) {
  if (Get-Command winget -ErrorAction SilentlyContinue) {
    Write-Host '    Ollama is not installed. Installing it with winget...'
    winget install --id Ollama.Ollama -e --accept-source-agreements --accept-package-agreements
    $ollama = Find-Ollama
  }
  if (-not $ollama) {
    throw 'Ollama is not installed. Download it from https://ollama.com/download, install it, then run this script again.'
  }
}
Ok "Found $ollama"

# 2. The server
Step "Checking the Ollama server at $BaseUrl"
if (-not (Test-Server)) {
  Write-Host '    Starting the Ollama server...'
  Start-Process -FilePath $ollama -ArgumentList 'serve' -WindowStyle Hidden
  $deadline = (Get-Date).AddSeconds(30)
  while (-not (Test-Server) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
  if (-not (Test-Server)) {
    throw "The Ollama server did not start at $BaseUrl. Start the Ollama app and run this script again."
  }
}
Ok 'Server is running'

# 3. The model
Step "Checking for the model $Model"
$installed = @((Invoke-RestMethod "$BaseUrl/api/tags").models | ForEach-Object { $_.name })
if ($installed -contains $Model) {
  Ok 'Already installed'
} else {
  Write-Host "    Downloading $Model (this can take several minutes the first time)..."
  & $ollama pull $Model
  if ($LASTEXITCODE -ne 0) { throw "Downloading $Model failed. Check the model name and your internet connection." }
  Ok 'Downloaded'
}

# 4. A real answer
Step 'Testing the model'
$body = @{ model = $Model; stream = $false; messages = @(@{ role = 'user'; content = 'Reply with the single word OK.' }) } | ConvertTo-Json -Depth 5
try {
  $reply = Invoke-RestMethod "$BaseUrl/api/chat" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 180
  Ok "The model answered: $($reply.message.content.Trim())"
} catch {
  throw "The model is installed but did not answer: $($_.Exception.Message)"
}

Write-Host ''
Write-Host 'Done. Last step, in TaskFlow as an Administrator:' -ForegroundColor Green
Write-Host '  Settings > Application Settings > AI assistant (TFA) > Ollama'
Write-Host "  turn it on, set the model to '$Model', keep the address $BaseUrl, then click Test connection."
