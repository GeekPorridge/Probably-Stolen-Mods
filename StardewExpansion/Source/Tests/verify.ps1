param([string]$GameDir='D:/steam/steamapps/common/Probably Stolen')
$ErrorActionPreference='Stop'
$managedDirectory=Join-Path $GameDir 'Probably Stolen_Data/Managed'
$releaseDirectory=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Release/Mods/PelicanCultivation'))
$dllPath=Join-Path $releaseDirectory 'PelicanCultivation.dll'
foreach($assemblyName in @('UnityEngine.CoreModule.dll','UnityEngine.UIModule.dll','UnityEngine.UI.dll','UnityEngine.TextRenderingModule.dll','Unity.TextMeshPro.dll','0Harmony.dll','DseCecil.dll','Assembly-CSharp-firstpass.dll','Assembly-CSharp.dll')){[Reflection.Assembly]::LoadFrom((Join-Path $managedDirectory $assemblyName)) | Out-Null}
[Reflection.Assembly]::LoadFrom($dllPath) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
[Microsoft.CodeAnalysis.SyntaxTree[]]$trees=@(foreach($name in @('Verification.cs','HeadlessRunner.cs','FarmVerification.cs','TownDialogueVerification.cs','SeaFounderVerification.cs','NightReportVerification.cs','CropUpdateVerification.cs','V2Verification.cs','ManualStockVerification.cs','BaitGeometryVerification.cs','ReservoirAccessVerification.cs','ReservoirEventVerification.cs','CompatibilityVerification.cs')){[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $PSScriptRoot $name)))})
[Microsoft.CodeAnalysis.MetadataReference[]]$references=@(
  foreach($assemblyName in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','0Harmony.dll','Newtonsoft.Json.dll')){[Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managedDirectory $assemblyName))}
  [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($dllPath)
)
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('PelicanCultivation.Verification',$trees,$references,$options)
$verificationDirectory=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../verification'))
New-Item -ItemType Directory -Path $verificationDirectory -Force | Out-Null
$verificationDll=Join-Path $verificationDirectory 'PelicanCultivation.Verification.dll'
$stream=[IO.File]::Create($verificationDll)
try {
  $result=$compilation.Emit($stream)
  if(-not $result.Success){$result.Diagnostics | ForEach-Object {$_.ToString()};throw 'Verification compilation failed.'}
} finally {$stream.Dispose()}
[Reflection.Assembly]::LoadFrom($verificationDll) | Out-Null
Write-Output ([PelicanVerification]::Run())
Write-Output ([FarmVerification]::Run())
# The game's Harmony needs BinaryFormatter, removed from modern pwsh's CLR.
# .NET 6 supports it and the game's default interface methods. No game process
# is controlled. The harness artifacts remain outside the runtime package.
$runtimeConfig=@{runtimeOptions=@{tfm='net6.0';framework=@{name='Microsoft.NETCore.App';version='6.0.0'};configProperties=@{'System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization'=$true}}}
[IO.File]::WriteAllText((Join-Path $verificationDirectory 'PelicanCultivation.Verification.runtimeconfig.json'),($runtimeConfig | ConvertTo-Json -Depth 5))
& (Join-Path $PSScriptRoot 'prepare-engine-fixture.ps1') -GameDir $GameDir
& dotnet $verificationDll $GameDir $dllPath
if($LASTEXITCODE -ne 0){throw 'Native CLR verification failed.'}
$allowedReferences=[string[]]@(Get-ChildItem -LiteralPath $managedDirectory -Filter '*.dll' -File | ForEach-Object {$_.BaseName})
$scan=[ModScanner]::Scan([string[]]@($dllPath),$allowedReferences,$releaseDirectory,$true)
if($scan.Refused -or $scan.Error){$scan | Format-List *;throw 'Native scanner refused the mod.'}
$declared=@(([xml](Get-Content -LiteralPath (Join-Path $releaseDirectory 'manifest.xml') -Raw)).Manifest.Capabilities.Capability)
if(Compare-Object @($scan.Capabilities | Sort-Object) @($declared | Sort-Object)){throw 'Detected capabilities differ from the manifest.'}
Write-Output ('Native scanner passed: '+$scan.MethodsScanned+' methods; declared capability: '+($declared -join ', '))
function Read-NativeLocale([string[]]$lines) {
  $seen=@{}
  foreach($line in $lines){
    if([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')){continue}
    $separator=$line.IndexOf(',')
    if($separator -lt 1){throw 'Invalid native locale line.'}
    $key=$line.Substring(0,$separator).Trim()
    if($key -notmatch '^[a-z][a-z0-9_]*$' -or $key -ceq 'Key'){throw ('Invalid native key: '+$key)}
    if($seen.ContainsKey($key)){throw ('Duplicate native key: '+$key)}
    $seen[$key]=$true
    $value=$line.Substring($separator+1).Trim()
    if($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')){$value=$value.Substring(1,$value.Length-2).Replace('""','"')}
    $value=$value.Replace('\n',"`n")
    [pscustomobject]@{Key=$key;Value=$value}
  }
}
$regressionRejected=$false
try {$null=@(Read-NativeLocale @('"Key","Value"','"crop_parsnip","防风草"'))} catch {$regressionRejected=$true}
if(-not $regressionRejected){throw 'Regression: spreadsheet-style quoted keys must be rejected.'}
$english=@(Read-NativeLocale ([IO.File]::ReadAllLines((Join-Path $releaseDirectory 'Localization/en.csv'))))
$chinese=@(Read-NativeLocale ([IO.File]::ReadAllLines((Join-Path $releaseDirectory 'Localization/zh.csv'))))
if(Compare-Object ($english.Key | Sort-Object) ($chinese.Key | Sort-Object)){throw 'Language keys differ.'}
foreach($table in @($english,$chinese)){
  if($table.Count -ne @($table.Key | Sort-Object -Unique).Count){throw 'Duplicate language key.'}
  foreach($entry in $table){if([string]::IsNullOrWhiteSpace($entry.Value) -or $entry.Value -match '\\n'){throw ('Empty or escaped-newline text: '+$entry.Key)}}
}
foreach($entry in $english){
  $zh=$chinese | Where-Object Key -eq $entry.Key
  $enArgs=@([regex]::Matches($entry.Value,'\{\d+\}') | ForEach-Object {$_.Value} | Sort-Object)
  $zhArgs=@([regex]::Matches($zh.Value,'\{\d+\}') | ForEach-Object {$_.Value} | Sort-Object)
  if(($enArgs -join ',') -ne ($zhArgs -join ',')){throw ('Translation placeholders differ: '+$entry.Key)}
  if($entry.Value -match '[\p{IsCJKUnifiedIdeographs}]'){throw ('Chinese text in English table: '+$entry.Key)}
}
$expectedMeatNames=@{
  farm_beef_tiny=@('一口牛肉','Bite of Beef');farm_beef_medium=@('牛肉（小）','Beef (Small)');farm_beef_large=@('牛肉（标准）','Beef (Standard)')
  farm_pork_tiny=@('一口猪肉','Bite of Pork');farm_pork_medium=@('猪肉（小）','Pork (Small)');farm_pork_large=@('猪肉（标准）','Pork (Standard)')
  farm_lobster_meat_tiny=@('一口龙虾肉','Bite of Lobster Meat');farm_lobster_meat_small=@('龙虾肉（小）','Lobster Meat (Small)')
  farm_lobster_meat_medium=@('龙虾肉（标准）','Lobster Meat (Standard)');farm_lobster_meat_large=@('龙虾肉（大）','Lobster Meat (Large)')
}
foreach($key in $expectedMeatNames.Keys){
  $zhName=($chinese | Where-Object Key -eq $key).Value;$enName=($english | Where-Object Key -eq $key).Value
  if($zhName -cne $expectedMeatNames[$key][0] -or $enName -cne $expectedMeatNames[$key][1]){throw ('Wrong size-specific meat name: '+$key)}
  if($zhName -match '\d|千卡|卡路里' -or $enName -match '\d|kcal|calorie'){throw ('Calories leaked into meat name: '+$key)}
}
foreach($key in @('cow','pig','lobster','lobster_roe','lobster_fry','sea_cucumber','sea_cucumber_roe','sea_cucumber_fry','milk','truffle_small','truffle','sea_cucumber_food','beef','pork','lobster_meat')){
  foreach($suffix in @('desc','flavor')){
    $narrativeKey='farm_'+$key+'_'+$suffix
    foreach($table in @($english,$chinese)){
      $line=($table | Where-Object Key -eq $narrativeKey).Value
      if([string]::IsNullOrWhiteSpace($line)){throw ('Missing farm narrative: '+$narrativeKey)}
      if($line -match '沿用原版|机制|物品ID|native (disease|genetics|mechanic)|kcal|千卡'){throw ('Implementation or numeric rules leaked into narrative: '+$narrativeKey)}
    }
  }
}
Write-Output 'Approved bilingual meat names and fifteen pairs of natural descriptions/flavor text passed.'
$expectedEggNames=@{farm_lobster_roe=@('龙虾卵','Lobster Eggs');farm_sea_cucumber_roe=@('海参卵','Sea Cucumber Eggs')}
foreach($key in $expectedEggNames.Keys){
  if(($chinese | Where-Object Key -eq $key).Value -cne $expectedEggNames[$key][0] -or ($english | Where-Object Key -eq $key).Value -cne $expectedEggNames[$key][1]){throw ('Wrong simplified egg name: '+$key)}
}
foreach($code in (@(1,2,10,11,12,13,14,20,21,22)+@(100..107)+@(200..205)+@(300..305))){
  $key=[PelicanCultivation.TownDialogue]::Key($code)
  foreach($table in @($english,$chinese)){if([string]::IsNullOrWhiteSpace(($table | Where-Object Key -eq $key).Value)){throw ('Missing contextual dialogue: '+$key)}}
}
foreach($key in @('farmer_goods_seed','farmer_goods_ranch')){if($key -notin $english.Key){throw ('Missing visit-specific goods line: '+$key)}}
Write-Output 'Simplified egg names and all 30 contextual dialogue texts plus two goods lines passed in both languages.'
$sourceText=(Get-ChildItem -LiteralPath (Split-Path $PSScriptRoot) -Filter '*.cs' | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw}) -join "`n"
$knownKeys=@($english.Key)
foreach($sourceFile in Get-ChildItem -LiteralPath (Split-Path $PSScriptRoot) -Filter '*.cs'){
foreach($match in [regex]::Matches([IO.File]::ReadAllText($sourceFile.FullName),'(?<![a-zA-Z0-9_.])(?:(ModEntry|Expansion)\.)?L\("([a-z_]+)"')){
  $key=$match.Groups[2].Value
  if($key.EndsWith('_')){continue}
  if($match.Groups[1].Value -eq 'Expansion' -or ($match.Groups[1].Value -eq '' -and $sourceFile.Name -eq 'Expansion.cs')){$key='v2_'+$key}
  if($key -notin $knownKeys){throw ('Missing literal localization key: '+$key)}
}
}
Add-Type -AssemblyName System.Drawing
$itemsDirectory=Join-Path $releaseDirectory 'Textures/Items'
if(Test-Path -LiteralPath (Join-Path $releaseDirectory 'Items')){throw 'Regression: native loader does not scan a root Items directory.'}
$sprites=@(Get-ChildItem -LiteralPath $itemsDirectory -Filter '*.png')
if($sprites.Count -ne 160){throw 'Expected 160 sprites including the three new seafood dishes.'}
$expansionArt=@{}
foreach($record in (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Design/v2-art-exports.json') -Raw | ConvertFrom-Json)){$expansionArt[$record.key]=$record}
if($expansionArt.Count -ne 100){throw 'Expansion/revised sprite catalog is incomplete'}
$farmKeys=@((Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../Design/farm-art-exports.json') -Raw | ConvertFrom-Json) | Where-Object key -ne 'farmer_ranch' | ForEach-Object key)
if($farmKeys.Count -ne 31 -or @($farmKeys | Sort-Object -Unique).Count -ne 31){throw 'Expected 31 distinct husbandry item sprite records.'}
foreach($key in $farmKeys){if(-not (Test-Path -LiteralPath (Join-Path $itemsDirectory ($key+'.png')))){throw ('Missing husbandry sprite: '+$key)}}
foreach($sprite in $sprites){
  $bitmap=[Drawing.Bitmap]::new($sprite.FullName)
  try {
    if($expansionArt.ContainsKey($sprite.BaseName)){$record=$expansionArt[$sprite.BaseName];if($bitmap.Width -ne 128*$record.width -or $bitmap.Height -ne 128*$record.height){throw ('Expansion sprite geometry mismatch: '+$sprite.BaseName)}}
    elseif($sprite.BaseName -in @('farmer','farmer_ranch','farmer_artisan')){if($bitmap.Width -ne 128 -or $bitmap.Height -ne 160){throw 'Wrong portrait size.'}}
    elseif($sprite.BaseName -in $farmKeys){
      $farmWidth=0;$farmHeight=0
      if(-not [PelicanCultivation.FarmGeometry]::Shape($sprite.BaseName,[ref]$farmWidth,[ref]$farmHeight)){throw ('No farm geometry: '+$sprite.BaseName)}
      if($bitmap.Width -ne $farmWidth*128 -or $bitmap.Height -ne $farmHeight*128){throw ('Husbandry texture/footprint mismatch: '+$sprite.BaseName)}
    }
    elseif($sprite.BaseName.EndsWith('_seed')){if($bitmap.Width -ne 128 -or $bitmap.Height -ne 128){throw ('Seed detail must be retained in a 128px one-cell texture: '+$sprite.Name)}}
    elseif($sprite.BaseName.StartsWith('bottle_')){
      $beer=$sprite.BaseName -in @('bottle_beer','bottle_pale_ale','bottle_chili_spirit')
      $expectedWidth=if($beer){256}else{128};$expectedHeight=if($beer){384}else{512}
      if($bitmap.Width -ne $expectedWidth -or $bitmap.Height -ne $expectedHeight){throw ('Bottle must retain 128px detail per native cell: '+$sprite.Name)}
    }
    elseif($sprite.BaseName -ne 'parsnip_small' -and -not $sprite.BaseName.EndsWith('_large')){
      $cropKey=$sprite.BaseName -replace '_(small|large|huge)$',''
      $crop=[PelicanCultivation.Rules]::Crops | Where-Object Key -eq $cropKey
      if($null -eq $crop){throw ('Unknown crop sprite: '+$sprite.Name)}
      $size=if($sprite.BaseName.EndsWith('_small')){0}elseif($sprite.BaseName.EndsWith('_large')){2}elseif($sprite.BaseName.EndsWith('_huge')){3}else{1}
      if($bitmap.Width -ne 128*[PelicanCultivation.Rules]::CropWidth($crop,$size) -or $bitmap.Height -ne 128*[PelicanCultivation.Rules]::CropHeight($crop,$size)){throw ('Crop texture differs from its exact 2/4/6/9-cell footprint: '+$sprite.Name)}
    }
    if($bitmap.GetPixel(0,0).A -ne 0){throw ('Sprite corner is not transparent: '+$sprite.Name)}
  } finally {$bitmap.Dispose()}
}
$files=@(Get-ChildItem -LiteralPath $releaseDirectory -Recurse -File)
if(@($files | Where-Object {$_.Extension -notin @('.dll','.xml','.csv','.png')}).Count -gt 0){throw 'Unexpected source/docs in runtime package.'}
Write-Output ('Bilingual text, placeholders, transparency, native grid and minimal runtime passed: '+$english.Count+' keys, '+$sprites.Count+' sprites, '+(($files | Measure-Object Length -Sum).Sum)+' bytes.')
Write-Output 'Native asset-layout and CSV-key regression checks passed.'
foreach($cropKey in @('parsnip','potato','corn','wheat','hops','strawberry','starfruit','ancient_fruit')){
  foreach($prefix in @('seed_desc_','produce_desc_')){
    if(($prefix+$cropKey) -notin $knownKeys){throw ('Missing narrative description: '+$prefix+$cropKey)}
  }
}
if(($chinese | Where-Object Key -eq 'farmer_name').Value -ne '老乡'){throw 'Visitor display name did not update.'}
if(@(Get-ChildItem -LiteralPath $itemsDirectory -Filter 'mark_*.png').Count -gt 0){throw 'Obsolete bottle overlays remain in the runtime.'}
Write-Output 'Native seed filter, HD sprite geometry, smaller portrait and sixteen bilingual crop narratives passed.'
$drawingReferences=@([Drawing.Bitmap].Assembly.Location,[Drawing.Rectangle].Assembly.Location)+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll' | ForEach-Object {$_.FullName})
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Drawing;
public static class PelicanArtAssertions {
  public static Rectangle Subject(Bitmap bitmap,Rectangle cell) {
    int left=cell.Right,top=cell.Bottom,right=-1,bottom=-1;
    for(int y=cell.Top;y<cell.Bottom;y++) for(int x=cell.Left;x<cell.Right;x++) {
      if(bitmap.GetPixel(x,y).A<128) continue;
      left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);
    }
    if(right<left) throw new Exception("Empty artwork cell");
    return Rectangle.FromLTRB(left,top,right+1,bottom+1);
  }
}
'@
$projectDirectory=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$aspectChecks=0
foreach($atlas in @(
  @{File='R2-seed-packets.png';Columns=4;Rows=2;Keys=@('parsnip_seed','potato_seed','corn_seed','wheat_seed','hops_seed','strawberry_seed','starfruit_seed','ancient_fruit_seed')},
  @{File='R4-fruit-wines.png';Columns=4;Rows=1;Keys=@('bottle_ancient_wine','bottle_strawberry_wine','bottle_starfruit_wine','bottle_blueberry_wine')},
  @{File='beer-bottles-012.png';Columns=2;Rows=1;Keys=@('bottle_beer','bottle_pale_ale')}
)) {
  $original=[Drawing.Bitmap]::new((Join-Path $projectDirectory ('ArtOriginals/'+$atlas.File)))
  try {
    for($index=0;$index -lt $atlas.Keys.Count;$index++) {
      $column=$index%$atlas.Columns;$row=[int][Math]::Floor($index/$atlas.Columns)
      $cell=[Drawing.Rectangle]::FromLTRB([int][Math]::Round($column*$original.Width/[double]$atlas.Columns),[int][Math]::Round($row*$original.Height/[double]$atlas.Rows),[int][Math]::Round(($column+1)*$original.Width/[double]$atlas.Columns),[int][Math]::Round(($row+1)*$original.Height/[double]$atlas.Rows))
      $subject=[PelicanArtAssertions]::Subject($original,$cell)
      $exported=[Drawing.Bitmap]::new((Join-Path $itemsDirectory ($atlas.Keys[$index]+'.png')))
      try {
        $exportSubject=[PelicanArtAssertions]::Subject($exported,[Drawing.Rectangle]::new(0,0,$exported.Width,$exported.Height))
        $relativeError=[Math]::Abs(($exportSubject.Width/[double]$exportSubject.Height)/($subject.Width/[double]$subject.Height)-1)
        if($relativeError -gt 0.025){throw ('Artwork aspect ratio changed: '+$atlas.Keys[$index])}
        if($exportSubject.Width -lt 64 -or $exportSubject.Height -lt 96){throw ('Artwork was reduced below its detail budget: '+$atlas.Keys[$index])}
        $aspectChecks++
      } finally {$exported.Dispose()}
    }
  } finally {$original.Dispose()}
}
Write-Output ('Original artwork aspect ratios and retained texture detail passed for '+$aspectChecks+' seed/bottle exports.')
$cropAtlas=[Drawing.Bitmap]::new((Join-Path $projectDirectory 'ArtOriginals/crops-clean.png'))
$cropAspectChecks=0
try {
  for($index=0;$index -lt 8;$index++) {
    $crop=[PelicanCultivation.Rules]::Crops[$index]
    $column=$index%4;$row=[int][Math]::Floor($index/4)
    $cell=[Drawing.Rectangle]::FromLTRB([int][Math]::Round($column*$cropAtlas.Width/4.0),[int][Math]::Round($row*$cropAtlas.Height/2.0),[int][Math]::Round(($column+1)*$cropAtlas.Width/4.0),[int][Math]::Round(($row+1)*$cropAtlas.Height/2.0))
    $originalBounds=[PelicanArtAssertions]::Subject($cropAtlas,$cell)
    $previousWidth=0;$previousHeight=0
    $suffixes=if($crop.Sized){@('_small','','_huge')}else{@('')}
    foreach($suffix in $suffixes) {
      $export=[Drawing.Bitmap]::new((Join-Path $itemsDirectory ($crop.Key+$suffix+'.png')))
      try {
        $bounds=[PelicanArtAssertions]::Subject($export,[Drawing.Rectangle]::new(0,0,$export.Width,$export.Height))
        $relativeError=[Math]::Abs(($bounds.Width/[double]$bounds.Height)/($originalBounds.Width/[double]$originalBounds.Height)-1)
        if($relativeError -gt 0.025){throw ('Crop artwork ratio changed: '+$crop.Key+$suffix)}
        if($crop.Sized -and ($bounds.Width -le $previousWidth -or $bounds.Height -le $previousHeight)){throw ('Crop subject did not visibly grow between tiers: '+$crop.Key+$suffix)}
        $previousWidth=$bounds.Width;$previousHeight=$bounds.Height
        $cropAspectChecks++
      } finally {$export.Dispose()}
    }
  }
} finally {$cropAtlas.Dispose()}
Write-Output ('Crop artwork proportions and strictly increasing visible subject sizes passed for '+$cropAspectChecks+' exports.')
