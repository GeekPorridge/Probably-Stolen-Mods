$ErrorActionPreference='Stop'
$r3Project=Split-Path $PSScriptRoot
$r3Items=Join-Path $PSScriptRoot 'Items'
$r3Art=Join-Path $r3Project 'ArtOriginals'
Add-Type -AssemblyName System.Drawing
$r3References=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll'|ForEach-Object FullName)
$r3References+=Join-Path $PSHOME 'ref/System.Collections.dll'
$r3Original=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'export-v2-art.ps1') -Raw
$r3Class=[regex]::Match($r3Original,"(?s)-TypeDefinition @'\r?\n(.*?)\r?\n'@").Groups[1].Value
Add-Type -ReferencedAssemblies $r3References -TypeDefinition $r3Class
$r3Records=[ordered]@{}
foreach($record in (Get-Content -LiteralPath (Join-Path $r3Project 'Design/v2-art-exports.json') -Raw|ConvertFrom-Json)){$r3Records[$record.key]=$record}
function R3-Export($bitmap,$box,[string]$key,[int]$w,[int]$h,[string]$source){
    [V2SpriteSlicer]::Export($bitmap,$box,(Join-Path $r3Items ($key+'.png')),($w*128),($h*128))
    $r3Records[$key]=[ordered]@{key=$key;width=$w;height=$h;source_box=@($box.X,$box.Y,$box.Width,$box.Height);source=$source}
}
$bitmap=[Drawing.Bitmap]::new((Join-Path $r3Art 'V2-kings.png'))
try{$boxes=[V2SpriteSlicer]::Row($bitmap,0,1,4);$keys=@('legend','crimsonfish','glacierfish','mutant_carp');for($n=0;$n -lt 4;$n++){R3-Export $bitmap $boxes[$n] $keys[$n] 4 3 'V2-kings.png'}}finally{$bitmap.Dispose()}
$bitmap=[Drawing.Bitmap]::new((Join-Path $r3Art 'R3-chili-spirit.png'))
try{$box=[V2SpriteSlicer]::Row($bitmap,0,1,1)[0];R3-Export $bitmap $box 'bottle_chili_spirit' 2 3 'R3-chili-spirit.png'}finally{$bitmap.Dispose()}
[IO.File]::WriteAllText((Join-Path $r3Project 'Design/v2-art-exports.json'),(@($r3Records.Values)|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
Write-Output 'Restored original four kings at 4x3 cells; exported broad clear spirit bottle at 2x3 cells.'
