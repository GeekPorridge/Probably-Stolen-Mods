$ErrorActionPreference='Stop'
$project=if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'PelicanCultivation.csproj')){Split-Path $PSScriptRoot}else{Join-Path $PSScriptRoot 'StardewExpansion-v2.0'}
$generated='C:/Users/pc/.codex/generated_images/01a1b991-4ded-7641-adff-390f1618db21'
$art=Join-Path $project 'ArtOriginals'
$items=Join-Path $project 'Source/Items'
Add-Type -AssemblyName System.Drawing
$drawingReferences=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll'|ForEach-Object FullName)
$drawingReferences+=Join-Path $PSHOME 'ref/System.Collections.dll'
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
public static class V2SpriteSlicer {
 private sealed class Subject {public Rectangle Rect;public int Area;}
 private static readonly Dictionary<Bitmap,List<Subject>> subjects=new Dictionary<Bitmap,List<Subject>>();
 private static List<Subject> Subjects(Bitmap b) {
  if(subjects.TryGetValue(b,out var cached))return cached;
  int width=b.Width,height=b.Height;var solid=new bool[width*height];var seen=new bool[solid.Length];var result=new List<Subject>();
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)solid[y*width+x]=b.GetPixel(x,y).A>=180;
  var queue=new Queue<int>();
  for(int at=0;at<solid.Length;at++){
   if(!solid[at]||seen[at])continue;int minX=width,minY=height,maxX=0,maxY=0,area=0;seen[at]=true;queue.Enqueue(at);
   while(queue.Count>0){int p=queue.Dequeue(),x=p%width,y=p/width;area++;minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=width||ny>=height)continue;int n=ny*width+nx;if(!seen[n]&&solid[n]){seen[n]=true;queue.Enqueue(n);}}
   }
   if(area>=100)result.Add(new Subject{Area=area,Rect=Rectangle.FromLTRB(Math.Max(0,minX-2),Math.Max(0,minY-2),Math.Min(width,maxX+3),Math.Min(height,maxY+3))});
  }
  subjects[b]=result;return result;
 }
 public static Rectangle[] Row(Bitmap b,int row,int rows,int count) {
  var selected=Subjects(b).FindAll(s=>(s.Rect.Top+s.Rect.Bottom)/2>=b.Height*row/rows&&(s.Rect.Top+s.Rect.Bottom)/2<b.Height*(row+1)/rows);
  selected.Sort((a,c)=>c.Area.CompareTo(a.Area));if(selected.Count<count)throw new Exception("Expected "+count+" subjects in row "+row+", found "+selected.Count);
  selected=selected.GetRange(0,count);selected.Sort((a,c)=>a.Rect.Left.CompareTo(c.Rect.Left));return selected.ConvertAll(s=>s.Rect).ToArray();
 }
 public static void Export(Bitmap b,Rectangle rect,string path,int width,int height,float sharedScale=0,bool bottom=false) {
  using(var output=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(output)) {
   g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
   float scale=sharedScale>0?sharedScale:Math.Min((width-12f)/rect.Width,(height-12f)/rect.Height);int w=Math.Max(1,(int)Math.Round(rect.Width*scale)),h=Math.Max(1,(int)Math.Round(rect.Height*scale));
   g.DrawImage(b,new Rectangle((width-w)/2,bottom?height-h-6:(height-h)/2,w,h),rect,GraphicsUnit.Pixel);output.Save(path,System.Drawing.Imaging.ImageFormat.Png);
  }
 }
}
'@
$records=[Collections.Generic.List[object]]::new()
function Source([string]$id,[string]$file){$dest=Join-Path $art ('V2-'+$id+'.png');if(-not(Test-Path -LiteralPath $dest)){Copy-Item -LiteralPath (Join-Path $generated $file) -Destination $dest -Force};return [Drawing.Bitmap]::new($dest)}
function Emit($bitmap,$rect,[string]$key,[int]$width,[int]$height,[single]$scale=0,[bool]$bottom=$false){
    [V2SpriteSlicer]::Export($bitmap,$rect,(Join-Path $items ($key+'.png')),$width*128,$height*128,$scale,$bottom)
    $records.Add([ordered]@{key=$key;width=$width;height=$height;source_box=@($rect.X,$rect.Y,$rect.Width,$rect.Height)})
}
$b=Source 'new-crops' 'exec-cbdbe2f0-6fab-4742-8b87-05f89bd77bfc.png'
try{for($r=0;$r -lt 4;$r++){$crop=@('chili','blueberry','poppy','fairy_rose')[$r];$boxes=[V2SpriteSlicer]::Row($b,$r,4,5);Emit $b $boxes[0] ($crop+'_seed') 1 1;Emit $b $boxes[1] $crop 1 2;$scale=[single][Math]::Min(244/($boxes[2..4].Width|Measure-Object -Maximum).Maximum,244/($boxes[2..4].Height|Measure-Object -Maximum).Maximum);for($c=0;$c -lt 3;$c++){Emit $b $boxes[$c+2] ('growth_'+$crop+'_'+$c) 2 2 $scale $true}}}finally{$b.Dispose()}
$sheets=@(@{id='growth-basic';file='exec-9b912306-a7d9-40e1-8891-12cd24665fe5.png';keys=@('parsnip','potato','corn','wheat')},@{id='growth-fruit';file='exec-058013ab-1a3a-47aa-817f-88e24a8cc875.png';keys=@('hops','strawberry','starfruit','ancient_fruit')})
foreach($sheet in $sheets){$b=Source $sheet.id $sheet.file;try{for($r=0;$r -lt 4;$r++){$boxes=[V2SpriteSlicer]::Row($b,$r,4,3);$scale=[single][Math]::Min(244/($boxes.Width|Measure-Object -Maximum).Maximum,244/($boxes.Height|Measure-Object -Maximum).Maximum);for($c=0;$c -lt 3;$c++){Emit $b $boxes[$c] ('growth_'+$sheet.keys[$r]+'_'+$c) 2 2 $scale $true}}}finally{$b.Dispose()}}
$b=Source 'animals' 'exec-cb3a93da-6521-4a8e-a861-c2e4d1203c47.png'
try{for($r=0;$r -lt 2;$r++){$prefix=@('chicken','goat')[$r];$boxes=[V2SpriteSlicer]::Row($b,$r,2,4);for($c=0;$c -lt 4;$c++){$key=$prefix+'_'+@('baby','young','male','female')[$c];$w=if($c -eq 0){1}elseif($c -eq 1 -or $r -eq 0){2}else{3};$h=if($c -lt 2){1}else{2};Emit $b $boxes[$c] $key $w $h}}}finally{$b.Dispose()}
$b=Source 'fish' 'exec-33008b69-9091-47be-98dd-6746b09f0705.png'
try{for($r=0;$r -lt 4;$r++){$prefix=@('midnight_carp','largemouth_bass','tuna','lava_eel')[$r];$boxes=[V2SpriteSlicer]::Row($b,$r,4,5);for($c=0;$c -lt 5;$c++){$w=@(1,1,2,3,4)[$c];$h=if($c -lt 3){1}else{2};Emit $b $boxes[$c] ($prefix+'_'+@('roe','fry','small','medium','large')[$c]) $w $h}}}finally{$b.Dispose()}
$b=Source 'kings' 'exec-98ed3dc0-7540-48d7-ab8a-8b0d71fc359a.png'
try{$boxes=[V2SpriteSlicer]::Row($b,0,1,4);for($c=0;$c -lt 4;$c++){Emit $b $boxes[$c] @('legend','crimsonfish','glacierfish','mutant_carp')[$c] 4 2}}finally{$b.Dispose()}
$b=Source 'props' 'exec-7e648522-98d2-46e4-9c1c-7182f0cbebc1.png'
$keys=@(@('cheese_press','oil_press','barrel','grill'),@('egg','goat_milk','mayonnaise','cheese'),@('goat_cheese','truffle_oil','poppy_perfume','fairy_perfume'),@('chicken_meat','goat_meat','bait','premium_bait'),@('plant_manual','animal_manual','artisan_manual','chicken_egg'))
try{for($r=0;$r -lt 5;$r++){$boxes=[V2SpriteSlicer]::Row($b,$r,5,4);for($c=0;$c -lt 4;$c++){$key=$keys[$r][$c];if($r -eq 3 -and $c -lt 2){Emit $b $boxes[$c] ($key+'_tiny') 1 1;Emit $b $boxes[$c] ($key+'_medium') 2 2;Emit $b $boxes[$c] ($key+'_large') 3 2;continue};$w=if($r -eq 0){3}elseif($r -eq 4 -and $c -lt 3){2}else{1};$h=if($r -eq 0 -or ($r -eq 4 -and $c -lt 3)){3}elseif($key -eq 'goat_milk' -or $key.EndsWith('_oil') -or $key.EndsWith('_perfume')){2}else{1};Emit $b $boxes[$c] $key $w $h}}}finally{$b.Dispose()}
$b=Source 'foods-wine' 'exec-77e26ac7-1bd3-40cf-b277-a6bc991d7b45.png'
try{$boxes=[V2SpriteSlicer]::Row($b,0,2,4);for($c=0;$c -lt 4;$c++){Emit $b $boxes[$c] ('cooked_'+@('chicken','goat','beef','pork')[$c]) 3 2};$boxes=[V2SpriteSlicer]::Row($b,1,2,3);Emit $b $boxes[0] 'cooked_fish' 3 2;Emit $b $boxes[1] 'bottle_chili_spirit' 1 4;Emit $b $boxes[2] 'bottle_blueberry_wine' 1 4}finally{$b.Dispose()}
[IO.File]::WriteAllText((Join-Path $project 'Design/v2-art-exports.json'),($records|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
Write-Output ('Exported '+$records.Count+' new item/growth sprites; total '+@(Get-ChildItem -LiteralPath $items -File -Filter '*.png').Count)
