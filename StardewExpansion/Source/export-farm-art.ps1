param([switch]$SkipMissing)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$drawingReferences=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll' | ForEach-Object {$_.FullName})
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class FarmSpriteExport {
  public static string Export(string source,string target,int columns,int rows,int index,int width,int height) {
    using(var image=new Bitmap(source)) {
      int col=index%columns,row=index/columns;
      int left=image.Width*col/columns,top=image.Height*row/rows;
      int right=image.Width*(col+1)/columns,bottom=image.Height*(row+1)/rows;
      int minX=right,minY=bottom,maxX=left,maxY=top;
      for(int y=top;y<bottom;y++) for(int x=left;x<right;x++) if(image.GetPixel(x,y).A>16) {
        minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
      }
      if(minX>maxX || minY>maxY) throw new Exception("Empty sprite: "+target);
      if(minX<=left+1 || minY<=top+1 || maxX>=right-2 || maxY>=bottom-2) throw new Exception("Sprite touches its atlas-cell boundary: "+target);
      var clip=Rectangle.FromLTRB(Math.Max(left,minX-2),Math.Max(top,minY-2),Math.Min(right,maxX+3),Math.Min(bottom,maxY+3));
      using(var output=new Bitmap(width,height,PixelFormat.Format32bppArgb)) using(var graphics=Graphics.FromImage(output)) {
        graphics.Clear(Color.Transparent);graphics.CompositingMode=CompositingMode.SourceCopy;
        graphics.InterpolationMode=InterpolationMode.NearestNeighbor;graphics.PixelOffsetMode=PixelOffsetMode.Half;
        double scale=Math.Min((width-8.0)/clip.Width,(height-8.0)/clip.Height);
        int w=(int)Math.Round(clip.Width*scale),h=(int)Math.Round(clip.Height*scale);
        graphics.DrawImage(image,new Rectangle((width-w)/2,(height-h)/2,w,h),clip,GraphicsUnit.Pixel);
        output.Save(target,ImageFormat.Png);
        return clip.X+","+clip.Y+","+clip.Width+","+clip.Height;
      }
    }
  }
}
'@
$artRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../ArtOriginals/Husbandry'))
$items=Join-Path $PSScriptRoot 'Items'
$specs=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../ArtOriginals/HUSBANDRY_1.1.0_EXPORT.json') -Raw | ConvertFrom-Json
$results=@(foreach($spec in $specs){
  $source=Join-Path $artRoot $spec.source
  if(-not (Test-Path -LiteralPath $source)){if($SkipMissing){continue}else{throw ('Missing source atlas: '+$source)}}
  $target=Join-Path $items ($spec.key+'.png')
  $bounds=[FarmSpriteExport]::Export($source,$target,$spec.columns,$spec.rows,$spec.index,$spec.width,$spec.height)
  [pscustomobject]@{key=$spec.key;width=$spec.width;height=$spec.height;source=$spec.source;source_clip=$bounds;bytes=(Get-Item -LiteralPath $target).Length;sha256=(Get-FileHash -LiteralPath $target).Hash}
})
[IO.File]::WriteAllText((Join-Path $PSScriptRoot '../Design/farm-art-exports.json'),($results | ConvertTo-Json -Depth 4)+"`n",[Text.UTF8Encoding]::new($false))
Write-Output ('Exported '+$results.Count+' transparent husbandry sprites, preserving aspect ratio and 128 texture pixels per grid cell.')
