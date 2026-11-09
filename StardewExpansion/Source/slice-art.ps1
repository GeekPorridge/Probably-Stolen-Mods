$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$drawingReferences=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll' | ForEach-Object {$_.FullName})
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class PelicanArtSlicer {
  public static Rectangle Bounds(Bitmap source,Rectangle cell) {
    int minX=cell.Right,minY=cell.Bottom,maxX=-1,maxY=-1;
    for(int y=cell.Top;y<cell.Bottom;y++) for(int x=cell.Left;x<cell.Right;x++) {
      // Measure the visible subject, excluding barely visible export-alpha dust.
      // Pixels inside the resulting crop retain their generated RGBA values.
      if(source.GetPixel(x,y).A<128) continue;
      minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
    }
    return maxX<0?cell:Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);
  }
  public static void Save(Bitmap source,Rectangle crop,int width,int height,string destination,int padding=1,int subjectWidth=0,int subjectHeight=0) {
    using(var target=new Bitmap(width,height,PixelFormat.Format32bppArgb)) using(var graphics=Graphics.FromImage(target)) {
      graphics.Clear(Color.Transparent);
      graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
      graphics.PixelOffsetMode=PixelOffsetMode.Half;
      double availableWidth=width-2.0*padding,availableHeight=height-2.0*padding;
      if(subjectWidth>0) availableWidth=Math.Min(availableWidth,subjectWidth);
      if(subjectHeight>0) availableHeight=Math.Min(availableHeight,subjectHeight);
      double ratio=Math.Min(availableWidth/crop.Width,availableHeight/crop.Height);
      int w=Math.Max(1,(int)Math.Round(crop.Width*ratio)),h=Math.Max(1,(int)Math.Round(crop.Height*ratio));
      graphics.DrawImage(source,new Rectangle((width-w)/2,(height-h)/2,w,h),crop,GraphicsUnit.Pixel);
      target.Save(destination,ImageFormat.Png);
    }
  }
}
'@
$itemsDirectory=Join-Path $PSScriptRoot 'Items'
New-Item -ItemType Directory -Path $itemsDirectory -Force | Out-Null
$artDirectory=Join-Path $PSScriptRoot '../ArtOriginals'
$cropNames=@('parsnip','potato','corn','wheat','hops','strawberry','starfruit','ancient_fruit')
$cropBitmap=[Drawing.Bitmap]::new((Join-Path $artDirectory 'crops-clean.png'))
$seedBitmap=[Drawing.Bitmap]::new((Join-Path $artDirectory 'seeds-clean.png'))
try {
  for($i=0;$i -lt $cropNames.Count;$i++){
    $column=$i%4;$row=[int][Math]::Floor($i/4)
    $left=[int][Math]::Round($column*$cropBitmap.Width/4.0);$right=[int][Math]::Round(($column+1)*$cropBitmap.Width/4.0)
    $top=[int][Math]::Round($row*$cropBitmap.Height/2.0);$bottom=[int][Math]::Round(($row+1)*$cropBitmap.Height/2.0)
    $cell=[Drawing.Rectangle]::FromLTRB($left,$top,$right,$bottom)
    $cropBounds=[PelicanArtSlicer]::Bounds($cropBitmap,$cell)
    # The two atlases need not have the same pixel dimensions.
    $seedCell=[Drawing.Rectangle]::FromLTRB(
      [int][Math]::Round($column*$seedBitmap.Width/4.0),[int][Math]::Round($row*$seedBitmap.Height/2.0),
      [int][Math]::Round(($column+1)*$seedBitmap.Width/4.0),[int][Math]::Round(($row+1)*$seedBitmap.Height/2.0))
    $seedBounds=[PelicanArtSlicer]::Bounds($seedBitmap,$seedCell)
    $cropName=$cropNames[$i]
    $fixedSize=$cropName -in @('parsnip','wheat','hops')
    $normalWidth=if($fixedSize){128}else{256}
    if($fixedSize){[PelicanArtSlicer]::Save($cropBitmap,$cropBounds,$normalWidth,256,(Join-Path $itemsDirectory ($cropName+'.png')),8)}
    else{[PelicanArtSlicer]::Save($cropBitmap,$cropBounds,256,256,(Join-Path $itemsDirectory ($cropName+'.png')),8,200,200)}
    [PelicanArtSlicer]::Save($seedBitmap,$seedBounds,128,128,(Join-Path $itemsDirectory ($cropName+'_seed.png')),8)
    if(-not $fixedSize){
      [PelicanArtSlicer]::Save($cropBitmap,$cropBounds,128,128,(Join-Path $itemsDirectory ($cropName+'_small.png')),8,112,112)
      [PelicanArtSlicer]::Save($cropBitmap,$cropBounds,384,384,(Join-Path $itemsDirectory ($cropName+'_huge.png')),8,352,352)
    }
  }
} finally {$cropBitmap.Dispose();$seedBitmap.Dispose()}
$farmerBitmap=[Drawing.Bitmap]::new((Join-Path $artDirectory 'farmer-front-012.png'))
try {
  $bounds=[PelicanArtSlicer]::Bounds($farmerBitmap,[Drawing.Rectangle]::new(0,0,$farmerBitmap.Width,$farmerBitmap.Height))
  [PelicanArtSlicer]::Save($farmerBitmap,$bounds,128,160,(Join-Path $itemsDirectory 'farmer.png'),2)
} finally {$farmerBitmap.Dispose()}
foreach($wineKey in @('beer','pale_ale','strawberry_wine','starfruit_wine','ancient_wine')){
  $legacyMarker=Join-Path $itemsDirectory ('mark_'+$wineKey+'.png')
  if(Test-Path -LiteralPath $legacyMarker){Remove-Item -LiteralPath $legacyMarker}
}
foreach($sheet in @(
  @{File='fruit-bottles-012.png';Columns=3;Keys=@('strawberry_wine','starfruit_wine')},
  @{File='beer-bottles-012.png';Columns=2;Keys=@('beer','pale_ale')},
  @{File='ancient-wine-light-blue-100.png';Columns=1;Keys=@('ancient_wine')}
)){
  $bitmap=[Drawing.Bitmap]::new((Join-Path $artDirectory $sheet.File))
  try {
    for($i=0;$i -lt $sheet.Keys.Count;$i++){
      $left=[int][Math]::Round($i*$bitmap.Width/[double]$sheet.Columns)
      $right=[int][Math]::Round(($i+1)*$bitmap.Width/[double]$sheet.Columns)
      $cell=[Drawing.Rectangle]::FromLTRB($left,0,$right,$bitmap.Height)
      $bounds=[PelicanArtSlicer]::Bounds($bitmap,$cell)
      # Uniform scaling only: retain the source's slender / squat silhouettes.
      # Six-cell beer canvas; fruit wines retain their four-cell canvas.
      $isBeer=$sheet.Keys[$i] -in @('beer','pale_ale')
      $bottleWidth=if($isBeer){256}else{128};$bottleHeight=if($isBeer){384}else{512}
      [PelicanArtSlicer]::Save($bitmap,$bounds,$bottleWidth,$bottleHeight,(Join-Path $itemsDirectory ('bottle_'+$sheet.Keys[$i]+'.png')),8)
    }
  } finally {$bitmap.Dispose()}
}
Write-Output ('Prepared '+@(Get-ChildItem -LiteralPath $itemsDirectory -Filter '*.png').Count+' PNG sprites.')
