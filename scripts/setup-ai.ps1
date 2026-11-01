<#
.SYNOPSIS
  Sets up the AI for TFA (the TaskFlow assistant): a free Google Gemini key, and optionally a local Ollama chat model.

.DESCRIPTION
  Asks for a Google Gemini API key (free from https://aistudio.google.com/apikey), checks that Google accepts it, and
  stores it with .NET user secrets for the API project as AiProviders:GeminiApiKey. On the first start of a new database
  the API then switches Gemini on and indexes the TaskFlow documents with it. The key never goes into the repository.

  With -WithOllama it also installs Ollama if it is missing (via winget), makes sure the Ollama server is running,
  downloads the chat model and checks that it answers. Ollama is optional and only answers chats: document search always
  uses Gemini. Safe to run again: it skips whatever is already done.

.PARAMETER GeminiApiKey
  The Gemini key. When omitted, the script asks for it (the typed key is not shown).

.PARAMETER SkipGemini
  Do not ask for or store a Gemini key (for example when it is already saved in TFA Settings).

.PARAMETER WithOllama
  Also set up the optional local Ollama chat model.

.PARAMETER Model
  The Ollama chat model to install with -WithOllama. Default: llama3.2:3b (about 2 GB, runs on most laptops, supports tools).

.PARAMETER BaseUrl
  Where the Ollama server listens. Default: http://localhost:11434

.EXAMPLE
  .\scripts\setup-ai.ps1
  .\scripts\setup-ai.ps1 -WithOllama -Model llama3.1:8b
#>
param(
  [string]$GeminiApiKey,
  [switch]$SkipGemini,
  [switch]$WithOllama,
  [string]$Model = 'llama3.2:3b',
  [string]$BaseUrl = 'http://localhost:11434'
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$apiProject = Join-Path $PSScriptRoot '..\TaskFlow Proj\backend\src\TaskFlow.Api'

function Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Ok($text) { Write-Host "    $text" -ForegroundColor Green }

# 1. Google Gemini (default)
if (-not $SkipGemini) {
  Step 'Google Gemini API key'
  if (-not $GeminiApiKey) {
    Write-Host '    Create a free key at https://aistudio.google.com/apikey (sign in with a Google account, Create API key).'
    $secure = Read-Host '    Paste the key' -AsSecureString
    $GeminiApiKey = [System.Net.NetworkCredential]::new('', $secure).Password
  }
  $GeminiApiKey = $GeminiApiKey.Trim()
  if (-not $GeminiApiKey) { throw 'No key was given. Run the script again with a key, or use -SkipGemini.' }

  # Listing the models is free and proves Google accepts the key; the key goes in a header, never in the URL.
  try {
    Invoke-RestMethod 'https://generativelanguage.googleapis.com/v1beta/models?pageSize=1' -Headers @{ 'x-goog-api-key' = $GeminiApiKey } -TimeoutSec 30 | Out-Null
    Ok 'Google accepted the key'
  } catch {
    throw "Google did not accept the key ($($_.Exception.Message)). Check it in Google AI Studio and run the script again."
  }

  if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The .NET SDK is not installed, so the key cannot be stored with user secrets.' }
  & dotnet user-secrets set 'AiProviders:GeminiApiKey' $GeminiApiKey --project $apiProject | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'Storing the key with dotnet user-secrets failed.' }
  Ok 'Stored as AiProviders:GeminiApiKey in the API project''s user secrets (outside the repository)'
}

if ($WithOllama) {
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

  # 2. Ollama itself
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

  # 3. The server
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

  # 4. The chat model
  Step "Checking for the model $Model"
  $installed = @((Invoke-RestMethod "$BaseUrl/api/tags").models | ForEach-Object { $_.name })
  # A name without a tag is listed by Ollama as name:latest.
  if ($installed -contains $Model -or ($Model -notmatch ':' -and $installed -contains "$($Model):latest")) {
    Ok 'Already installed'
  } else {
    Write-Host "    Downloading $Model (this can take several minutes the first time)..."
    & $ollama pull $Model
    if ($LASTEXITCODE -ne 0) { throw "Downloading $Model failed. Check the model name and your internet connection." }
    Ok 'Downloaded'
  }

  # 5. A real answer
  Step 'Testing the model'
  $body = @{ model = $Model; stream = $false; messages = @(@{ role = 'user'; content = 'Reply with the single word OK.' }) } | ConvertTo-Json -Depth 5
  try {
    $reply = Invoke-RestMethod "$BaseUrl/api/chat" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 180
    Ok "The model answered: $($reply.message.content.Trim())"
  } catch {
    throw "The model is installed but did not answer: $($_.Exception.Message)"
  }
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
if (-not $SkipGemini) {
  Write-Host '  On the first start of a new database, TFA uses Google Gemini and indexes the TaskFlow documents with it.'
  Write-Host '  On an existing database, paste the key in Settings > TFA Settings (Add AI model, Google Gemini) instead.'
}
if ($WithOllama) {
  Write-Host "  To chat with Ollama: Settings > TFA Settings, use '$Model' at $BaseUrl, then click Test connection."
  Write-Host '  Document search keeps using Google Gemini.'
}
