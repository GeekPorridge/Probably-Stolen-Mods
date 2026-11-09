$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$art=Join-Path $project 'ArtOriginals'
$items=Join-Path $PSScriptRoot 'Items'
Add-Type -AssemblyName System.Drawing
$references=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll'|ForEach-Object FullName)
$references+=Join-Path $PSHOME 'ref/System.Collections.dll'
$original=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'export-v2-art.ps1') -Raw
$code=[regex]::Match($original,"(?s)-TypeDefinition @'\r?\n(.*?)\r?\n'@").Groups[1].Value
if(-not('V2SpriteSlicer' -as [type])){Add-Type -ReferencedAssemblies $references -TypeDefinition $code}
$records=[ordered]@{}
foreach($record in (Get-Content -LiteralPath (Join-Path $project 'Design/v2-art-exports.json') -Raw|ConvertFrom-Json)){$records[$record.key]=$record}
foreach($key in @('baked_lobster','scallion_sea_cucumber','sashimi_platter'))
{
    $source='R5-'+$key+'.png'
    $bitmap=[Drawing.Bitmap]::new((Join-Path $art $source))
    try
    {
        foreach($point in @(@(0,0),@(($bitmap.Width-1),0),@(0,($bitmap.Height-1)),@(($bitmap.Width-1),($bitmap.Height-1))))
        {if($bitmap.GetPixel($point[0],$point[1]).A -ne 0){throw ('Non-transparent source corner: '+$key)}}
        $box=[V2SpriteSlicer]::Row($bitmap,0,1,1)[0]
        $target=Join-Path $items ($key+'.png')
        [V2SpriteSlicer]::Export($bitmap,$box,$target,384,256)
        $records[$key]=[ordered]@{key=$key;width=3;height=2;source_box=@($box.X,$box.Y,$box.Width,$box.Height);source=$source;alpha_preserved=$true;scaling='uniform nearest-neighbor; transparent padding'}
        $output=[Drawing.Bitmap]::new($target)
        try
        {
            if($output.Width -ne 384 -or $output.Height -ne 256){throw ('Incorrect output dimensions: '+$key)}
            foreach($point in @(@(0,0),@(383,0),@(0,255),@(383,255)))
            {if($output.GetPixel($point[0],$point[1]).A -ne 0){throw ('Non-transparent output corner: '+$key)}}
        }
        finally{$output.Dispose()}
    }
    finally{$bitmap.Dispose()}
}
[IO.File]::WriteAllText((Join-Path $project 'Design/v2-art-exports.json'),(@($records.Values)|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
Write-Output 'Exported baked lobster, scallion sea cucumber and sashimi platter at 3x2 cells (384x256), preserving aspect ratio and alpha.'
