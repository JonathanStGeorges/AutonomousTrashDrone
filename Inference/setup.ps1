param(
    [string]$Python="$env:USERPROFILE/Downloads/marineCam/.venv/Scripts/python.exe",
    [string]$Assets="$env:USERPROFILE/Downloads/marineCam",
    [switch]$CreateEnvironment,
    [string]$EnvironmentPath=(Join-Path $PSScriptRoot '.venv')
)
$ErrorActionPreference='Stop'
if(-not (Test-Path -LiteralPath $Python -PathType Leaf)) { throw "Explicit Python executable missing: $Python" }
if($CreateEnvironment) {
    if(Test-Path -LiteralPath $EnvironmentPath) { throw 'Isolated environment path already exists; refusing to modify an existing environment' }
    & $Python -m venv $EnvironmentPath
    if($LASTEXITCODE -ne 0) { throw 'venv creation failed' }
    $Python=Join-Path $EnvironmentPath 'Scripts/python.exe'
    & $Python -m pip install --requirement (Join-Path $PSScriptRoot 'environment.lock.txt') --extra-index-url https://download.pytorch.org/whl/cpu
    if($LASTEXITCODE -ne 0) { throw 'Pinned package installation failed; existing user environment was not touched' }
}
& $Python (Join-Path $PSScriptRoot 'setup_assets.py') --assets $Assets
if($LASTEXITCODE -ne 0) { throw 'Runtime or model verification failed; inspect the exact diagnostic above' }
Write-Output "Verified. Start with: ./Inference/start.ps1 -Python '$Python'"
