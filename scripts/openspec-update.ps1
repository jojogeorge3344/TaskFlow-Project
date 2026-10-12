# Runs `openspec update`, then removes the duplicates it regenerates so that
# .claude/skills/ stays the single shared source for Claude Code and OpenCode.
# Usage (from anywhere):  powershell -ExecutionPolicy Bypass -File scripts/openspec-update.ps1 [--force]

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

openspec update @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# OpenCode reads .claude/skills natively, so drop its regenerated copies.
Get-ChildItem '.opencode/skills' -Directory -Filter 'openspec-*' -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force
if ((Test-Path '.opencode/skills') -and -not (Get-ChildItem '.opencode/skills')) {
    Remove-Item '.opencode/skills' -Force
}

# Replace the regenerated full-length commands with thin wrappers around the shared skills.
# A generated command repeats its skill's body, so they are paired by the first body line;
# this also covers workflows added later via `openspec config profile`.
$frontmatter = '(?s)^(---\r?\n.*?\r?\n---)\s*(.*)$'
function Get-FirstBodyLine([string]$text) {
    if ($text -notmatch $frontmatter) { return $null }
    ($Matches[2] -split '\r?\n' | Where-Object { $_.Trim() } | Select-Object -First 1)
}

$skillByFirstLine = @{}
foreach ($skillFile in Get-ChildItem '.claude/skills/*/SKILL.md') {
    $line = Get-FirstBodyLine ([IO.File]::ReadAllText($skillFile.FullName))
    if ($line) { $skillByFirstLine[$line] = $skillFile.Directory.Name }
}

$utf8 = New-Object System.Text.UTF8Encoding $false
$commandFiles = @(Get-ChildItem '.claude/commands/opsx/*.md', '.opencode/commands/opsx-*.md' -ErrorAction SilentlyContinue)

foreach ($file in $commandFiles) {
    $text = [IO.File]::ReadAllText($file.FullName)
    $skill = $skillByFirstLine[(Get-FirstBodyLine $text)]
    if (-not $skill) { continue }   # already a wrapper, or no matching skill

    $null = $text -match $frontmatter
    $body = "Load the ``$skill`` skill (from ``.claude/skills/$skill/SKILL.md``) and follow its instructions exactly.`n`n**Provided arguments**: `$ARGUMENTS`n"
    [IO.File]::WriteAllText($file.FullName, "$($Matches[1])`n`n$body", $utf8)
}

Write-Host 'Deduplicated: skills live only in .claude/skills; commands are thin wrappers.'
