$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$items=Join-Path $PSScriptRoot 'Items'
Add-Type -AssemblyName System.Drawing
$references=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll'|ForEach-Object FullName)
$references+=Join-Path $PSHOME 'ref/System.Collections.dll'
$original=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'export-v2-art.ps1') -Raw
$code=[regex]::Match($original,"(?s)-TypeDefinition @'\r?\n(.*?)\r?\n'@").Groups[1].Value
if(-not('V2SpriteSlicer' -as [type])){Add-Type -ReferencedAssemblies $references -TypeDefinition $code}
$records=[ordered]@{}
foreach($record in (Get-Content -LiteralPath (Join-Path $project 'Design/v2-art-exports.json') -Raw|ConvertFrom-Json)){$records[$record.key]=$record}
$bitmap=[Drawing.Bitmap]::new((Join-Path $project 'ArtOriginals/V2-props.png'))
try
{
    $boxes=[V2SpriteSlicer]::Row($bitmap,3,5,4)
    for($index=2;$index -lt 4;$index++)
    {
        $key=@('bait','premium_bait')[$index-2];$box=$boxes[$index]
        [V2SpriteSlicer]::Export($bitmap,$box,(Join-Path $items ($key+'.png')),256,256)
        $records[$key]=[ordered]@{key=$key;width=2;height=2;source_box=@($box.X,$box.Y,$box.Width,$box.Height);source='V2-props.png';alpha_preserved=$true;scaling='uniform nearest-neighbor; transparent padding'}
    }
}
finally{$bitmap.Dispose()}
[IO.File]::WriteAllText((Join-Path $project 'Design/v2-art-exports.json'),(@($records.Values)|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
Write-Output 'Re-exported both original bait sprites at 2x2 cells (256x256), preserving their aspect ratio and alpha.'
