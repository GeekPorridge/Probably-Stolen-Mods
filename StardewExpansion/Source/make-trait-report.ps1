param([string]$GameDir='D:/steam/steamapps/common/Probably Stolen')
$ErrorActionPreference='Stop'
$managedDirectory=Join-Path $GameDir 'Probably Stolen_Data/Managed'
$projectDirectory=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$modDll=Join-Path $projectDirectory 'Release/Mods/PelicanCultivation/PelicanCultivation.dll'
foreach($name in @('UnityEngine.CoreModule.dll','UnityEngine.UIModule.dll','UnityEngine.UI.dll','Assembly-CSharp-firstpass.dll','Assembly-CSharp.dll')){[Reflection.Assembly]::LoadFrom((Join-Path $managedDirectory $name)) | Out-Null}
[Reflection.Assembly]::LoadFrom($modDll) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
$source=@'
using System;
using System.Collections.Generic;
using PelicanCultivation;
public static class PelicanTraitFacts {
  private sealed class ReportSeed : GameItem {
    public ReportSeed(string id) : base(id) { modifiedState=state; }
    public override void Validate() { modifiedState=state; }
    public override void Destroy() { unitCount=0; }
  }
  public static List<Dictionary<string,object>> Read() {
    var result=new List<Dictionary<string,object>>();
    foreach(var crop in Rules.Crops) {
      PlantRegistry.Add(Crops.DefinePlant(crop,crop.Key));
      var seed=new ReportSeed(crop.SeedId);
      HydroponicHelper.InitSeedItem(seed,crop.SeedId);
      var traits=new List<Dictionary<string,object>>();
      foreach(var pair in crop.Traits) {
        var definition=PlantTraitRegistry.Get(pair.Key);
        var stats=new Dictionary<string,float>();
        foreach(var stat in definition.statPercentPerRank) stats[stat.Key]=definition.GetStatPercent(stat.Key,pair.Value);
        if(pair.Key=="delicious") stats["PRODUCE_SELL_PRICE"]=PlantGeneticsHelper.RankedPercent(PlantTraitList.DELICIOUS_PRICE_PERCENT,pair.Value);
        traits.Add(new Dictionary<string,object>{{"id",pair.Key},{"rank",pair.Value},{"name_en",definition.englishName},{"stats_percent",stats},{"complexity_offset",definition.complexityOffsetPerRank*pair.Value},{"locked",definition.locked},{"perennial",definition.perennial}});
      }
      result.Add(new Dictionary<string,object>{{"crop",crop.Key},{"seed_id",crop.SeedId},{"traits",traits},{"initial_complexity",PlantGeneticsHelper.GetComplexity(seed)}});
    }
    return result;
  }
}
'@
[Microsoft.CodeAnalysis.SyntaxTree[]]$trees=@([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source))
[Microsoft.CodeAnalysis.MetadataReference[]]$references=@(
  foreach($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll')){[Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managedDirectory $name))}
  [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($modDll)
)
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('PelicanTraitFacts',$trees,$references,$options)
$stream=[IO.MemoryStream]::new()
try {$emitted=$compilation.Emit($stream);if(-not $emitted.Success){$emitted.Diagnostics | ForEach-Object {$_.ToString()};throw 'Trait report compilation failed.'};[Reflection.Assembly]::Load($stream.ToArray()) | Out-Null} finally {$stream.Dispose()}
$facts=@([PelicanTraitFacts]::Read())
$json=$facts | ConvertTo-Json -Depth 14
[IO.File]::WriteAllText((Join-Path $projectDirectory 'Design/seed-traits-current.json'),$json+"`n",[Text.UTF8Encoding]::new($false))
$names=@{parsnip='防风草';potato='土豆';corn='玉米';wheat='小麦';hops='啤酒花';strawberry='草莓';starfruit='杨桃';ancient_fruit='上古水果'}
$traitNames=@{edible='可食用';fast_grower='生长迅速';slow_grower='生长缓慢';bountiful='高产';delicious='美味';drought_resistant='耐旱';thirsty='耗水量大';hardy='生命力顽强';frail='生命力脆弱';nutritious='营养丰富';simple_genome='简单基因组';complex_genome='复杂基因组';perennial='多年生'}
$statNames=@{GROWTH_SPEED='生长速度';HARVEST_QUANTITY='收获数量';WATER_USE='耗水量';CONTAMINANT_SENSITIVITY='污染敏感度';CALORIES='热量';SEED_YIELD='种子产量';PRODUCE_SELL_PRICE='作物售价'}
$zh=[Collections.Generic.List[string]]::new();$en=[Collections.Generic.List[string]]::new()
$zh.Add('老乡来了 v1.0 — 8种种子默认初始词条与效果')
$zh.Add('依据当前模组定义和本机游戏的原版词条公式生成；已编辑种子以实际词条为准。')
$zh.Add('全部8种均已移除默认可食用词条；产物本身具有原版食物属性，可陈列售卖，符合配方的作物也可酿酒。')
$zh.Add('数值保留两位小数，游戏面板可能显示取整值。Ⅱ级按原版 rank^0.75 曲线计算。')
$zh.Add('')
$en.Add('A Fellow Townsman Arrives v1.0 — Default traits of the eight added seeds')
$en.Add('Generated from current mod definitions and the installed native trait formulas. Edited strains retain their actual traits.')
$en.Add('The default Edible trait has been removed from all eight seeds. Their produce is inherently food merchandise; eligible crops can also be brewed.')
$en.Add('Percentages below have two decimal places. The game may round its display; rank II uses the native rank^0.75 curve.')
$en.Add('')
function Signed([double]$number){$number.ToString('+0.##;-0.##;0',[Globalization.CultureInfo]::InvariantCulture)}
foreach($crop in $facts) {
  $zh.Add($names[$crop.crop]+'种子');$en.Add($crop.crop+' seed')
  foreach($trait in $crop.traits) {
    $effectsZh=[Collections.Generic.List[string]]::new();$effectsEn=[Collections.Generic.List[string]]::new()
    if($trait.id -eq 'edible'){$effectsZh.Add('产物具有食物属性');$effectsEn.Add('edible food produce')}
    if($trait.perennial){$effectsZh.Add('采收后母株保留并再次结果');$effectsEn.Add('parent survives harvest and fruits again')}
    foreach($entry in $trait.stats_percent.GetEnumerator()){$effectsZh.Add($statNames[$entry.Key]+' '+(Signed $entry.Value)+'%');$effectsEn.Add($entry.Key+' '+(Signed $entry.Value)+'%')}
    if($trait.complexity_offset -ne 0){$effectsZh.Add('基因复杂度 '+(Signed $trait.complexity_offset));$effectsEn.Add('genetic complexity '+(Signed $trait.complexity_offset))}
    if($trait.locked){$effectsZh.Add('原版锁定，不可编辑');$effectsEn.Add('native locked trait')}
    $rank=@('','Ⅰ','Ⅱ','Ⅲ')[$trait.rank]
    $zh.Add('  '+$traitNames[$trait.id]+$rank+'：'+($effectsZh -join '；')+'。')
    $en.Add('  '+$trait.name_en+' '+$trait.rank+': '+($effectsEn -join '; ')+'.')
  }
  $zh.Add('');$en.Add('')
}
$zh.Add('多个不同词条按原版规则叠乘；例如多年生Ⅰ和高产Ⅰ同时存在时，产量倍率为0.75×1.20=0.90。')
$zh.Add('多年生不等于自动返种。自动补种依次从本机输出区、主背包、仓库取同种种子；没有种子则等待。')
$en.Add('Different traits multiply under native rules: Perennial I and Bountiful I give 0.75×1.20=0.90 harvest quantity.')
$en.Add('Perennial survival does not create replacement seeds. Automatic replanting checks matching seeds in the machine output, main inventory, then storage.')
foreach($record in @(@{File='种子词条与效果.zh.txt';Lines=$zh},@{File='Seed-traits.en.txt';Lines=$en})){
  [IO.File]::WriteAllLines((Join-Path $projectDirectory ('Docs/'+$record.File)),[string[]]$record.Lines,[Text.UTF8Encoding]::new($false))
}
Write-Output 'Generated eight seed trait reports directly from current definitions and native formulas.'
