param([string]$Python = "$env:USERPROFILE/Downloads/marineCam/.venv/Scripts/python.exe", [string]$Device = 'cpu', [int]$ReadyTimeoutSeconds=45)
$ErrorActionPreference='Stop'
$service=Join-Path $PSScriptRoot 'service.py'
$projectRoot=Split-Path $PSScriptRoot -Parent
$statePath=Join-Path $PSScriptRoot 'service-state.json'
function Save-Ready($health) {
    if($health.service -ne 'TrashSailInference' -or $health.projectRoot -ne $projectRoot) { throw 'Port 8765 does not belong to this project service' }
    @{pid=$health.pid;projectRoot=$health.projectRoot;service=$health.service} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
    Write-Output "READY: project inference service PID $($health.pid), device $($health.device)"
}
$health=$null
try { $health=Invoke-RestMethod 'http://127.0.0.1:8765/health' -TimeoutSec 2 } catch { }
if($null -ne $health) {
    Save-Ready $health; exit 0
}
$existing=$null
if(Test-Path -LiteralPath $statePath) {
    $state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if($state.projectRoot -eq $projectRoot) {
        $existing=Get-CimInstance Win32_Process -Filter "ProcessId=$($state.pid)" -ErrorAction SilentlyContinue
        if($existing -and $existing.CommandLine -notlike "*$service*") { $existing=$null }
    }
}
if(-not $existing) {
    $process=Start-Process -FilePath $Python -ArgumentList @(('"'+$service+'"'),'--device',$Device) -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $PSScriptRoot 'service.stdout.log') -RedirectStandardError (Join-Path $PSScriptRoot 'service.stderr.log')
    @{pid=$process.Id;projectRoot=$projectRoot;service='TrashSailInference';status='loading'} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
}
$deadline=(Get-Date).AddSeconds($ReadyTimeoutSeconds)
do {
    try { $health=Invoke-RestMethod 'http://127.0.0.1:8765/health' -TimeoutSec 2 } catch { $health=$null }
    if($health -and $health.ok) { Save-Ready $health; exit 0 }
    Start-Sleep -Milliseconds 500
} while((Get-Date) -lt $deadline)
throw "Service not ready within $ReadyTimeoutSeconds seconds. Inspect Inference/service.stderr.log."
