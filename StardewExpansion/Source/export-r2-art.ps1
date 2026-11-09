$ErrorActionPreference='Stop'
$r2Project=Split-Path $PSScriptRoot
$r2Items=Join-Path $PSScriptRoot 'Items'
$r2Art=Join-Path $r2Project 'ArtOriginals'
Add-Type -AssemblyName System.Drawing
$r2References=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll'|ForEach-Object FullName)
$r2References+=Join-Path $PSHOME 'ref/System.Collections.dll'
$r2Original=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'export-v2-art.ps1') -Raw
$r2Class=[regex]::Match($r2Original,"(?s)-TypeDefinition @'\r?\n(.*?)\r?\n'@").Groups[1].Value
if([string]::IsNullOrWhiteSpace($r2Class)){throw 'Missing alpha-component atlas exporter'}
Add-Type -ReferencedAssemblies $r2References -TypeDefinition $r2Class
Add-Type -ReferencedAssemblies $r2References -TypeDefinition @'
using System;using System.Drawing;using System.Drawing.Drawing2D;
public static class R2Overlay {
 public static void Draw(Bitmap source,Rectangle box,string path){
  using(var output=new Bitmap(384,512))using(var g=Graphics.FromImage(output)){
   g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
   // Native hydroponic and nutrifruit_p sprites are both 48x64 @100 PPU.
   // Keep a full matching 48x64 canvas; the plant roots meet the base at y43.
   float scale=Math.Min(352f/box.Width,280f/box.Height);int w=(int)Math.Round(box.Width*scale),h=(int)Math.Round(box.Height*scale);
   g.DrawImage(source,new Rectangle((384-w)/2,344-h,w,h),box,GraphicsUnit.Pixel);output.Save(path,System.Drawing.Imaging.ImageFormat.Png);
  }
 }
}
'@
$r2Records=[ordered]@{}
foreach($record in (Get-Content -LiteralPath (Join-Path $r2Project 'Design/v2-art-exports.json') -Raw | ConvertFrom-Json)){
    if($record.key -notmatch '^growth_.*_[01]$'){$r2Records[$record.key]=$record}
}
function R2-Record([string]$key,[int]$w,[int]$h,$rect,[string]$source){$r2Records[$key]=[ordered]@{key=$key;width=$w;height=$h;source_box=@($rect.X,$rect.Y,$rect.Width,$rect.Height);source=$source}}
function R2-Baseline([string]$key){$dest=Join-Path $r2Art ('R2-base-'+$key+'.png');if(-not(Test-Path -LiteralPath $dest)){Copy-Item -LiteralPath (Join-Path $r2Items ($key+'.png')) -Destination $dest};return [Drawing.Bitmap]::new($dest)}
foreach($key in @('parsnip','potato','corn','wheat','hops','strawberry','starfruit','ancient_fruit','chili','blueberry','poppy','fairy_rose')){
    $sprite='growth_'+$key+'_2';$bitmap=R2-Baseline $sprite
    try{$box=[V2SpriteSlicer]::Row($bitmap,0,1,1)[0];[R2Overlay]::Draw($bitmap,$box,(Join-Path $r2Items ($sprite+'.png')));R2-Record $sprite 3 4 $box ('R2-base-'+$sprite+'.png')}finally{$bitmap.Dispose()}
    foreach($oldStage in @(0,1)){$obsolete=[IO.Path]::GetFullPath((Join-Path $r2Items ('growth_'+$key+'_'+$oldStage+'.png')));if(-not $obsolete.StartsWith([IO.Path]::GetFullPath($r2Items)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Sprite target escapes Items'};if(Test-Path -LiteralPath $obsolete){Remove-Item -LiteralPath $obsolete}}
}
foreach($key in @('poppy_perfume','fairy_perfume','cheese','goat_cheese','truffle_oil','wheat','poppy','fairy_rose','blueberry')){
    $bitmap=R2-Baseline $key
    try{$box=[V2SpriteSlicer]::Row($bitmap,0,1,1)[0];[V2SpriteSlicer]::Export($bitmap,$box,(Join-Path $r2Items ($key+'.png')),256,256);R2-Record $key 2 2 $box ('R2-base-'+$key+'.png')
        if($key -eq 'blueberry'){foreach($suffix in @('small','huge')){$size=if($suffix -eq 'small'){1}else{3};[V2SpriteSlicer]::Export($bitmap,$box,(Join-Path $r2Items ($key+'_'+$suffix+'.png')),($size*128),($size*128));R2-Record ($key+'_'+$suffix) $size $size $box ('R2-base-'+$key+'.png')}}
    }finally{$bitmap.Dispose()}
}
$r2SeedKeys=@('parsnip','potato','corn','wheat','hops','strawberry','starfruit','ancient_fruit')
$bitmap=[Drawing.Bitmap]::new((Join-Path $r2Art 'R2-seed-packets.png'))
try{for($r=0;$r -lt 2;$r++){$boxes=[V2SpriteSlicer]::Row($bitmap,$r,2,4);for($c=0;$c -lt 4;$c++){$key=$r2SeedKeys[$r*4+$c]+'_seed';[V2SpriteSlicer]::Export($bitmap,$boxes[$c],(Join-Path $r2Items ($key+'.png')),128,128);R2-Record $key 1 1 $boxes[$c] 'R2-seed-packets.png'}}}finally{$bitmap.Dispose()}
$bitmap=[Drawing.Bitmap]::new((Join-Path $r2Art 'R2-kings.png'))
try{$boxes=[V2SpriteSlicer]::Row($bitmap,0,1,4);$keys=@('legend','crimsonfish','glacierfish','mutant_carp');for($c=0;$c -lt 4;$c++){[V2SpriteSlicer]::Export($bitmap,$boxes[$c],(Join-Path $r2Items ($keys[$c]+'.png')),512,256);R2-Record $keys[$c] 4 2 $boxes[$c] 'R2-kings.png'}}finally{$bitmap.Dispose()}
[IO.File]::WriteAllText((Join-Path $r2Project 'Design/v2-art-exports.json'),(@($r2Records.Values)|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
Write-Output ('Exported '+$r2Records.Count+' expansion/revised sprite records; '+@(Get-ChildItem -LiteralPath $r2Items -Filter '*.png' -File).Count+' runtime sprites, 12 mature hydro overlays at 384x512.')
