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
$skills = [ordered]@{
    apply   = 'openspec-apply-change'
    archive = 'openspec-archive-change'
    explore = 'openspec-explore'
    propose = 'openspec-propose'
    sync    = 'openspec-sync-specs'
    update  = 'openspec-update-change'
}
$utf8 = New-Object System.Text.UTF8Encoding $false

foreach ($cmd in $skills.Keys) {
    $skill = $skills[$cmd]
    $body = "Load the ``$skill`` skill (from ``.claude/skills/$skill/SKILL.md``) and follow its instructions exactly.`n`n**Provided arguments**: `$ARGUMENTS`n"

    foreach ($file in @(".claude/commands/opsx/$cmd.md", ".opencode/commands/opsx-$cmd.md")) {
        if (-not (Test-Path $file)) { continue }
        $text = [IO.File]::ReadAllText((Resolve-Path $file))
        # Keep the generated frontmatter (first --- ... --- block), swap the body.
        if ($text -match '(?s)^(---\r?\n.*?\r?\n---)') {
            [IO.File]::WriteAllText((Join-Path $root $file), "$($Matches[1])`n`n$body", $utf8)
        }
    }
}

Write-Host 'Deduplicated: skills live only in .claude/skills; commands are thin wrappers.'
