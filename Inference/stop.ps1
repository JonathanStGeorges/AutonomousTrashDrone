$ErrorActionPreference='Stop'
$state=Get-Content (Join-Path $PSScriptRoot 'service-state.json') -Raw | ConvertFrom-Json
$health=Invoke-RestMethod 'http://127.0.0.1:8765/health'
$projectRoot=Split-Path $PSScriptRoot -Parent
if($health.service -ne 'TrashSailInference' -or $health.projectRoot -ne $projectRoot -or $state.projectRoot -ne $projectRoot -or $health.pid -ne $state.pid) { throw 'Service PID/project identity does not match this launcher; refusing shutdown' }
$body=@{owner='TrashSailDemo';pid=$health.pid;projectRoot=$projectRoot} | ConvertTo-Json
Invoke-RestMethod 'http://127.0.0.1:8765/shutdown' -Method Post -ContentType 'application/json' -Body $body
