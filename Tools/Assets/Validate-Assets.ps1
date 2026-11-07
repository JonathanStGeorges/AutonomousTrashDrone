$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$imported=Join-Path $project 'Assets/TrashSailDemo/Imported'
$manifest=Get-Content (Join-Path $imported 'asset-manifest.json') -Raw|ConvertFrom-Json
$count=0
foreach($entry in $manifest){
 $path=if($entry.path.StartsWith('Assets/')){Join-Path $project $entry.path}else{Join-Path $imported $entry.path}
 if(!(Test-Path -LiteralPath $path)){throw "Missing asset: $path"}
 if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256){throw "SHA256 mismatch: $path"}
 if((Get-Item -LiteralPath $path).Length -ne $entry.bytes){throw "Size mismatch: $path"}
 $count++
}
Write-Output "PASS: $count downloaded/derived files match manifest SHA256 and sizes."
