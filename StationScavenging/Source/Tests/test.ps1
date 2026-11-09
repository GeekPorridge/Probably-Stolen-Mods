param(
    [string]$GameDir = 'D:\steam\steamapps\common\Probably Stolen',
    [string]$ReportPath = '',
    [string]$NightWalkerDll = ''
)
$ErrorActionPreference='Stop'
$managedDir=Join-Path $GameDir 'Probably Stolen_Data\Managed'
$sourceDir=Split-Path -Parent $PSScriptRoot
$modDir=[IO.Path]::GetFullPath((Join-Path $sourceDir '..\Release\Mods\StationScavenging'))
foreach($name in @('UnityEngine.CoreModule.dll','UnityEngine.UI.dll','Unity.TextMeshPro.dll','UnityEngine.UIModule.dll','DseCecil.dll','Assembly-CSharp-firstpass.dll','Assembly-CSharp.dll')){[Reflection.Assembly]::LoadFrom((Join-Path $managedDir $name)) | Out-Null}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $modDir 'StationScavenging.dll'))
$privateStatic=[Reflection.BindingFlags]'Static,NonPublic'
$privateInstance=[Reflection.BindingFlags]'Instance,NonPublic'
$gameType=$assembly.GetType('StationScavenging.StationGame')
$lowerType=$assembly.GetType('StationScavenging.LowerScavenging')
$script:checks=0
$script:groups=[Collections.Generic.List[string]]::new()
function Check([bool]$ok,[string]$message){if(-not $ok){throw "FAIL: $message"};$script:checks++}
function Close-To([double]$actual,[double]$expected,[string]$message){Check ([Math]::Abs($actual-$expected) -lt 0.000001) "$message (actual=$actual expected=$expected)"}
function Mark-Group([string]$name){$script:groups.Add($name);Write-Output "PASS: $name"}
function Invoke-Station([string]$name,[object[]]$arguments=@()){return $gameType.GetMethod($name,$privateStatic).Invoke($null,$arguments)}
function Get-Station([string]$name){$property=$gameType.GetProperty($name,$privateStatic);if($null -ne $property){return $property.GetValue($null)};return $gameType.GetField($name,$privateStatic).GetValue($null)}
function New-TestRun([int]$day,[int]$seed=1234){
    $store=[Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([PlayerStore])
    $store.modData=[Collections.Generic.Dictionary[string,string]]::new()
    $store.modData['station_scavenging:route_seed']=$seed.ToString()
    $store.startingPerks=[Collections.Generic.List[StartingPerk]]::new()
    $store.storeReputations=[Collections.Generic.List[StoreReputation]]::new()
    $store.afterhourState=[Collections.Generic.Dictionary[string,AfterhourLocationState]]::new()
    $store.healthData=[HealthData]::new()
    $store.difficultyId='normal';$store.storeState=[PlayerStore+StoreState]::CLOSED
    foreach($region in [Enum]::GetValues([StationScavenging.Region])){$rep=[StoreReputation]::new();$rep.factionId=[StationScavenging.StationRules]::FactionId($region);$store.storeReputations.Add($rep)}
    [PlayerStore].GetField('instance',$privateStatic).SetValue($null,$store)
    $station=[Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([StoreStation]);$station.dayCounter=$day
    [StoreStation].GetField('instance',$privateStatic).SetValue($null,$station)
    Invoke-Station 'Reload' | Out-Null
    return $store
}
function Outing([string]$region){return Invoke-Station 'GetOuting' @([StationScavenging.Region]$region)}
function Spend($outing){return Invoke-Station 'SpendSearch' @($outing)}
function End-Outing($outing,[StationScavenging.Departure]$reason){return [StationScavenging.OutingState].GetMethod('End').Invoke($outing,[object[]]@($reason))}
function Add-Perk($store,[string]$id){$perk=[StartingPerk]::new();$perk.id=$id;$store.startingPerks.Add($perk)}
function Activate-Event($store,[string]$id,[int]$ends){$state=$store.GetAfterhourState('dumping_ground');$active=[AfterhourActiveEvent]::new();$active.eventId=$id;$active.endsOnDay=$ends;$state.activeEvents.Add($active)}

$settings=[StationScavenging.BalanceSettings]::new()
$regions=[Enum]::GetValues([StationScavenging.Region])
$days=@(1,10,15,18,20);$reps=@(0,5,10,12,15);$ids=[Collections.Generic.HashSet[string]]::new()
foreach($region in $regions){$i=[int]$region;Check ($ids.Add([StationScavenging.StationRules]::LocationId($region))) 'Unique region IDs';Check ([StationScavenging.StationRules]::RequiredDay($region) -eq $days[$i]) 'Day gate';Close-To ([StationScavenging.StationRules]::RequiredReputation($region)) $reps[$i] 'Reputation gate';Check ([StationScavenging.StationRules]::MeetsUnlock($region,$days[$i],$reps[$i],$true)) 'Exact unlock';if($i -gt 0){Check (-not [StationScavenging.StationRules]::MeetsUnlock($region,($days[$i]-1),100,$true)) 'Minimum day enforced'}}
Check (-not [StationScavenging.StationRules]::MeetsUnlock('BlackMarket',20,100,$false)) 'Exchange gate'
Mark-Group 'Stable region IDs and original unlock gates'

foreach($total in @(10,15,8,12)){
    $night=[StationScavenging.NightState]::new();$night.Day=20;$night.Total=$total
    $outings=@(foreach($region in $regions){$o=[StationScavenging.OutingState]::new();$o.Day=20;$o.Region=$region;$o})
    for($i=0;$i -lt $total;$i++){
        Close-To $night.Progress ($i/[double]($total-1)) 'Next-search progress uses the whole nightly allowance'
        Check ($outings[$i%5].TrySearch($night)) 'All five regions spend one shared pool'
        Check ($night.Remaining -eq ($total-$i-1)) 'Each click spends exactly one chance'
    }
    foreach($o in $outings){Check (-not $o.TrySearch($night)) 'No region can search after the shared cap';Check (-not $o.Ended) 'Exhaustion leaves time to pack'}
}
$night=[StationScavenging.NightState]::new();$night.Day=1
$o=[StationScavenging.OutingState]::new();$o.Day=2;Check (-not $o.TrySearch($night)) 'Stale outings cannot spend another day';$o.Day=1
Check (End-Outing $o 'Voluntary') 'Manual departure';Check (-not $o.TrySearch($night)) 'Ended outings cannot spend';Check ($night.Used -eq 0) 'Rejected search never spends'
Mark-Group '10/15 searches shared across five regions, exact limits and packing after exhaustion'

foreach($region in $regions){
    $previous=-1.0
    for($i=0;$i -lt 15;$i++){
        $progress=$i/14.0;$p=[StationScavenging.StationRules]::PreciousChance($region,$progress,$true,$false,$settings)
        Check ($p -ge $previous) 'All five crate curves rise with used searches';$previous=$p
        Close-To $p (0.01+0.04*$progress) 'Crate curve is 1% to 5%'
    }
    Close-To ([StationScavenging.StationRules]::DetectionChance($region,0,'Normal')) (@(0,0.01,0.02,0.015,0.015)[[int]$region]) 'Detection starts at its region base'
    Close-To ([StationScavenging.StationRules]::DetectionChance($region,1,'Normal')) (@(0,0.06,0.07,0.065,0.065)[[int]$region]) 'Detection endpoint is calibrated for attempts'
    Close-To ([StationScavenging.StationRules]::PreciousChance($region,1,$false,$true,$settings)) 0 'Shared cooldown disables core valuables in every region'
}
foreach($p in @(0,0.5,1)){Close-To ([StationScavenging.StationRules]::PreciousChance('BlackMarket',$p,$true,$true,$settings)) (0.015+0.06*$p) 'Card-night 1.5% to 7.5% curve'}
Close-To ([StationScavenging.StationRules]::PreciousChance('Lower',1,$false,$false,$settings)) 0 'Lower service crate obeys shared valuable rest'
Close-To ([StationScavenging.StationRules]::PreciousChance('Upper',-5,$true,$false,$settings)) 0.01 'Negative progress clamps'
Close-To ([StationScavenging.StationRules]::PreciousChance('Upper',5,$true,$false,$settings)) 0.05 'Overshoot clamps'
foreach($case in @(@(0,20,0.5),@(100,45,0.75),@(400,120,1.5),@(1000,200,3))){$q=[StationScavenging.StationRules]::GetPenalty($case[0],1,1,$settings);Check ($q.Credits -eq $case[1]) 'Unchanged fines';Close-To $q.Reputation $case[2] 'Unchanged reputation penalties'}
Mark-Group 'Progressive crate/card odds, regional detection curves and penalty formulas'

foreach($region in @('Upper','Security','Revolution','BlackMarket')){
    foreach($entry in [StationScavenging.LootCatalog]::Entries($region)){Check ($entry.ItemId -notin @('junk','glass_shard','empty_beer_bottle') -and $entry.Category.ToString() -ne 'Waste') 'Advanced pools exclude garbage'}
    foreach($profile in [Enum]::GetValues([StationScavenging.LootProfile])){foreach($day in @(10,15,18,20,25,30,40)){foreach($condition in [Enum]::GetValues([StationScavenging.SupplyCondition])){for($i=0;$i -lt 20;$i++){$choice=[StationScavenging.LootCatalog]::Pick([StationScavenging.LootCatalog]::Entries($region),$profile,$day,$condition,($i/20.0));Check ($null -ne $choice -and $choice.MinimumDay -le $day) 'Native loot progression';Check ($choice.ItemId -notmatch 'keycard|loot_crate') 'Regular/scanner pool cannot duplicate advanced core valuables'}}}}
}
Check ([StationScavenging.LootCatalog]::Keycards.Count -eq 7) 'Seven card types'
Mark-Group 'Ordinary loot progression and garbage/core exclusions'

$store=New-TestRun 30
$night=Get-Station 'Night';Check ($night.Total -eq 10 -and $night.Remaining -eq 10) 'Default allowance is ten'
$upper=Outing 'Upper';$lower=Outing 'Lower';$black=Outing 'BlackMarket'
foreach($i in 1..4){Check (Spend $upper) 'Spend upper attempts'}
Check ((Get-Station 'Night').Remaining -eq 6 -and $store.scavengingAttempts -eq 4) 'Native and saved shared counters agree'
Close-To (Invoke-Station 'PreciousChance' @($lower)) (0.01+0.04*4/9) 'Lower inherits previous upper progress'
Check (Spend $lower) 'Lower consumes the same pool'
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'Night').Used -eq 5 -and (Get-Station 'Night').Remaining -eq 5) 'Reload preserves the shared allowance'
$upper=Outing 'Upper';Check ($upper.Searches -eq 4) 'Outing counts persist'
$store.scavengingAttempts=0;Invoke-Station 'RestoreNativeAttempts' | Out-Null;Check ($store.scavengingAttempts -eq 5) 'Native counter reset cannot refill allowance'
$black=Outing 'BlackMarket';foreach($i in 1..4){Check (Spend $black) 'Cross-region spend'}
Close-To (Invoke-Station 'PreciousChance' @($upper)) 0.05 'Final displayed crate chance reaches the endpoint'
Check (Spend $black) 'Tenth search remains available';Check (-not (Spend $upper)) 'Eleventh search rejected across areas'
Check (-not $black.Ended) 'No forced departure at zero allowance'
Invoke-Station 'RecordPreciousFind' @($black) | Out-Null;Check ($black.PreciousFinds -eq 1) 'Last allowed search can still register its valuable'
[StoreStation]::Instance.dayCounter=31
Check ((Get-Station 'Night').Used -eq 0 -and (Get-Station 'Night').Total -eq 10) 'Next night resets the single pool'
Mark-Group 'Native save ledger, cross-region progress, reloads, final-search finds and next-night reset'

$store=New-TestRun 40
Activate-Event $store 'georgio_assistance' 40
$night=Get-Station 'Night';Check ($night.Total -eq 15 -and $night.Remaining -eq 15) 'Real Georgio modifier grants exactly five shared attempts'
Close-To ([ScavHelper]::GetMinorWoundChance()) 0 'Native Georgio minor-wound immunity survives'
Close-To ([ScavHelper]::GetMajorWoundChance()) 0 'Native Georgio major-wound immunity survives'
$a=Outing 'Lower';$b=Outing 'Security'
foreach($i in 1..7){Check (Spend $a) 'Event lower searches'}
Close-To (Invoke-Station 'PreciousChance' @($b)) 0.03 'Half of fifteen-search progress gives the same middle chance'
foreach($i in 1..7){Check (Spend $b) 'Event upper searches'}
Close-To (Invoke-Station 'PreciousChance' @($b)) 0.05 'Fifteenth search reaches the same maximum'
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'Night').Remaining -eq 1) 'Event reload does not grant another five'
$b=Outing 'Security';Check (Spend $b) 'Fifteenth event search succeeds';Check (-not (Spend (Outing 'Upper'))) 'Sixteenth search blocked'
[StoreStation]::Instance.dayCounter=41
Check ((Get-Station 'Night').Total -eq 10) 'Expired one-day event returns to ten'
$store=New-TestRun 50;Add-Perk $store 'proficient_scavenger';Check ((Get-Station 'Night').Total -eq 12) 'Native proficient +2 retained'
$store=New-TestRun 50;Add-Perk $store 'careful_scavenger';Check ((Get-Station 'Night').Total -eq 8) 'Native careful -2 retained';Close-To ([ScavHelper]::GetMinorWoundChance()) 0 'Native careful injury immunity retained'
$store=New-TestRun 50;Add-Perk $store 'maimed';Check ((Get-Station 'Night').Total -eq 0) 'Hard native disability restriction not bypassed'
Mark-Group 'Actual Georgio +5 and injury immunity, event expiry, perk modifiers and disability gates'

$store=New-TestRun 60
$a=Outing 'Upper';Check (Spend $a) 'First rare search'
foreach($i in 1..3){Invoke-Station 'RecordPreciousFind' @($a) | Out-Null;Check (Get-Station 'PreciousAvailable') 'Repeated finds stay eligible'}
Check ($a.PreciousFinds -eq 3 -and (Get-Station 'Night').Used -eq 1) 'Finding valuables spends no additional actions'
End-Outing $a 'Voluntary' | Out-Null;Invoke-Station 'Save' @($a) | Out-Null
$b=Outing 'Revolution';Check (Spend $b) 'Another region can find valuables same night';Invoke-Station 'RecordPreciousFind' @($b) | Out-Null
Invoke-Station 'Reload' | Out-Null
Check ((Outing 'Upper').PreciousFinds -eq 3 -and (Get-Station 'PreciousAvailable')) 'Reload preserves repeat finds before settlement'
Invoke-Station 'CompleteNight' | Out-Null
Check (-not (Get-Station 'PreciousAvailable') -and (Get-Station 'Night').Closed) 'Sleep commits cooldown and closes the allowance'
foreach($day in 61..63){[StoreStation]::Instance.dayCounter=$day;Check (-not (Get-Station 'PreciousAvailable')) 'Three following rest nights';foreach($region in $regions){Close-To (Invoke-Station 'PreciousChance' @((Outing $region.ToString()))) 0 'All five regions have zero core chance during rest'}}
[StoreStation]::Instance.dayCounter=64;Check (Get-Station 'PreciousAvailable') 'Valuables return on the fourth following night'
Invoke-Station 'CompleteNight' | Out-Null;[StoreStation]::Instance.dayCounter=65;Check (Get-Station 'PreciousAvailable') 'No-find night adds no cooldown'
$store=New-TestRun 70;$a=Outing 'Lower';Invoke-Station 'RecordPreciousFind' @($a) | Out-Null;Invoke-Station 'CompleteNight' | Out-Null;[StoreStation]::Instance.dayCounter=71;Check (-not (Get-Station 'PreciousAvailable')) 'Lower crates trigger the same shared rest'
$store=New-TestRun 80;$a=Outing 'Upper';Invoke-Station 'RecordPreciousFind' @($a) | Out-Null;[StoreStation]::Instance.dayCounter=81;Invoke-Station 'Reload' | Out-Null;Check (-not (Get-Station 'PreciousAvailable')) 'Missed sleep hook settles on next day';Check ($gameType.GetField('LastPreciousDay',$privateStatic).GetValue($null) -eq 80) 'Settlement anchored to discovery day'
Mark-Group 'Repeated core finds, night settlement and shared five-region cooldown timing'

. (Join-Path $PSScriptRoot 'shared-rest.ps1')

$store=New-TestRun 90;$a=Outing 'Security';Check (Spend $a) 'Search before detection';Check (Invoke-Station 'MarkCaught' @($a)) 'Detection closes the night';Check (-not (Invoke-Station 'MarkCaught' @($a))) 'Detection resolves once'
foreach($region in $regions){Check (-not (Spend (Outing $region.ToString()))) 'Caught night blocks every region'}
Invoke-Station 'Reload' | Out-Null;Check ((Get-Station 'Night').Caught -and (Get-Station 'Night').Remaining -eq 0) 'Caught flag survives reload'
[StoreStation]::Instance.dayCounter=91;Check ((Get-Station 'Night').Remaining -eq 10) 'Detection lock expires next night'
$store=New-TestRun 100
$store.modData.Remove('station_scavenging:night_budget_day') | Out-Null
$store.modData['station_scavenging:timed_1_day']='100';$store.modData['station_scavenging:timed_1_searches']='2';$store.modData['station_scavenging:timed_1_remaining']='0'
$store.scavengingAttempts=3;$lowerState=$store.GetAfterhourState('dumping_ground');$lowerState.lastVisitDay=100
Invoke-Station 'Reload' | Out-Null;Check ((Get-Station 'Night').Used -eq 5 -and (Get-Station 'Night').Remaining -eq 5) 'Old timed plus lower searches migrate without a fresh pool'
Invoke-Station 'Reload' | Out-Null;Check ((Get-Station 'Night').Used -eq 5) 'Migration never counts searches twice'
$store=New-TestRun 1 999;Check ((Get-Station 'Night').Used -eq 0 -and (Get-Station 'PreciousAvailable')) 'Save isolation'
Mark-Group 'Whole-night detection lock, idempotence, old-version migration and save isolation'

$store=New-TestRun 101
$store.storeState=[PlayerStore+StoreState]::MORNING
Invoke-Station 'Reload' | Out-Null
Check (-not (Get-Station 'Night').Closed -and (Get-Station 'Night').Remaining -eq 10) 'Loading a normal morning must not lock its coming evening'
$store.storeState=[PlayerStore+StoreState]::CLOSED
Check (Spend (Outing 'Lower')) 'The evening after a morning load can spend its allowance'
$store.storeState=[PlayerStore+StoreState]::MORNING
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'Night').Closed) 'Post-sleep same-day fallback closes an already-used pool'
[StoreStation]::Instance.dayCounter=102
Check (-not (Get-Station 'Night').Closed -and (Get-Station 'Night').Remaining -eq 10) 'Next morning prepares a fresh allowance for the next evening'
Mark-Group 'Morning-load versus post-sleep lifecycle regression'

$store=New-TestRun 1
$original=[AfterhourLocation]::Get('dumping_ground')
$nativeWeights=[ordered]@{junkTable=650;materialTable=150;t1moduleTable=10;t2moduleTable=5;toolTable=20;householdTable=47.5;makeshiftWeaponTable=20;packedFoodTable=47.5;dumpingGroundMedical=50}
$group=[Collections.Generic.List[LootEntry]]::new();foreach($key in $nativeWeights.Keys){$group.Add([LootEntry]::new($key,[float]$nativeWeights[$key]))}
[LootRegistry].GetField('vanillaGroups',$privateStatic).GetValue($null)['dumpingGroundTG']=$group
[LootRegistry].GetField('dirty',$privateStatic).SetValue($null,$true)
$entry=[StationScavenging.ModEntry]::new();$entry.OnEnable();$entry.OnEnable()
foreach($chance in @(0.01,0.03,0.05,0)){
    $lowerType.GetMethod('UpdateCrateWeight',$privateStatic).Invoke($null,[object[]]@($chance)) | Out-Null
    $rows=[ModHelper]::GetLootGroupEntries('dumpingGroundTG');$crate=@($rows | Where-Object id -eq ([StationScavenging.LootCatalog]::LowerCrateTable))
    Check ($crate.Count -eq 1) 'Crate weight updates never duplicate the route'
    Close-To ($crate[0].weight/($rows | Measure-Object weight -Sum).Sum) $chance 'Actual native group matches progressive crate chance'
    foreach($row in $rows | Where-Object id -ne ([StationScavenging.LootCatalog]::LowerCrateTable)){Close-To $row.weight $nativeWeights[$row.id] 'Native ordinary weights retained'}
}
Activate-Event $store 'fresh_dump' 1
$lowerType.GetMethod('UpdateCrateWeight',$privateStatic).Invoke($null,[object[]]@(0.05)) | Out-Null
$rows=[ModHelper]::GetLootGroupEntries('dumpingGroundTG');$crate=@($rows | Where-Object id -eq ([StationScavenging.LootCatalog]::LowerCrateTable))[0]
Close-To ($crate.weight/(675+$crate.weight)) 0.05 'Native junk -50% event still matches the shared service curve'
$hook=Outing 'Upper';Invoke-Station 'RecordPreciousFind' @($hook) | Out-Null
[ModHook].GetMethod('FireOnGoingSleepLate',$privateStatic).Invoke($null,$null) | Out-Null
Check ((Get-Station 'Night').Closed -and -not (Get-Station 'PreciousAvailable')) 'Real registered sleep hook settles both systems'
$entry.OnDisable();Check (@([ModHelper]::GetLootGroupEntries('dumpingGroundTG') | Where-Object id -eq ([StationScavenging.LootCatalog]::LowerCrateTable)).Count -eq 0) 'Disable removes custom crate route';Check ([Object]::ReferenceEquals([AfterhourLocation]::Get('dumping_ground'),$original)) 'Disable restores original lower location'
$entry.OnEnable();Check (@([ModHelper]::GetLootGroupEntries('dumpingGroundTG') | Where-Object id -eq ([StationScavenging.LootCatalog]::LowerCrateTable)).Count -eq 1) 'Re-enable has one crate route';$entry.OnDisable()
Mark-Group 'Native progressive lower loot registry, event weights, real sleep hook and reversible lifecycle'

[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null
[Microsoft.CodeAnalysis.MetadataReference[]]$refs=@(foreach($name in @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll')){[Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $managedDir $name))})
$tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'LowerBridgeProbe.cs')))
$comp=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('LowerBridgeProbe',[Microsoft.CodeAnalysis.SyntaxTree[]]@($tree),$refs,[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary))
$memory=[IO.MemoryStream]::new();$emit=$comp.Emit($memory);if(-not $emit.Success){throw ($emit.Diagnostics -join "`n")};$probeAssembly=[Reflection.Assembly]::Load($memory.ToArray());$memory.Dispose()
$store=New-TestRun 110;$a=Outing 'Upper';foreach($i in 1..4){Spend $a | Out-Null}
$probe=[Activator]::CreateInstance($probeAssembly.GetType('LowerBridgeProbe'))
$bridgeType=$assembly.GetType('StationScavenging.LowerLevelsLocation');$bridge=[Activator]::CreateInstance($bridgeType,$privateInstance,$null,[object[]]@($probe),$null)
$bridgeType.GetMethod('EnterNative',$privateInstance).Invoke($bridge,$null) | Out-Null
Check ($probe.Arrivals -eq 1 -and $store.scavengingAttempts -eq 4) 'Actual native arrival reset is followed by shared-counter restoration'
$bridge.Tick();$bridge.Leave();Check ($probe.Ticks -eq 1 -and $probe.Leaves -eq 1) 'Native lower story callbacks retained'
Check ($bridge.CountsDumpedWaste -and $bridge.AllowsHazardousWaste -and $bridge.ImageKey -eq $probe.ImageKey) 'Native disposal and art retained'
Mark-Group 'Lower arrival reset regression and native story/disposal callbacks'

. (Join-Path $PSScriptRoot 'reentry.ps1')

# Continue the travel integration with its original day-110 fixture.
$store=New-TestRun 110
$a=Outing 'Upper';foreach($i in 1..4){Spend $a | Out-Null}

if($NightWalkerDll -and (Test-Path -LiteralPath $NightWalkerDll)){
    $nw=[Reflection.Assembly]::LoadFrom($NightWalkerDll);$nwEntry=[Activator]::CreateInstance($nw.GetType('NightWalker.ModEntry'));$nwEntry.OnEnable()
    Add-Perk $store 'night_walker.night_walker'
    $history=[AfterhourLocationState]::new();$history.visits=1;$history.lastVisitDay=110;$store.afterhourState[[StationScavenging.StationRules]::LocationId('Upper')]=$history
    $nwRules=$nw.GetType('NightWalker.NightRules');$nwRules.GetMethod('SyncRemainingVisits',$privateStatic).Invoke($null,$null) | Out-Null
    Check ($store.visitLeftTonight -eq 2) 'Night Walker still grants its remaining trips'
    Check ((Get-Station 'Night').Remaining -eq 6) 'Extra trips never restore scavenging allowance'
    $dest=[DelegateAfterhourLocation]::new();[DelegateAfterhourLocation].GetField('id').SetValue($dest,[StationScavenging.StationRules]::LocationId('BlackMarket'))
    Check ($nwRules.GetMethod('CanTravel',$privateStatic).Invoke($null,[object[]]@($dest))) 'Distinct destination remains available to Night Walker'
    $nwEntry.OnDisable();Mark-Group 'Actual Night Walker assembly retains travel without multiplying shared searches'
}

. (Join-Path $PSScriptRoot 'presentation.ps1')

$locales=@{}
foreach($locale in @('en','zh')){$rows=Import-Csv -LiteralPath (Join-Path $modDir "Localization\$locale.csv") -Header Key,Value;Check (@($rows.Key | Select-Object -Unique).Count -eq $rows.Count) 'Unique localized keys';$map=@{};foreach($row in $rows){Check (-not [string]::IsNullOrWhiteSpace($row.Value)) 'Nonempty localized text';$map[$row.Key]=$row.Value};$locales[$locale]=$map}
Check ((($locales.en.Keys | Sort-Object) -join '|') -eq (($locales.zh.Keys | Sort-Object) -join '|')) 'Matching localization keys'
foreach($key in $locales.en.Keys){$en=@([regex]::Matches($locales.en[$key],'\{\d+(?:[^}]*)?\}') | ForEach-Object Value | Sort-Object);$zh=@([regex]::Matches($locales.zh[$key],'\{\d+(?:[^}]*)?\}') | ForEach-Object Value | Sort-Object);Check (($en -join '|') -eq ($zh -join '|')) 'Matching format placeholders'}
$allowed=[string[]]@(Get-ChildItem -LiteralPath $managedDir -Filter '*.dll' -File | ForEach-Object BaseName)
$scan=[ModScanner]::Scan([string[]]@((Join-Path $modDir 'StationScavenging.dll')),$allowed,$modDir,$true)
Check (-not $scan.Refused -and $scan.Findings.Count -eq 0 -and $scan.Capabilities.Count -eq 0) "Native scanner accepts DLL: $($scan.Error) $($scan.FormatFindings())"
Mark-Group "Localization and native scanner ($($scan.MethodsScanned) methods)"
$report=[ordered]@{version='1.5.5';checks=$script:checks;groups=$script:groups.ToArray();scannerMethods=$scan.MethodsScanned;scannerFindings=$scan.Findings.Count;scannerHardFlags=$scan.HardFlags.Count;gameplayTest='Isolated native-assembly checks include actual repeat lower arrival/departure, saved-visit recovery, exhaustion/injury/caught/sleep guards, five-region shared valuable rest and legacy save migration. Original third-party DLL integration and unchanged optional box rules are reported separately.'}
if($ReportPath){$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8}
Write-Output "ALL CHECKS PASSED: $($script:checks)"
