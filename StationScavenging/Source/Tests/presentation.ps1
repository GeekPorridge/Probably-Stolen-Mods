$flavorType=$assembly.GetType('StationScavenging.StationFlavor')
$transitType=$assembly.GetType('StationScavenging.StationTransitUI')
$artType=$assembly.GetType('StationScavenging.StationArt')
$locationType=$assembly.GetType('StationScavenging.ScavengingLocation')
function Status-Key([StationScavenging.Region]$region){return $flavorType.GetMethod('StatusKey',$privateStatic).Invoke($null,[object[]]@($region))}
$store=New-TestRun 1
$presentationEntry=[StationScavenging.ModEntry]::new();$presentationEntry.OnEnable()
foreach($region in @('Upper','Security','Revolution','BlackMarket')){
    $area=[Activator]::CreateInstance($locationType,$privateInstance,$null,[object[]]@([StationScavenging.Region]$region,[AfterhourLocation]::Get('dumping_ground')),$null)
    Check ($area.IsDiscovered() -and -not $area.IsAvailable()) 'Locked advanced cards are visible but cannot be entered'
    Check ((Status-Key $region) -eq 'status_locked') 'Locked card has an explicit access status'
    Check ($area.ImageKey -match '^station_scavenging:.+_cover$') 'Each advanced cover uses its mod asset key'
    Check ($area.BackgroundKey -match '^station_scavenging:.+_background$') 'Each advanced background uses its mod asset key'
}
foreach($i in 1..10){Spend (Outing 'Lower') | Out-Null}
foreach($region in $regions){Check ((Status-Key $region) -eq 'status_exhausted') 'Every exhausted card says no searches left rather than generic unavailable'}
$store=New-TestRun 31;Invoke-Station 'MarkCaught' @((Outing 'Upper')) | Out-Null
foreach($region in $regions){Check ((Status-Key $region) -eq 'status_caught') 'Caught status takes precedence over generic unavailability'}
$args=[object[]]@('dr_jackson',[StationScavenging.Region]::Lower)
Check (-not $transitType.GetMethod('TryRegion',$privateStatic).Invoke($null,$args)) 'Doctor and commissary statuses are outside the presentation scope'
Mark-Group 'Visible locked destinations, explicit exhausted/caught status, distinct art keys and native-location isolation'

$assetHashes=[Collections.Generic.HashSet[string]]::new()
foreach($region in @('Upper','Security','Revolution','BlackMarket')){
    foreach($kind in @('CoverKey','BackgroundKey')){
        $key=$artType.GetMethod($kind,$privateStatic).Invoke($null,[object[]]@([StationScavenging.Region]$region))
        $file=Join-Path $modDir ('Textures\Items\'+($key -split ':',2)[1]+'.png')
        Check (Test-Path -LiteralPath $file) 'All eight consuming sprite keys resolve to packaged PNG files'
        $stream=[IO.File]::OpenRead($file);try{$header=[byte[]]::new(24);[void]$stream.Read($header,0,24)}finally{$stream.Dispose()}
        Check (($header[0..7] -join ',') -eq '137,80,78,71,13,10,26,10') 'Valid PNG signature'
        $w=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($header,16));$h=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($header,20))
        $expectedWidth=if($kind -eq 'CoverKey'){592}else{1672}
        Check ($w -eq $expectedWidth -and [Math]::Abs($w/[double]$h-16/9.0) -lt 0.01) 'Covers use 2x native card resolution; full-size backgrounds retain widescreen geometry'
        Check ($assetHashes.Add((Get-FileHash -LiteralPath $file).Hash)) 'Every cover/background has distinct image content'
    }
}
Mark-Group 'Eight distinct production PNG assets, widescreen geometry and package resolution'

Check ([StationScavenging.StationEvents]::Catalogue.Count -eq 12) 'Twelve authored events'
foreach($region in @('Upper','Security','Revolution','BlackMarket')){
    $activeCount=0
    foreach($testDay in 1..300){
        $selected=[StationScavenging.StationEvents]::Select($region,1234,$testDay)
        $again=[StationScavenging.StationEvents]::Select($region,1234,$testDay)
        Check ([Object]::ReferenceEquals($selected,$again)) 'Daily selection is deterministic across reloads'
        if($null -ne $selected){$activeCount++;Check ($selected.Region.ToString() -eq $region) 'Events remain tied to their own region'}
    }
    Check ($activeCount -gt 60 -and $activeCount -lt 180) 'Daily selection leaves ordinary nights as well as event nights'
}
Check ($null -eq [StationScavenging.StationEvents]::Select('Lower',1234,100)) 'Lower native events are never replaced'
foreach($e in [StationScavenging.StationEvents]::Catalogue){
    $eventSeed=0
    foreach($candidate in 1..2000){$selected=[StationScavenging.StationEvents]::Select($e.Region,$candidate,150);if($null -ne $selected -and $selected.Id -eq $e.Id){$eventSeed=$candidate;break}}
    Check ($eventSeed -gt 0) 'Every authored event is reachable'
    $store=New-TestRun 150 $eventSeed
    $o=Outing $e.Region.ToString()
    $nativeEvent=[AfterhourEvents]::GetDefinition($e.Id)
    Check ($null -ne $nativeEvent -and [ModHelper]::IsAfterhourEventActive($e.Id)) 'Selected event is a real active native afterhour event'
    Check ([AfterhourEvents]::GetDaysLeft($e.Id) -eq 1) 'Regional event lasts only the current day'
    $startingChance=if($e.Region.ToString() -eq 'BlackMarket' -and (Get-Station 'BlackHasCards')){0.015}else{0.01}
    Close-To (Invoke-Station 'PreciousChance' @($o)) ([Math]::Min(0.5,$startingChance*(1+$e.PreciousBonus/100))) "Actual valuable odds include the native event modifier: $($e.Key)"
    $condition=$gameType.GetMethod('Condition',$privateStatic).Invoke($null,[object[]]@($e.Region))
    $baseRisk=[StationScavenging.StationRules]::DetectionChance($e.Region,0,$condition)
    Close-To (Invoke-Station 'DetectionChance' @($o)) ([Math]::Max(0.0,[Math]::Min(0.4,$baseRisk+$e.DetectionPoints/100))) "Actual detection odds include event and patrol: $($e.Key) condition=$condition base=$baseRisk points=$($e.DetectionPoints)"
    Check ((Get-Station 'Night').Total -eq 10) 'Fun events do not multiply the shared search allowance'
    if($e.WeightBonus -gt 0){
        $mult=[StationScavenging.StationEvents].GetMethod('CategoryMultiplier',$privateStatic).Invoke($null,[object[]]@($e.Region,$e.Category))
        Close-To $mult (1+$e.WeightBonus/100) 'Actual ordinary category modifier matches the advertised effect'
        Check (@([StationScavenging.LootCatalog]::Entries($e.Region) | Where-Object {$_.Category -eq $e.Category}).Count -gt 0) 'An event weight bonus affects an item category that exists in the destination'
    }
    foreach($i in 1..3){Check (Spend $o) 'Event still spends one shared attempt'}
    $before=Invoke-Station 'PreciousChance' @($o)
    Invoke-Station 'Reload' | Out-Null
    $reloaded=Outing ($e.Region.ToString())
    Close-To (Invoke-Station 'PreciousChance' @($reloaded)) $before 'Reload never rerolls or stacks an active event bonus'
    Check ((Get-Station 'Night').Used -eq 3) 'Reload preserves event-night spent attempts'
    foreach($r in @('Upper','Security','Revolution','BlackMarket')){
        $owned=@([AfterhourEvents]::GetActive([StationScavenging.StationRules]::LocationId($r)) | Where-Object source -eq 'station_scavenging')
        Check ($owned.Count -le 1) 'At most one authored event affects each region tonight'
    }
    [StoreStation]::Instance.dayCounter=151
    Invoke-Station 'Reload' | Out-Null
    $expected=[StationScavenging.StationEvents]::Select($e.Region,$eventSeed,151)
    $owned=@([AfterhourEvents]::GetActive([StationScavenging.StationRules]::LocationId($e.Region)) | Where-Object source -eq 'station_scavenging')
    Check ($owned.Count -eq $(if($null -eq $expected){0}else{1})) 'Next-day native events exactly match the new selection'
    if($owned.Count -gt 0){Check ($owned[0].id -eq $expected.Id) 'No previous event carries into the new selection'}
}
Close-To ([StationScavenging.StationEvents]::ApplyPrecious(0,50)) 0 'Event bonuses cannot bypass valuable cooldown'
Close-To ([StationScavenging.StationEvents]::ApplyPrecious(0.05,50)) 0.075 'Gala raises the final crate chance to 7.5%'
Close-To ([StationScavenging.StationEvents]::ApplyDetection('Lower',0.2,10)) 0 'Advanced detection effects never create a lower detection roll'
Activate-Event $store 'georgio_assistance' 151
$presentationEntry.OnDisable()
Check ([ModHelper]::IsAfterhourEventActive('georgio_assistance')) 'Unregistering regional events preserves native Georgio assistance'
Check (@([AfterhourEvents]::GetAll() | Where-Object source -eq 'station_scavenging').Count -eq 0) 'Disable removes all authored event definitions'
Mark-Group 'Twelve real native events, deterministic selection, advertised modifiers, reload/day transitions and native-event preservation'

$translations=@{}
foreach($locale in @('en','zh')){
    $translation=@{}
    foreach($line in Get-Content -LiteralPath (Join-Path $modDir "Localization\$locale.csv")){
        if([string]::IsNullOrWhiteSpace($line)){continue};$comma=$line.IndexOf(',');Check ($comma -gt 0) 'Every localization line has a raw native-compatible key'
        $key=$line.Substring(0,$comma)
        $value=[ModLoader].GetMethod('ParseCsvValue',$privateStatic).Invoke($null,[object[]]@($line.Substring($comma+1)))
        $translation[$key]=$value
    }
    $translations[$locale]=$translation
    foreach($e in [StationScavenging.StationEvents]::Catalogue){foreach($suffix in @('_title','_desc')){Check ($translation.ContainsKey('event_'+$e.Key+$suffix)) 'Every event has a localized title and story'}}
    foreach($r in 1..4){foreach($reason in @('day','rep')){foreach($v in 0..1){Check ($translation.ContainsKey("taunt_${r}_${reason}_$v")) 'Day and reputation banter is fully localized'}}}
    foreach($v in 0..1){Check ($translation.ContainsKey("taunt_4_exchange_$v")) 'Black Market exchange banter is localized'}
    foreach($key in @('status_exhausted','status_locked','status_caught','status_visited','effect_precious','effect_risk_up','effect_risk_down','effect_weight','event_arrival','unlock_requirements','unlock_exchange','reputation_existing_access')){Check ($translation.ContainsKey($key)) 'All new UI and modifier explanations are localized'}
}
Check ($translations.zh.status_exhausted -eq '拾荒次数已用尽') 'Requested Chinese exhaustion wording is exact'
foreach($value in $translations.en.Values){Check ($value -notmatch '[\u4e00-\u9fff]') 'English localization contains no untranslated Chinese'}
Mark-Group 'Actual native CSV parsing, full Chinese/English event and taunt coverage, exact exhausted wording'
