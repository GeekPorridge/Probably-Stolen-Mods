param([string]$GameDir='D:\steam\steamapps\common\Probably Stolen',[string]$ReportPath='')
$ErrorActionPreference = 'Stop'
$managedDirectory = Join-Path $GameDir 'Probably Stolen_Data\Managed'
$sourceDirectory=Split-Path -Parent $PSScriptRoot
$modDirectory=[IO.Path]::GetFullPath((Join-Path $sourceDirectory '..\Release\Mods\NightWalker'))
foreach ($name in @('UnityEngine.CoreModule.dll', '0Harmony.dll', 'DseCecil.dll', 'Assembly-CSharp-firstpass.dll', 'Assembly-CSharp.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $managedDirectory $name)) | Out-Null
}
$modAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $modDirectory 'NightWalker.dll'))
$staticPrivate = [Reflection.BindingFlags]'Static, NonPublic'
$listPatch = $modAssembly.GetType('NightWalker.DestinationListPatch').GetMethod('Postfix', $staticPrivate)
$travelGuard = $modAssembly.GetType('NightWalker.TravelGuardPatch').GetMethod('Prefix', $staticPrivate)
$script:assertions = 0

function Assert-Equal($actual, $expected, [string]$message) {
    if ($actual -cne $expected) { throw "FAIL: $message -- expected '$expected', actual '$actual'" }
    $script:assertions++
    Write-Output "PASS: $message"
}

function Set-TestRun([bool]$hasPerk, [int]$day) {
    # Isolated objects in this PowerShell process only; no save files or running
    # game memory are read or modified. Skip constructors that need Unity.
    $script:store = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([PlayerStore])
    $script:store.startingPerks = [Collections.Generic.List[StartingPerk]]::new()
    if ($hasPerk) { $script:store.startingPerks.Add([StartingPerk]@{id='night_walker.night_walker'}) }
    $script:store.afterhourState = [Collections.Generic.Dictionary[string,AfterhourLocationState]]::new()
    $script:store.storeState = [PlayerStore+StoreState]::CLOSED
    $script:store.visitLeftTonight = 1
    [PlayerStore].GetField('instance', $staticPrivate).SetValue($null, $script:store)
    $station = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([StoreStation])
    $station.dayCounter = $day
    [StoreStation].GetField('instance', $staticPrivate).SetValue($null, $station)
}

function Fire-Hook([string]$name) {
    [ModHook].GetMethod($name, $staticPrivate).Invoke($null, $null) | Out-Null
}

function Visit([string]$id) {
    $store.afterhourState[$id] = [AfterhourLocationState]@{visits=1;lastVisitDay=[StoreStation]::Instance.dayCounter}
    $store.visitLeftTonight--  # Native OnArriveStore's return bookkeeping.
    Fire-Hook 'FireOnReturningStoreLate'
}

function Can-Travel([AfterhourLocation]$location) {
    return [bool]$travelGuard.Invoke($null, [object[]]@($location))
}

function Filter-List([Collections.Generic.List[AfterhourLocation]]$source) {
    $copy = [Collections.Generic.List[AfterhourLocation]]::new($source)
    [object[]]$arguments = [object[]]::new(1)
    $arguments[0] = $copy
    $listPatch.Invoke($null, $arguments) | Out-Null
    return ,$copy
}

# Native config objects, pre-seeded in this isolated process so no user settings
# or save files are read/written. The loader normally performs Config.For/Init.
$config=[Activator]::CreateInstance([ModConfig],[Reflection.BindingFlags]'Instance,NonPublic',$null,[object[]]@('night_walker'),$null)
[ModConfig].GetField('configs',$staticPrivate).GetValue($null)['night_walker']=$config
$configuredEntry=[NightWalker.ModEntry]::new()
$configuredEntry.Init([ModManifest]::new())
[NightWalker.ModEntry].GetField('log',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($configuredEntry,$null)
$cost=$config.Get('perk_cost')
Assert-Equal $config.HasEntries $true 'Native mod settings detects the new option'
Assert-Equal $cost.Value 2 'Fresh configuration defaults to 2 points'
Assert-Equal $cost.Default 2 'Reset-to-default value is 2 points'
Assert-Equal $cost.RequiresRestart $true 'Native settings UI requests restart before perk registration'
Assert-Equal $cost.Min 1 'Minimum selectable cost is 1'
Assert-Equal $cost.Max 2 'Maximum selectable cost is 2'
$applyCost=$cost.GetType().GetMethod('ApplyBoxed',[Reflection.BindingFlags]'Instance,NonPublic')
[void]$applyCost.Invoke($cost,[object[]]@([int]1))
$configuredEntry.OnEnable()
Assert-Equal ([StartingPerkList]::GetModPerk('night_walker.night_walker').cost) 1 'Native setting 1 creates a 1-point perk'
$oldDefinition=[StartingPerkList]::GetModPerk('night_walker.night_walker')
[void]$applyCost.Invoke($cost,[object[]]@([int]2))
Assert-Equal $oldDefinition.cost 1 'Applying a restart-required setting does not mutate an existing perk'
$configuredEntry.OnDisable();$configuredEntry.OnEnable()
Assert-Equal ([StartingPerkList]::GetModPerk('night_walker.night_walker').cost) 2 'Next registration uses the saved 2-point setting'
Assert-Equal $oldDefinition.cost 1 'Existing perk objects keep their original cost'
$configuredEntry.OnDisable()
$deserialize=$cost.GetType().GetMethod('TryDeserialize',[Reflection.BindingFlags]'Instance,NonPublic')
Assert-Equal ([bool]$deserialize.Invoke($cost,[object[]]@('1'))) $true 'Native saved setting can deserialize 1 point'
Assert-Equal $cost.Value 1 'Saved 1-point choice is retained'
[void]$applyCost.Invoke($cost,[object[]]@([int]0));Assert-Equal $cost.Value 1 'Out-of-range low setting clamps to 1'
[void]$applyCost.Invoke($cost,[object[]]@([int]99));Assert-Equal $cost.Value 2 'Out-of-range high setting clamps to 2'

Set-TestRun $true 7
$entry = [NightWalker.ModEntry]::new()
$entry.OnEnable()
$perk = [StartingPerkList]::GetModPerk('night_walker.night_walker')
Assert-Equal $perk.cost 2 'Starting perk costs 2 points'
Assert-Equal $perk.iconKey 'night_walker:night_walker_icon' 'Starting perk uses its own sprite key'
Assert-Equal ([int]$perk.type) ([int][StartingPerk+StartingPerkType]::POSITIVE) 'Starting perk is positive'

$dump = [DumpingGroundLocation]::new()
$commissary = [CommissaryLocation]::new()
$jackson = [DrJacksonLocation]::new()
$modded = [DelegateAfterhourLocation]::new()
$moddedId = 'other_mod.black_dock'
[DelegateAfterhourLocation].GetField('id').SetValue($modded, $moddedId)
[DelegateAfterhourLocation].GetField('source').SetValue($modded, 'other_mod')
[DelegateAfterhourLocation].GetField('isAvailable').SetValue($modded, [Func[bool]]{return $true})
$all = [Collections.Generic.List[AfterhourLocation]]::new()
$all.Add($dump); $all.Add($commissary); $all.Add($jackson); $all.Add($modded)

$store.storeState = [PlayerStore+StoreState]::MORNING
$store.visitLeftTonight = 0
Fire-Hook 'FireOnGameLoadedLate'
Assert-Equal $store.visitLeftTonight 0 'Loading a morning save grants no nighttime trips'
$store.storeState = [PlayerStore+StoreState]::OPEN
Fire-Hook 'FireOnShutterClosedLate'
Assert-Equal $store.visitLeftTonight 0 'Daytime state does not receive extra trips'
$store.storeState = [PlayerStore+StoreState]::CLOSED
$store.visitLeftTonight = 1
Fire-Hook 'FireOnShutterClosedLate'
Assert-Equal $store.visitLeftTonight 3 'Closing the shop grants 3 total trips'
Assert-Equal (Filter-List $all).Count 4 'All discovered destinations remain listed before visiting'
Assert-Equal (Can-Travel $dump) $true 'First destination is allowed'

Fire-Hook 'FireOnReturningStoreLate'
Assert-Equal $store.visitLeftTonight 3 'Canceling the transit menu spends no trip'
Visit $dump.Id
Assert-Equal $store.visitLeftTonight 2 'Returning from the first destination leaves 2 trips'
Assert-Equal (Can-Travel $dump) $false 'Revisiting the first destination is blocked at Travel'
Assert-Equal (Can-Travel $modded) $true 'An unvisited other-mod destination remains allowed'
$filtered = Filter-List $all
Assert-Equal $filtered.Count 3 'Visited destination is removed from the nighttime list'
Assert-Equal ($filtered.Contains($dump)) $false 'The first destination cannot be selected again'
Assert-Equal $all.Count 4 'Filtering preserves the original registry/list'
Fire-Hook 'FireOnReturningStoreLate'
Assert-Equal $store.visitLeftTonight 2 'Canceling after a previous visit does not restore trips'

Visit $moddedId
Assert-Equal $store.visitLeftTonight 1 'Other-mod destination consumes the second trip'
Assert-Equal (Can-Travel $modded) $false 'Other-mod destination also cannot be revisited'
Visit $commissary.Id
Assert-Equal $store.visitLeftTonight 0 'Third distinct destination exhausts the night'
Assert-Equal (Can-Travel $jackson) $false 'A fourth distinct destination is blocked'
Assert-Equal (Filter-List $all).Count 0 'No destination is selectable after the third visit'
Fire-Hook 'FireOnShutterClosedLate'
Assert-Equal $store.visitLeftTonight 0 'Repeating the close hook cannot refill the same night'
Fire-Hook 'FireOnGameLoadedLate'
Assert-Equal $store.visitLeftTonight 0 'Reloading current-night history cannot refill trips'

[StoreStation]::Instance.dayCounter = 8
$store.visitLeftTonight = 1
Fire-Hook 'FireOnShutterClosedLate'
Assert-Equal $store.visitLeftTonight 3 'A new night restores 3 trips'
Assert-Equal (Can-Travel $dump) $true 'Yesterday destination is visitable on the next night'
Assert-Equal (Filter-List $all).Count 4 'All destinations reappear on the next night'

Set-TestRun $true 8
$store.afterhourState[$dump.Id] = [AfterhourLocationState]@{visits=5;lastVisitDay=8}
$store.afterhourState[$moddedId] = [AfterhourLocationState]@{visits=1;lastVisitDay=8}
$store.afterhourState['removed_mod.old_location'] = [AfterhourLocationState]@{visits=1;lastVisitDay=7}
$store.afterhourState['null_entry'] = $null
Fire-Hook 'FireOnGameLoadedLate'
Assert-Equal $store.visitLeftTonight 1 'Loaded native visit history restores the correct remainder'
Assert-Equal (Can-Travel $dump) $false 'Loaded visit history still prevents a duplicate'
Assert-Equal (Can-Travel $jackson) $true 'An unvisited destination works after loading'

$withDuplicates = [Collections.Generic.List[AfterhourLocation]]::new($all)
$withDuplicates.Add($dump)
$withDuplicates.Add($null)
$filteredDuplicates = Filter-List $withDuplicates
Assert-Equal $filteredDuplicates.Count 3 'Reverse filtering removes every visited duplicate'
Assert-Equal ($filteredDuplicates.Contains($null)) $true 'Filtering handles a null list entry safely'

$store.afterhourState['removed_mod.visited_today'] = [AfterhourLocationState]@{visits=1;lastVisitDay=8}
Fire-Hook 'FireOnGameLoadedLate'
Assert-Equal $store.visitLeftTonight 0 'A visited location still consumes its trip after its mod is removed'
$store.afterhourState[$commissary.Id] = [AfterhourLocationState]@{visits=1;lastVisitDay=8}
Fire-Hook 'FireOnGameLoadedLate'
Assert-Equal $store.visitLeftTonight 0 'Imported history beyond the cap never produces a negative remainder'
Assert-Equal (Can-Travel $jackson) $false 'Early cap counting still blocks a further trip'

Set-TestRun $true 8
$store.afterhourState[''] = [AfterhourLocationState]@{visits=1;lastVisitDay=8}
$store.afterhourState['null_entry'] = $null
$store.afterhourState['old_entry'] = [AfterhourLocationState]@{visits=1;lastVisitDay=7}
Fire-Hook 'FireOnGameLoadedLate'
Assert-Equal $store.visitLeftTonight 3 'Empty IDs, null states and previous-night records do not consume trips'

Set-TestRun $true 8
Fire-Hook 'FireOnGameLoadedLate'
Assert-Equal $store.visitLeftTonight 3 'A separate run does not inherit another run visits'
$store.afterhourState = $null
Fire-Hook 'FireOnGameLoadedLate'
Assert-Equal $store.visitLeftTonight 3 'Legacy/missing history is handled safely'

Set-TestRun $false 8
Fire-Hook 'FireOnShutterClosedLate'
Assert-Equal $store.visitLeftTonight 1 'An unselected perk leaves vanilla trip count unchanged'
$store.afterhourState[$dump.Id] = [AfterhourLocationState]@{visits=1;lastVisitDay=8}
Assert-Equal (Can-Travel $dump) $true 'An unselected perk adds no Travel restriction'
Assert-Equal (Filter-List $all).Count 4 'An unselected perk leaves destination list unchanged'

$entry.OnDisable()
Set-TestRun $true 8
Fire-Hook 'FireOnShutterClosedLate'
Assert-Equal $store.visitLeftTonight 1 'Disabling unsubscribes hooks'
Assert-Equal ([StartingPerkList]::GetModPerk('night_walker.night_walker') -eq $null) $true 'Disabling unregisters the perk'
Assert-Equal (Filter-List $all).Count 4 'Disabled patches have no effect'

[string[]]$allowed = Get-ChildItem -LiteralPath $managedDirectory -Filter '*.dll' | ForEach-Object { $_.BaseName }
$scan = [ModScanner]::Scan([string[]]@((Join-Path $modDirectory 'NightWalker.dll')), $allowed, $modDirectory, $true)
Assert-Equal $scan.Refused $false 'Native game scanner accepts the DLL and package'
Assert-Equal $scan.Capabilities.Count 0 'Native scanner requires no extra capabilities'
Assert-Equal $scan.Findings.Count 0 'Native scanner reports no findings'
Write-Output "Native scanner examined $($scan.MethodsScanned) methods."
Write-Output "All $script:assertions assertions passed against installed game types."
$locales=@{}
foreach($locale in @('en','zh')){
    $map=@{}
    foreach($row in Import-Csv -LiteralPath (Join-Path $modDirectory "Localization\$locale.csv") -Header Key,Value){
        Assert-Equal ([string]::IsNullOrWhiteSpace($row.Value)) $false 'Localized configuration text is nonempty'
        $map[$row.Key]=$row.Value
    }
    $locales[$locale]=$map
}
Assert-Equal (($locales.en.Keys | Sort-Object) -join '|') (($locales.zh.Keys | Sort-Object) -join '|') 'All four labels/descriptions have matching English and Chinese keys'
$report=[ordered]@{version='1.1.0';checks=$script:assertions;scannerMethods=$scan.MethodsScanned;scannerFindings=$scan.Findings.Count;scannerHardFlags=$scan.HardFlags.Count;note='Isolated native game assemblies; no settings/save writes. Live settings layout not visually tested.'}
if($ReportPath){$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding utf8}
Write-Output "ALL CHECKS PASSED: $script:assertions"
