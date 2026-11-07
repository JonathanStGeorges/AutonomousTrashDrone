param([switch]$Compact,[switch]$Player,[string]$Python="$env:USERPROFILE/Downloads/marineCam/.venv/Scripts/python.exe")
$ErrorActionPreference='Stop'
$demoRoot=Split-Path -Parent $PSScriptRoot
Push-Location $demoRoot
try {
    & (Join-Path $demoRoot 'Inference/start.ps1') -Python $Python
    if($Player) {
        $executable=Join-Path $demoRoot 'Builds/TrashOceanDemo/TrashOceanDemo.exe'
        if(!(Test-Path -LiteralPath $executable)){throw 'Build the Windows player using Trash Sail Demo / Build Windows Player first.'}
        $existing=Get-Process -Name TrashOceanDemo -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $executable}
        if(!$existing){Start-Process -FilePath $executable -WorkingDirectory $demoRoot -ArgumentList @('-logFile',('"'+(Join-Path $demoRoot 'Evidence/TrashSailDemo/player.log')+'"'))}
    } else {
        $bridge=Join-Path $PSScriptRoot 'Invoke-TrashSailDemo.ps1'
        & $bridge -Command status
        if($LASTEXITCODE -ne 0){throw 'Unity status check failed.'}
        $status=Get-Content -Raw (Join-Path $demoRoot 'Evidence/TrashSailDemo/editor-response-status.json') | ConvertFrom-Json
        if($status.detail -match 'isPlaying=True'){Write-Output 'Unity is already running; use Reset seed or Stop mission in the dashboard.';return}
        if($Compact){& $bridge -Command setup}else{& $bridge -Command ocean}
        if($LASTEXITCODE -ne 0){throw 'Unity scene setup failed.'}
        & $bridge -Command play
        if($LASTEXITCODE -ne 0){throw 'Unity Play Mode launch failed.'}
    }
} finally {Pop-Location}
