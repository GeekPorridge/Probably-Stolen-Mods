$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$art=Join-Path $project 'ArtOriginals'
$items=Join-Path $PSScriptRoot 'Items'
Add-Type -AssemblyName System.Drawing
$refs=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll'|ForEach-Object FullName)
$refs+=Join-Path $PSHOME 'ref/System.Collections.dll'
$original=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'export-v2-art.ps1') -Raw
$code=[regex]::Match($original,"(?s)-TypeDefinition @'\r?\n(.*?)\r?\n'@").Groups[1].Value
Add-Type -ReferencedAssemblies $refs -TypeDefinition $code
$records=[ordered]@{}
foreach($record in (Get-Content -LiteralPath (Join-Path $project 'Design/v2-art-exports.json') -Raw|ConvertFrom-Json)){$records[$record.key]=$record}
$b=[Drawing.Bitmap]::new((Join-Path $art 'R4-fruit-wines.png'))
try{
    $boxes=[V2SpriteSlicer]::Row($b,0,1,4)
    $scale=[single][Math]::Min(114/($boxes.Width|Measure-Object -Maximum).Maximum,440/($boxes.Height|Measure-Object -Maximum).Maximum)
    $keys=@('ancient_wine','strawberry_wine','starfruit_wine','blueberry_wine')
    $colors=@('#0F7281','#DB171F','#E4C51F','#4C5DB6')
    for($n=0;$n -lt 4;$n++){
        $key='bottle_'+$keys[$n];[V2SpriteSlicer]::Export($b,$boxes[$n],(Join-Path $items ($key+'.png')),128,512,$scale)
        $box=$boxes[$n];$records[$key]=[ordered]@{key=$key;width=1;height=4;source_box=@($box.X,$box.Y,$box.Width,$box.Height);source='R4-fruit-wines.png';liquid_base_color=$colors[$n]}
    }
}finally{$b.Dispose()}
$b=[Drawing.Bitmap]::new((Join-Path $art 'R4-artisan.png'))
try{$box=[V2SpriteSlicer]::Row($b,0,1,1)[0];[V2SpriteSlicer]::Export($b,$box,(Join-Path $items 'farmer_artisan.png'),128,160)}finally{$b.Dispose()}
[IO.File]::WriteAllText((Join-Path $project 'Design/v2-art-exports.json'),(@($records.Values)|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
Write-Output 'Exported four matching 1x4 fruit-wine bottles and a 128x160 artisan portrait.'
