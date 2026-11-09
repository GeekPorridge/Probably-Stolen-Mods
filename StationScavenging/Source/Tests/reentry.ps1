# Runs inside test.ps1's isolated native-assembly process. Exercise the actual
# lower location's arrival and departure methods without creating Unity UI.
$enabledField=$assembly.GetType('StationScavenging.ModEntry').GetField('Enabled',$privateStatic)
$wasEnabled=$enabledField.GetValue($null)
$enabledField.SetValue($null,$true)
$store=New-TestRun 130
$probe=[Activator]::CreateInstance($probeAssembly.GetType('LowerBridgeProbe'))
$bridge=[Activator]::CreateInstance($bridgeType,$privateInstance,$null,[object[]]@($probe),$null)
$enterLower=$bridgeType.GetMethod('EnterNative',$privateInstance)
$baseLocation=$assembly.GetType('StationScavenging.ScavengingLocation')
$activeOuting=$baseLocation.GetField('state',$privateInstance)
$lower=Outing 'Lower'
$activeOuting.SetValue($bridge,$lower)
[void]$enterLower.Invoke($bridge,$null)
Check (Spend $lower) 'First lower visit spends an action'
Check (Spend $lower) 'Second lower search spends an action'
$upper=Outing 'Upper';Check (Spend $upper) 'Another region uses the same pool before returning'
$lower.RecordValue(123)
Invoke-Station 'RecordPreciousFind' @($lower) | Out-Null
$bridge.Leave()
Check ($lower.Ended -and (Get-Station 'Night').Remaining -eq 7) 'Leaving ends the visit without ending its shared night'
Check ($null -eq $assembly.GetType('StationScavenging.StationFlavor').GetMethod('StatusKey',$privateStatic).Invoke($null,[object[]]@([StationScavenging.Region]::Lower))) 'Lower transit card does not impose an advanced-region visited lock'
Check ($lower.Ended) 'Reading a lower card never resumes the departed visit'
Check (-not (Spend $lower)) 'A departed lower visit cannot search before a real re-entry'
[void]$enterLower.Invoke($bridge,$null)
Check (-not $lower.Ended) 'REGRESSION: second lower arrival resumes a voluntary departure'
Check ($null -eq (Invoke-Station 'BlockReason' @($lower))) 'Second lower arrival keeps the search button usable with actions left'
Check ((Get-Station 'Night').Used -eq 3 -and $store.scavengingAttempts -eq 3) 'Native reset on repeat entry restores the saved shared count'
Check ($lower.Searches -eq 2 -and $lower.PreciousFinds -eq 1 -and $lower.FoundValue -eq 123) 'Repeat entry retains regional searches, valuable discoveries and value'
Check ((Get-Station 'PendingPreciousDay') -eq 130 -and (Get-Station 'PreciousAvailable')) 'Repeat entry does not settle or erase the current valuable-find night'
Check (Spend $lower) 'Second lower visit can spend one of its remaining actions'
$bridge.Leave()
Invoke-Station 'Reload' | Out-Null
$lower=Outing 'Lower';$activeOuting.SetValue($bridge,$lower)
Check ($lower.Ended -and $lower.Searches -eq 3) 'Saved voluntary departure survives reload before entering'
[void]$enterLower.Invoke($bridge,$null)
Check (-not $lower.Ended -and (Get-Station 'Night').Remaining -eq 6) 'A saved same-night lower departure resumes without refilling actions'
Check ($lower.PreciousFinds -eq 1 -and $lower.FoundValue -eq 123) 'Legacy saved lower visit retains discoveries and value after resuming'
foreach($i in 1..3){$bridge.Leave();[void]$enterLower.Invoke($bridge,$null)}
Check ((Get-Station 'Night').Used -eq 4 -and $store.scavengingAttempts -eq 4) 'Repeated empty visits never consume or restore searches'
foreach($i in 1..6){Check (Spend $lower) 'Resumed lower visit can finish the remaining shared pool'}
$bridge.Leave();[void]$enterLower.Invoke($bridge,$null)
Check (-not (Spend $lower)) 'Returning to exhausted lower grounds never grants a fresh allowance'
Check ((Invoke-Station 'BlockReason' @($lower)) -eq 'exhausted') 'Exhausted repeat entry explains the actual search limit'
Check ($store.scavengingAttempts -eq 10) 'Exhausted re-entry preserves the native counter'

$store=New-TestRun 131
$lower=Outing 'Lower';$activeOuting.SetValue($bridge,$lower)
[void]$enterLower.Invoke($bridge,$null)
Check (Spend $lower) 'New day starts a new lower visit'
$bridge.Leave()
Invoke-Station 'MarkCaught' @((Outing 'Upper')) | Out-Null
[void]$enterLower.Invoke($bridge,$null)
Check ($lower.Ended -and -not (Spend $lower)) 'A global caught lock cannot be bypassed by re-entering lower grounds'
Check ((Get-Station 'Night').Caught -and (Get-Station 'Night').Remaining -eq 0) 'Caught state remains saved across repeat entry'

$store=New-TestRun 132
$lower=Outing 'Lower';$activeOuting.SetValue($bridge,$lower)
Check (Spend $lower) 'Search before sleep'
$bridge.Leave();Invoke-Station 'CompleteNight' | Out-Null
[void]$enterLower.Invoke($bridge,$null)
Check ($lower.Ended -and -not (Spend $lower)) 'A settled night is never reopened by an arrival callback'

$store=New-TestRun 133
$lower=Outing 'Lower';$activeOuting.SetValue($bridge,$lower)
Check (Spend $lower) 'Search before injury'
$bridge.Leave();$store.healthData.isWoundedFresh=$true
[void]$enterLower.Invoke($bridge,$null)
Check ((Invoke-Station 'BlockReason' @($lower)) -eq 'wounded') 'Repeat entry keeps native fresh-injury restrictions'
Check ((Get-Station 'Night').Used -eq 1) 'Blocked repeat entry does not alter the allowance'
$upper=Outing 'Upper';Check (End-Outing $upper 'Voluntary') 'Advanced voluntary departure remains distinct'
[void]$enterLower.Invoke($bridge,$null)
Check ($upper.Ended) 'Lower compatibility does not reopen advanced destinations'

$store=New-TestRun 134
$lower=Outing 'Lower';$activeOuting.SetValue($bridge,$lower)
$bridge.Leave();$store.storeState=[PlayerStore+StoreState]::MORNING
[void]$enterLower.Invoke($bridge,$null)
Check ($lower.Ended) 'An out-of-order morning callback cannot resume a departed lower visit'
Mark-Group 'Actual lower re-entry, legacy departed saves, shared actions, finds, value, exhaustion, injury and caught/settled lockouts'
$enabledField.SetValue($null,$wasEnabled)
