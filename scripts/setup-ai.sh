#!/usr/bin/env bash
# Sets up the AI for TFA (the TaskFlow assistant): a free Google Gemini key, and optionally a local Ollama chat model.
#
# Asks for a Google Gemini API key (free from https://aistudio.google.com/apikey), checks that Google accepts it, and
# stores it with .NET user secrets for the API project as AiProviders:GeminiApiKey. On the first start of a new database
# the API then switches Gemini on and indexes the TaskFlow documents with it. The key never goes into the repository.
#
# With --with-ollama it also checks for Ollama, makes sure its server is running, downloads the chat model and checks
# that it answers. Ollama is optional and only answers chats: document search always uses Gemini. Safe to run again.
#
# Usage:  ./scripts/setup-ai.sh [--skip-gemini] [--with-ollama] [--model llama3.2:3b] [--base-url http://localhost:11434]
#         GEMINI_API_KEY=... ./scripts/setup-ai.sh        (no prompt)
set -euo pipefail

SKIP_GEMINI=0
WITH_OLLAMA=0
MODEL="llama3.2:3b"
BASE_URL="http://localhost:11434"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-gemini) SKIP_GEMINI=1 ;;
    --with-ollama) WITH_OLLAMA=1 ;;
    --model) MODEL="$2"; shift ;;
    --base-url) BASE_URL="$2"; shift ;;
    *) printf 'Unknown option: %s\n' "$1" >&2; exit 2 ;;
  esac
  shift
done
BASE_URL="${BASE_URL%/}"
API_PROJECT="$(cd "$(dirname "$0")/.." && pwd)/TaskFlow Proj/backend/src/TaskFlow.Api"

step() { printf '==> %s\n' "$1"; }
ok() { printf '    %s\n' "$1"; }
die() { printf 'Error: %s\n' "$1" >&2; exit 1; }

# 1. Google Gemini (default)
if [[ "$SKIP_GEMINI" -eq 0 ]]; then
  step "Google Gemini API key"
  key="${GEMINI_API_KEY:-}"
  if [[ -z "$key" ]]; then
    echo "    Create a free key at https://aistudio.google.com/apikey (sign in with a Google account, Create API key)."
    read -r -s -p "    Paste the key: " key
    echo
  fi
  key="$(printf '%s' "$key" | tr -d '[:space:]')"
  [[ -n "$key" ]] || die "No key was given. Run the script again with a key, or use --skip-gemini."

  # Listing the models is free and proves Google accepts the key; the key goes in a header, never in the URL.
  curl -fsS --max-time 30 -H "x-goog-api-key: $key" "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1" >/dev/null \
    || die "Google did not accept the key. Check it in Google AI Studio and run the script again."
  ok "Google accepted the key"

  command -v dotnet >/dev/null 2>&1 || die "The .NET SDK is not installed, so the key cannot be stored with user secrets."
  dotnet user-secrets set "AiProviders:GeminiApiKey" "$key" --project "$API_PROJECT" >/dev/null \
    || die "Storing the key with dotnet user-secrets failed."
  ok "Stored as AiProviders:GeminiApiKey in the API project's user secrets (outside the repository)"
fi

if [[ "$WITH_OLLAMA" -eq 1 ]]; then
  server_up() { curl -fsS --max-time 3 "$BASE_URL/api/version" >/dev/null 2>&1; }

  # 2. Ollama itself
  step "Checking for Ollama"
  if ! command -v ollama >/dev/null 2>&1; then
    case "$(uname -s)" in
      Darwin)
        command -v brew >/dev/null 2>&1 && { echo "    Installing Ollama with Homebrew..."; brew install ollama; } ;;
      Linux)
        read -r -p "    Ollama is not installed. Install it now with the official script from ollama.com? [y/N] " answer
        if [[ "${answer:-}" =~ ^[Yy]$ ]]; then curl -fsSL https://ollama.com/install.sh | sh; fi ;;
    esac
  fi
  command -v ollama >/dev/null 2>&1 || die "Ollama is not installed. Download it from https://ollama.com/download, install it, then run this script again."
  ok "Found $(command -v ollama)"

  # 3. The server
  step "Checking the Ollama server at $BASE_URL"
  if ! server_up; then
    echo "    Starting the Ollama server..."
    nohup ollama serve >/dev/null 2>&1 &
    for _ in $(seq 1 30); do server_up && break; sleep 1; done
    server_up || die "The Ollama server did not start at $BASE_URL. Start Ollama and run this script again."
  fi
  ok "Server is running"

  # 4. The chat model
  step "Checking for the model $MODEL"
  tags="$(curl -fsS "$BASE_URL/api/tags")"
  # A name without a tag is listed by Ollama as name:latest.
  if grep -q "\"name\":\"$MODEL\"" <<<"$tags" || { [[ "$MODEL" != *:* ]] && grep -q "\"name\":\"$MODEL:latest\"" <<<"$tags"; }; then
    ok "Already installed"
  else
    echo "    Downloading $MODEL (this can take several minutes the first time)..."
    ollama pull "$MODEL" || die "Downloading $MODEL failed. Check the model name and your internet connection."
    ok "Downloaded"
  fi

  # 5. A real answer
  step "Testing the model"
  curl -fsS --max-time 180 "$BASE_URL/api/chat" -H 'Content-Type: application/json' \
    -d "{\"model\":\"$MODEL\",\"stream\":false,\"messages\":[{\"role\":\"user\",\"content\":\"Reply with the single word OK.\"}]}" >/dev/null \
    || die "The model is installed but did not answer."
  ok "The model answered."
fi

echo
echo "Done."
if [[ "$SKIP_GEMINI" -eq 0 ]]; then
  echo "  On the first start of a new database, TFA uses Google Gemini and indexes the TaskFlow documents with it."
  echo "  On an existing database, paste the key in Settings > TFA Settings (Add AI model, Google Gemini) instead."
fi
if [[ "$WITH_OLLAMA" -eq 1 ]]; then
  echo "  To chat with Ollama: Settings > TFA Settings, use '$MODEL' at $BASE_URL, then click Test connection."
  echo "  Document search keeps using Google Gemini."
fi
