# The same shared rest must work regardless of which region found the valuable.
foreach($origin in $regions){
    $store=New-TestRun 220
    $found=Outing $origin.ToString()
    Check (Spend $found) 'First core discovery spends one shared search'
    foreach($i in 1..4){Invoke-Station 'RecordPreciousFind' @($found) | Out-Null}
    Check ($found.PreciousFinds -eq 4 -and (Get-Station 'Night').Used -eq 1) 'Four crate finds can belong to one still-active night without extra attempts'
    Check ((Get-Station 'PendingPreciousDay') -eq 220) 'Every origin records a pending shared rest'
    foreach($target in $regions){Check ((Invoke-Station 'PreciousChance' @((Outing $target.ToString()))) -gt 0) 'Finding a core still allows repeat or cross-region core finds that same night'}
    Invoke-Station 'Reload' | Out-Null
    Check ((Outing $origin.ToString()).PreciousFinds -eq 4 -and (Get-Station 'PreciousAvailable')) 'Reloading a lower or advanced discovery does not start rest early'
    Invoke-Station 'CompleteNight' | Out-Null
    foreach($restDay in 221..223){
        [StoreStation]::Instance.dayCounter=$restDay
        Check (-not (Get-Station 'PreciousAvailable')) 'Each of the next three complete nights is resting'
        Check ((Get-Station 'Night').Remaining -eq 10) 'Rest does not remove regular search attempts'
        foreach($target in $regions){Close-To (Invoke-Station 'PreciousChance' @((Outing $target.ToString()))) 0 'A discovery in any region disables all five core pools during rest'}
        Check (Spend (Outing 'Lower')) 'Lower ordinary searches can still spend the shared allowance during rest'
        Invoke-Station 'Reload' | Out-Null
        Check ((Get-Station 'Night').Used -eq 1 -and -not (Get-Station 'PreciousAvailable')) 'Reload retains spent ordinary attempts and shared core rest'
        Invoke-Station 'CompleteNight' | Out-Null
        Check ((Get-Station 'LastPreciousDay') -eq 220) 'Ordinary-only nights do not extend rest'
    }
    [StoreStation]::Instance.dayCounter=224
    foreach($target in $regions){Check ((Invoke-Station 'PreciousChance' @((Outing $target.ToString()))) -gt 0) 'Every core pool reopens on the fourth following night'}
}
Mark-Group 'Lower and advanced discoveries share three full rest nights; ordinary loot, repeat finds and exact recovery timing retained'

function Set-LegacyLowerRecord($store,[int]$findDay,[int]$count=2){
    $store.modData['station_scavenging:precious_settlement_version']='2'
    $store.modData['station_scavenging:outing_0_day']=$findDay.ToString()
    $store.modData['station_scavenging:outing_0_precious_finds']=$count.ToString()
    $store.modData['station_scavenging:outing_0_searches']='2'
}

$store=New-TestRun 310
Set-LegacyLowerRecord $store 310 4
$store.modData['station_scavenging:night_used']='2'
$store.modData['station_scavenging:last_precious_day']='309'
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'PendingPreciousDay') -eq 310 -and (Get-Station 'PreciousAvailable')) 'An ongoing legacy lower-find night remains eligible even during its old advanced-only rest'
Check ((Outing 'Lower').PreciousFinds -eq 4 -and (Get-Station 'Night').Used -eq 2) 'Migration preserves the four existing crates and spent searches'
Check ($store.modData['station_scavenging:precious_settlement_version'] -eq '3') 'Shared lower-rest migration is marked once'
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'PendingPreciousDay') -eq 310 -and (Get-Station 'PreciousAvailable')) 'Repeating reload does not settle a current-night migrated find early'
Invoke-Station 'CompleteNight' | Out-Null
[StoreStation]::Instance.dayCounter=311
foreach($target in $regions){Close-To (Invoke-Station 'PreciousChance' @((Outing $target.ToString()))) 0 'The completed migrated lower-find night blocks every core pool'}

$store=New-TestRun 330;Set-LegacyLowerRecord $store 329
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'LastPreciousDay') -eq 329 -and -not (Get-Station 'PreciousAvailable')) 'A retained previous-night lower find immediately restores its outstanding rest'
foreach($restDay in 330..332){[StoreStation]::Instance.dayCounter=$restDay;Invoke-Station 'Reload' | Out-Null;Check (-not (Get-Station 'PreciousAvailable')) 'Migrated rest stays anchored to discovery, not upgrade or reload day'}
[StoreStation]::Instance.dayCounter=333;Invoke-Station 'Reload' | Out-Null
Check (Get-Station 'PreciousAvailable') 'Old positive lower counters cannot restart rest after its original expiry'

$store=New-TestRun 350;Set-LegacyLowerRecord $store 350
$store.modData['station_scavenging:night_used']='2';$store.storeState=[PlayerStore+StoreState]::MORNING
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'LastPreciousDay') -eq 350 -and (Get-Station 'PendingPreciousDay') -eq -1 -and -not (Get-Station 'PreciousAvailable')) 'An already-finished same-day morning find settles immediately'
$store=New-TestRun 360;Set-LegacyLowerRecord $store 360
$store.modData['station_scavenging:night_closed']='1'
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'LastPreciousDay') -eq 360 -and (Get-Station 'PendingPreciousDay') -eq -1) 'An explicitly closed legacy pool is not treated as an ongoing find night'

$store=New-TestRun 381;Set-LegacyLowerRecord $store 378
$store.modData['station_scavenging:last_precious_day']='380'
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'LastPreciousDay') -eq 380) 'An older lower record never moves a newer advanced rest backwards'
$store=New-TestRun 381;Set-LegacyLowerRecord $store 380
$store.modData['station_scavenging:pending_precious_day']='381'
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'PreciousAvailable') -and (Get-Station 'PendingPreciousDay') -eq 381) 'An existing current-night advanced discovery retains repeat finds when older lower history is imported'
Invoke-Station 'CompleteNight' | Out-Null
Check ((Get-Station 'LastPreciousDay') -eq 381) 'The latest actual discovery remains the shared settlement anchor'

$store=New-TestRun 400;Set-LegacyLowerRecord $store 390
Invoke-Station 'Reload' | Out-Null
Check (Get-Station 'PreciousAvailable') 'An expired historical lower find grants no fresh upgrade-day rest'
$store=New-TestRun 400;Set-LegacyLowerRecord $store 410
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'PreciousAvailable') -and (Get-Station 'LastPreciousDay') -eq -1) 'Invalid future records cannot lock the save'
$store=New-TestRun 400;Set-LegacyLowerRecord $store 399 0
Invoke-Station 'Reload' | Out-Null
Check (Get-Station 'PreciousAvailable') 'A previous lower visit with no crate does not cause rest'
$store=New-TestRun 430
$store.modData['station_scavenging:precious_settlement_version']='1';$store.modData['station_scavenging:last_precious_day']='430'
Invoke-Station 'Reload' | Out-Null
Check ((Get-Station 'PreciousAvailable') -and (Get-Station 'PendingPreciousDay') -eq 430) 'The original pre-night-settlement migration remains supported'
Mark-Group 'One-time legacy lower-find migration, pending and completed nights, idempotence, old anchors and invalid-record handling'
