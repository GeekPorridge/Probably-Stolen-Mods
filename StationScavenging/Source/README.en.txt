Station Scavenging v1.5.5 — 小粥
Probably Stolen 0.46D / release 1.0.0.12 (VERSION 100-Rev12). Simplified Chinese / English.

v1.5.5: PRESERVE BETTER SCAVENGING'S ORIGINAL BOX RULES
Battered Wooden Crates and Pristine Metal Cases retain their author's Lower
drop odds, electronic glove bonuses, contents and expedition rules.
They are separate from Station core valuables: finding them never starts or
extends core rest, and they remain available during Station's core rest nights.
Native Service crates and advanced Station core valuables keep their existing
shared rest. The v1.5.4 optional-box core-pool and roll-interception code is removed.
Same-night Lower re-entry fixes, shared remaining actions, reloads, endurance,
extra outings, XP and glove search bonuses remain supported.
Original BetterScavenging 1.13.0 and ScavengerSkills 1.0.7 DLLs passed 168 isolated
integration checks; all 12477 existing regression checks passed.
Unity scene/RNG/audio/rendering/placement use isolated boundaries; no manual
in-game save-loading playthrough was performed.

v1.5.3: REPEAT LOWER-VISIT COMPATIBILITY
When another mod grants a second Lower Levels trip, remaining shared searches
can be used there. Voluntary departure ends the visit without permanently
disabling the Lower grounds for that night; saved departed visits also recover
on arrival. Lower transit cards no longer show the advanced-region visited lock.
Used actions, valuable discoveries/rest, fresh injuries, capture locks and settled
nights remain intact. Native search-count bonuses continue contributing to the
single five-region pool; Georgio's bonus is applied once. Extra-outing mods still
control actual travel opportunities. Returning never refills the search pool.
Night Walker's once-per-destination rule and advanced-region departure limits
remain effective. Fixes the reported second-Lower-entry failure when paired with
Scavenging Expansion or Better Scavenging.

v1.5.2: SHARED VALUABLE REST INCLUDING LOWER SERVICE CRATES
Service crates now trigger and obey the same rest as advanced crates, containers
and keycards. Repeat finds remain possible during the discovery night; rest starts
after that night ends and lasts three complete following nights by default.
Example: find a Service crate on night20, rest across all five regions on nights
21-23, recover on night24. Regular searches and native Lower injuries remain active.
The Lower card also shows the rest notice. Retained legacy Lower-find records
migrate once using their original discovery dates, preserving the active find
night and existing loot. Expired rest is not restarted on the upgrade date.
Historical dates no longer present in the save are not guessed from inventory.

v1.5.1: TRANSIT PRESENTATION AND PACKAGE SIZE
Complete page text before Canvas layout instead of adding it after a 0.2-second
poll. Cache stable strings, retain native scroll containers, release old page
bindings, and keep text intact during closing animations. Existing gameplay and
loot balance are unchanged. PNG assets total 17,242,272 -> 3,508,103 bytes (-79.65%).
Backgrounds retain 1672x941 resolution; covers use 592x333, about twice the native
card display size. Indexed 256-colour PNGs approximate colours slightly. Original
art remains in the v1.5.0 art gallery and historical source archive.

REGIONAL ART, ACCESS BANTER AND EVENTS
Four original cover illustrations and four separate in-game backgrounds replace
the borrowed Lower art: upper banquet service gallery, security impound, hidden
resistance workshop and a Black Market alley. Backgrounds fill the camera while
preserving aspect ratio and point-sampled pixels. Lower retains native artwork.
Locked destinations are visible with regional banter and precise day/reputation/
exchange requirements. Previously earned permanent access remains valid after a
reputation drop, accompanied by an appropriate remark. Exhaustion explicitly says
"No scavenging attempts left". Other native destinations retain their statuses.
Every new notice, event, story, numeric effect and notification is localized.

Each advanced region has about a 40% chance of one authored daily event, otherwise
a normal night. Twelve events cover elite parties, audits, bathhouse turnover,
shift changes, stocktaking, camera faults, supply relays, slogan rehearsals,
password mishaps, auctions, blackouts and an informant selling the same tip twice.
They are native afterhour events, shown before departure with duration and exact
effects. Save seed/day/region determine the choice; reloads do not reroll or stack
it. Events last one day, do not add searches, and adjust valuable odds, detection
percentage points or regular category weights. They are new mod stories inspired
by the native setting, not claims of existing native quests.
Example: Champagne Upstairs gives x1.5 valuable odds, -1 percentage point detection
and x1.25 regular medical weight. Without patrol, default crate odds are 1.5%-7.5%
and detection is 0%-5%. The live panel includes all modifiers. Final detection is
bounded to 0%-40%, core valuables to 0%-50%. Rest-night zero odds remain zero.
Category weights also affect regular scanner extras, never advanced core extras.
Native Lower/Georgio events are preserved. The following curves are BASE values
before authored regional-event effects.

All five destinations share TEN base searches per night. Native perk and event
modifiers still apply: Georgio's +5 means FIFTEEN with no other modifiers;
proficient +2, careful -2 and native disability/arthritis gates remain effective.
The mod reads the actual native event and does not restart or extend it.
Changing destinations, returning, reloading or re-enabling cannot refill searches.
Each effective attempt costs one, including injury, bad luck or capture. Packing,
opening containers and tool/scanner extras do not cost additional attempts.

Next-search progress = searches already used / (tonight's total - 1), clamped
to 0..1. The first action uses starting odds; the 10th normally, or 15th with
Georgio, uses endpoint odds. Perk-adjusted totals use the same relative curve.
All five crate/locker rates rise from 1% to 5%. This also replaces the previous
fixed 5% Lower service-crate roll. Black Market card nights rise 1.5% to 7.5%.
Odds are captured BEFORE spending the action, so the final allowed action can
still yield a core valuable. Time spent viewing or packing never changes odds.

Normal per-action detection: Upper 1%-6%; Security 2%-7%; Revolutionaries and
Black Market 1.5%-6.5%. Patrol adds 2 percentage points. Detection is checked
before loot; displayed valuable odds are conditional on passing this check.
Capture applies existing penalties, immediately returns you with packed items,
loses ground items, and blocks scavenging in all five regions for that night.
Non-scavenging activities and native disposal/story visits remain available.

Lower Levels retains the native injury sequence, luck saves, bad-luck empty
searches, introductory immunity counters, flashlight, tools, extras, perks,
backpack discoveries, statistics and story callbacks. It has no detection roll.
The native injury formula reads the shared number already used that night.
Georgio's native injury immunity still protects Lower; it does not disable
detection in advanced regions. Fresh injury blocks further Lower scavenging.
Native loot helpers award Lower items. Only the added service-crate route is
reweighted to current progress, compensating for native junk-weight events and
preserving ordinary relative weights. Native scanner extras can find a second
service crate. Service crates now trigger and obey the shared five-region rest;
their route has zero weight during rest, with native ordinary weights unchanged.

The countdown is removed. All regions clone the native panel layout. The first
row shows the shared remaining/total searches, with Georgio's bonus when active.
Lower keeps the minor/major injury rows; advanced panels show valuable/detection
odds. Searches are immediate. Exhaustion disables searching without forcing a
return, leaving unlimited time to pack. Manual Return uses the native ground-item
confirmation. Canceling it does not end the outing or cost an action.
Night Walker retains destination/travel choices without multiplying searches.

Repeated core valuables can appear in one outing and across destinations in the
same night. Discoveries add no action cost, do not reset progress, and only start
the shared three-full-night rest after sleep/night settlement. Example: find on
night20; nights21-23 rest; night24 eligible. Lost/unpacked valuables still count.
Rest disables core loot in all five regions, preserving regular loot and native
Lower injury/luck checks.
Native mod-save APIs preserve the budget, capture lock, outing values and pending
discoveries. Existing unlocks/cooldowns migrate; same-night timed searches count
against the new shared budget. Morning loads do not close the coming evening.

Advanced direct/scanner pools exclude junk, glass_shard and empty_beer_bottle.
The 70/30 split mixes two regular-weight profiles, not regular/core loot. Weapons,
modules and luxuries remain in regular pools. Advanced scanner extras are regular
only. Black Market locker/card direct drops remain mutually exclusive per night.
Native contents, locks, seven weighted card types and supply conditions remain.
Unlocks: Upper day10/rep5; Security day15/rep10; Revolution day18/rep12;
Black Market day20/rep15 plus underground exchange. IDs remain stable.

Penalties retain cumulative found value V: credits=min(200,ceil(20+0.25*V));
normal reputation=min(3,faction*(0.5+V/400)), with difficulty gain multiplier once.
Discarding/opening containers never reduces V. Advanced searches require enough
cash for the current penalty. Lower does not require penalty cash.

Exit the game; copy Release\Mods\StationScavenging over the game's mod folder;
restart. Same mod ID, no Harmony or extra dependencies. Options expose penalties,
rest nights and endpoint crate/card rates. Old duration options are ignored.
Return and save before disabling; disabling restores the original lower location
and removes the service-crate route. No player-save files are edited externally.
Build: pwsh -File .\build.ps1 -GameDir '<game folder>'
Check: pwsh -File .\Tests\test.ps1 -GameDir '<game folder>'
Optional -NightWalkerDll checks the actual Night Walker assembly. Checks use
isolated objects. Live UI, physical packing and long-term balance require
in-game testing.
