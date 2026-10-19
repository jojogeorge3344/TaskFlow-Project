#!/usr/bin/env bash
# Sets up the free, local AI model for TFA (the TaskFlow assistant) through Ollama.
#
# Checks for Ollama, makes sure its server is running, downloads the model if it is not already there, and checks that
# the model answers. Safe to run again: it skips whatever is already done.
#
# Usage:  ./scripts/setup-ai.sh [model] [base-url]
#         ./scripts/setup-ai.sh llama3.1:8b
set -euo pipefail

MODEL="${1:-llama3.2:3b}"
BASE_URL="${2:-http://localhost:11434}"
BASE_URL="${BASE_URL%/}"

step() { printf '==> %s\n' "$1"; }
ok() { printf '    %s\n' "$1"; }
die() { printf 'Error: %s\n' "$1" >&2; exit 1; }

server_up() { curl -fsS --max-time 3 "$BASE_URL/api/version" >/dev/null 2>&1; }

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

step "Checking the Ollama server at $BASE_URL"
if ! server_up; then
  echo "    Starting the Ollama server..."
  nohup ollama serve >/dev/null 2>&1 &
  for _ in $(seq 1 30); do server_up && break; sleep 1; done
  server_up || die "The Ollama server did not start at $BASE_URL. Start Ollama and run this script again."
fi
ok "Server is running"

step "Checking for the model $MODEL"
if curl -fsS "$BASE_URL/api/tags" | grep -q "\"name\":\"$MODEL\""; then
  ok "Already installed"
else
  echo "    Downloading $MODEL (this can take several minutes the first time)..."
  ollama pull "$MODEL" || die "Downloading $MODEL failed. Check the model name and your internet connection."
  ok "Downloaded"
fi

step "Testing the model"
reply="$(curl -fsS --max-time 180 "$BASE_URL/api/chat" -H 'Content-Type: application/json' \
  -d "{\"model\":\"$MODEL\",\"stream\":false,\"messages\":[{\"role\":\"user\",\"content\":\"Reply with the single word OK.\"}]}")" \
  || die "The model is installed but did not answer."
ok "The model answered."

echo
echo "Done. Last step, in TaskFlow as an Administrator:"
echo "  Settings > Application Settings > AI assistant (TFA) > Ollama"
echo "  turn it on, set the model to '$MODEL', keep the address $BASE_URL, then click Test connection."
