Night Walker v1.1.0 — 小粥
Probably Stolen 0.46D. Simplified Chinese / English.

Adds the positive starting perk Night Walker: up to 3 outings per night, with
each destination visited once. Visits reset the next day. Normal destination
unlock, injury, lockdown and other restrictions still apply. Native visit
history prevents reloads, repeated shop closing or cancelling the map from
refilling spent outings. Supports vanilla destinations and mod destinations
using the shared travel flow, including Station Scavenging.

CONFIGURABLE STARTING COST
The default is 2 starting perk points. In the main menu, open Mods, select
Night Walker, then open its settings using the gear button. Choose 1 or 2
on the integer slider, Apply, and restart the game before starting a new run.
Settings are stored by the native configuration API and survive Workshop
updates. Restore Defaults followed by Apply restores 2 points.
The cost does not retroactively refund existing saves or grant an unselected
perk. Existing Night Walker outing effects are unchanged. Three outings do
not multiply Station Scavenging's shared nightly search allowance.

INSTALL / BUILD
Merge Release/Mods into the game folder, enable the mod in the game menu,
and restart. Workshop subscriptions also need enabling and a restart.
Build with PowerShell 7: pwsh -File ./build.ps1 -GameDir "your game folder"
Checks: pwsh -File ./Tests/test.ps1
Game assemblies are referenced locally and are not redistributed.
