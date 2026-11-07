param([switch]$KeepInference)
$ErrorActionPreference='Stop'
$demoRoot=Split-Path -Parent $PSScriptRoot
Push-Location $demoRoot
try {
    $executable=Join-Path $demoRoot 'Builds/TrashOceanDemo/TrashOceanDemo.exe'
    Get-Process -Name TrashOceanDemo -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $executable} | ForEach-Object {[void]$_.CloseMainWindow()}
    try {& (Join-Path $PSScriptRoot 'Invoke-TrashSailDemo.ps1') -Command stop -TimeoutSeconds 30}
    catch {Write-Output ('Editor not available: '+$_.Exception.Message)}
    finally {if(!$KeepInference){& (Join-Path $demoRoot 'Inference/stop.ps1')}}
} finally {Pop-Location}
