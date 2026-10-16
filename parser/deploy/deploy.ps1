# One-command deployment of the parser API to the VPS (publish -> upload -> the server applies it and restarts).
# Usage (PowerShell, from anywhere):   .\parser\deploy\deploy.ps1
#   -Server  root@187.77.98.186      where to deploy
#   -SkipBuild                       upload the existing publish\parser-api folder without rebuilding
# Needs the one-time server setup (neuralnetgoal-deploy.path). Tip: set up an SSH key once, otherwise you are asked
# for the password for each ssh/scp call.
param(
    [string]$Server = "root@187.77.98.186",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$out  = Join-Path $repo "publish\parser-api"
$incoming = "/opt/neuralnetgoal-incoming"

if (-not $SkipBuild) {
    Write-Host "== publishing ==" -ForegroundColor Cyan
    dotnet publish (Join-Path $repo "parser\neuralnetgoal.parser.api") -c Release -o $out -p:UseAppHost=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
}
if (-not (Test-Path (Join-Path $out "neuralnetgoal.parser.api.dll"))) { throw "No published files in $out" }

Write-Host "== clearing the incoming folder on the server ==" -ForegroundColor Cyan
ssh $Server "find $incoming -mindepth 1 ! -name DEPLOY -delete"
if ($LASTEXITCODE -ne 0) { throw "could not clear $incoming (is the one-time server setup done?)" }

Write-Host "== uploading ==" -ForegroundColor Cyan
scp -r "$out\*" "${Server}:$incoming/"
if ($LASTEXITCODE -ne 0) { throw "upload failed" }

# the marker goes LAST, with new content each time, so the server knows the upload is complete
$marker = Join-Path $env:TEMP "DEPLOY"
"deployed $(Get-Date -Format s)" | Set-Content -Path $marker -Encoding ascii
scp $marker "${Server}:$incoming/DEPLOY"
if ($LASTEXITCODE -ne 0) { throw "could not upload the DEPLOY marker" }

Write-Host "== uploaded. The server applies it in ~20 seconds; see the result with: ==" -ForegroundColor Green
Write-Host "   ssh $Server `"tail -n 5 /var/log/neuralnetgoal-deploy.log`""
