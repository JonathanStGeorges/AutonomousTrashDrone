$ErrorActionPreference='Stop'
$headers=@{'User-Agent'='TrashSailDemo/2.0 (robotics research asset integration; https://github.com/MARUSimulator/marus2-example)'}
$root=Join-Path $PSScriptRoot '../../Assets/TrashSailDemo/Imported'
$manifest=@()
foreach($key in @('plastic_bottle_gallon','multi_cleaner_bottle','cleaner_tin_01','trashbag')) {
 $dir=Join-Path $root $key
 New-Item -ItemType Directory -Force $dir | Out-Null
 $files=Invoke-RestMethod "https://api.polyhaven.com/files/$key" -Headers $headers
 $info=Invoke-RestMethod "https://api.polyhaven.com/info/$key" -Headers $headers
 $files|ConvertTo-Json -Depth 30|Set-Content (Join-Path $dir 'source-files.json')
 $info|ConvertTo-Json -Depth 20|Set-Content (Join-Path $dir 'source-info.json')
 $fbx=$files.fbx.'1k'.fbx
 if(!$fbx){throw "No 1k FBX for $key"}
 $entries=@([pscustomobject]@{path="$key.fbx";file=$fbx})
 foreach($include in $fbx.include.PSObject.Properties){$entries += [pscustomobject]@{path=$include.Name;file=$include.Value}}
 foreach($entry in $entries){
  $dest=Join-Path $dir $entry.path
  New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
  if(!(Test-Path $dest)){Invoke-WebRequest $entry.file.url -OutFile $dest -Headers $headers -UseBasicParsing}
  if((Get-FileHash $dest -Algorithm MD5).Hash.ToLower() -ne $entry.file.md5){throw "Checksum failure $dest"}
  $manifest += [pscustomobject]@{asset=$key;path="$key/$($entry.path)";source=$entry.file.url;creator=($info.authors.PSObject.Properties.Name -join ', ');license='CC0-1.0';sha256=(Get-FileHash $dest -Algorithm SHA256).Hash;bytes=(Get-Item $dest).Length;sourceDimensionsMillimetres=$info.dimensions}
 }
 Write-Output "Downloaded and checksum verified: $key"
}
$fish=Join-Path $root 'BarramundiFish';New-Item -ItemType Directory -Force $fish|Out-Null
$url='https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models/BarramundiFish/glTF-Binary/BarramundiFish.glb'
Invoke-WebRequest $url -OutFile (Join-Path $fish 'BarramundiFish.glb') -Headers $headers -UseBasicParsing
Invoke-WebRequest 'https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models/BarramundiFish/README.md' -OutFile (Join-Path $fish 'SOURCE.md') -Headers $headers -UseBasicParsing
$manifest += [pscustomobject]@{asset='BarramundiFish';path='BarramundiFish/BarramundiFish.glb';source=$url;creator='Microsoft';license='CC0-1.0';sha256=(Get-FileHash (Join-Path $fish 'BarramundiFish.glb') -Algorithm SHA256).Hash;bytes=(Get-Item (Join-Path $fish 'BarramundiFish.glb')).Length}
$manifest|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'asset-manifest.json')
Write-Output 'Finished licensed asset download.'
