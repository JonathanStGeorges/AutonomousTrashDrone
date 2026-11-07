param(
    [ValidateSet('status','baseline','preserve','setup','tests','play','stop','suite','build')][string]$Command='status',
    [string]$Scenarios='',
    [int]$TimeoutSeconds=1800
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class TrashSailMenu {
 [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr w);
 [DllImport("user32.dll")] public static extern IntPtr GetSubMenu(IntPtr m,int p);
 [DllImport("user32.dll")] public static extern int GetMenuItemCount(IntPtr m);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetMenuString(IntPtr m,uint id,StringBuilder s,int n,uint f);
 [DllImport("user32.dll")] public static extern uint GetMenuItemID(IntPtr m,int p);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr w,uint m,IntPtr p,IntPtr l);
}
'@
$editor=Get-Process -Name Unity | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -like '*marus2-example*' } | Select-Object -First 1
if(-not $editor){throw 'Open the marus2-example project in Unity6000.5.11f1 first.'}
# Discover and invoke the actual Assets/Refresh command; no foreground keyboard injection.
$menu=[TrashSailMenu]::GetMenu($editor.MainWindowHandle)
for($i=0;$i -lt [TrashSailMenu]::GetMenuItemCount($menu);$i++){
    $label=New-Object Text.StringBuilder 512
    [TrashSailMenu]::GetMenuString($menu,$i,$label,512,0x400)|Out-Null
    if($label.ToString() -notmatch 'Assets'){continue}
    $submenu=[TrashSailMenu]::GetSubMenu($menu,$i)
    for($j=0;$j -lt [TrashSailMenu]::GetMenuItemCount($submenu);$j++){
        $child=New-Object Text.StringBuilder 512
        [TrashSailMenu]::GetMenuString($submenu,$j,$child,512,0x400)|Out-Null
        if($child.ToString() -match '^Refresh'){
            $commandId=[TrashSailMenu]::GetMenuItemID($submenu,$j)
            if(-not [TrashSailMenu]::PostMessage($editor.MainWindowHandle,0x111,[IntPtr]$commandId,[IntPtr]::Zero)){throw 'Unity refresh dispatch failed.'}
        }
    }
}
$requestId=[guid]::NewGuid().ToString('N')
$request=@{command=$Command;argument=$Scenarios;id=$requestId}|ConvertTo-Json -Compress
$requestPath=Join-Path $projectRoot 'Temp\TrashSailDemo.command.json'
if(Test-Path -LiteralPath $requestPath){throw 'A Unity operation is already queued; wait for it to finish.'}
[IO.File]::WriteAllText($requestPath,$request)
$responsePath=Join-Path $projectRoot ('Evidence\TrashSailDemo\editor-response-'+$Command+'.json')
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while([DateTime]::UtcNow -lt $deadline){
    if(Test-Path -LiteralPath $responsePath){
        try{$response=Get-Content -LiteralPath $responsePath -Raw|ConvertFrom-Json}catch{$response=$null}
        if($response -and $response.id -eq $requestId -and $response.status -ne 'RUNNING'){
            $response|ConvertTo-Json -Depth 6
            if($response.status -ne 'PASS'){exit 1};exit 0
        }
    }
    Start-Sleep -Milliseconds 300
}
throw "Unity command '$Command' did not complete within $TimeoutSeconds seconds. Inspect the Editor console and evidence response."
