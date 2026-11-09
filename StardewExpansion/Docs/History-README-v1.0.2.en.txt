A Fellow Townsman Arrives v1.0.2 — Full release

For Probably Stolen; built against the locally installed game assemblies.
Formerly Hydroponics & Brewing. Chinese title: 老乡来了. Internal version: 1.0.2. The original mod ID and installation directory remain, so existing saves can be updated directly.

Install Mods/PelicanCultivation in the game's Mods directory. Restart the game and enable it in the native mod manager before loading a save.
Station Scavenging and Metal Wine Bottles are optional. The Station Scavenging bridge targets version 1.5.2 and declares the native reflection-external capability to connect to its precious-loot cooldown.

The Fellow Townsman visits once every Wednesday and once every Saturday, independently of the retired farmer and his unlock state. The first week's visits are on operating days 3 and 6, repeating every seven days. This version sells seeds.
Per-visit stock stays unchanged, giving two restocking opportunities each week. Reloading or regenerating visitors on the same day cannot duplicate his visit or reset sold stock. Saved visits queued on an old off-day are reconciled to the new schedule; an ongoing trade is kept. Visit counts and gift records remain intact, and the gift still uses the third actual visit.
Basic stock per visit: 3 parsnip, 6 potato, 3 corn, 6 wheat.
Plant gene editing unlocks an additional 2 hops, 2 strawberry and 2 starfruit seeds. Ancient fruit seeds are never sold.
Stock that does not fit stays pending for the current visit. Make room while he is present to receive more offers. Pending stock does not carry over to the following visit.
All eight seed packets occupy one cell and fit the native hydroponic input slot.
Version 0.1.3 supplies the missing SEED category/tag, which native plant initialization does not add. New and saved seeds are repaired without resetting growth or genetics. Unpurchased seeds remain unusable under native ownership rules.

Automatic replanting is enabled by default for all seven native plants and eight added plants. Toggle Automatic replanting (native and added seeds) in this mod's Hydroponics settings section.
After harvest and cleanup, an empty input slot receives one matching seed, checking the machine's output first, then the main inventory, then storage. Ordinary containers are searched; seeds installed inside other machines and locked/unremovable seeds are excluded. Potato, wheat and starfruit still require existing purchased reserves because their harvests do not return seeds.
The actual seed's edited traits, deletions, heat and strain data are retained. A stack supplies only one independent seed. Perennial parents remain in place and do not consume a spare seed. Without an available seed, the machine waits. No free seeds are created. Native growth, power and water rules resume on the next production cycle.
Existing machines are checked on load and when the setting is enabled. Unknown old machines may infer their species from local output; they do not select arbitrary reserve seeds. Disabling the setting stops automatic transfers and leaves already planted crops intact.

Display all crops to attract native food buyers. Potato, corn, strawberry, starfruit and ancient fruit now use Small/Regular/Giant sizes: 1x1, 2x2 and 3x3 (1/4/9 cells), worth 0.5x/1x/3x the regular price. Strain modifiers remain and currency rounds to whole units.
Large is retired. Wheat, hops and parsnip are always Regular, two cells. Small/Giant use name prefixes; Regular uses the base name. Produce is inherently FOOD and has no magnifying-glass inspection. Added seeds do not need the redundant Edible trait.
The five variable crops and their seeds no longer show the mod-information section, including duplicate market rows. Native prices, tags, food and seed genetics remain. Growing guidance stays on hydroponic machines and brewing previews on bottles.

Care and giants
High-speed machines charge only remaining growth work. Harvest care normalizes native health loss to a 20-work cycle. Ordinary care gives at most one or two small crops, with most regular; severe neglect increases the small share. Care >=95 and effective quality >=50% give regular crops before the giant roll.
Giants require harvest care >=95 and effective quality >=50%, with 10% chance per qualified batch and at most one. A giant replaces an existing crop without increasing total yield.
After ten qualified misses on the same machine and species, the next qualified harvest guarantees one giant. Random success resets that counter; unqualified or fruitless harvests neither advance nor reset it. Separate machine/species counters survive reloads.
For six crops: ordinary care gives five regular and one small; premium care gives six regular; a successful giant roll gives five regular and one giant.
Default mineral ranges are 0.5%–2.5% for basic crops and 1%–3% for advanced crops. Pure water without minerals still reduces care; other pollutants also cause losses. Hydroponic tooltips retain the actual strain's mineral range, water risks, effective quality, care and guarantee progress.

Brewing scores
Condition scores remain Damaged -1, Poor 0, Good +1 and Excellent +2. Care 21–49 yields 85% Good/15% Poor; 50–84 yields 75% Good/25% Excellent; 85–100 yields 40% Good/60% Excellent. Care 0–20 yields 50% Damaged/35% Poor/15% Good.
Wine score = both condition scores + both size points + paired ingredients 1 + yeast 1 if present + native water score (-3 to +1). Small/Regular add zero size points; Giant adds +2 per crop. Each crop still counts as one ingredient, and matching species may mix sizes.
Size adds score only, with no extra wine price multiplier. Native quality caps at Master Vintage for 6+ points. Two Good Regular crops with yeast and clean water give 5 (Reserve), while two Good Giant give 9 (Master Vintage). The bottle preview shows the breakdown and expected tier.
Ancient fruit seeds can be discovered from day 15 onward. Rare discoveries remain possible after the first seed.
Without Station Scavenging, the default chance is 1% per native dumping-ground item roll.
With Station Scavenging, ancient seeds take 20% of lower-level core valuable outcomes. This is a share within the valuable outcome, not a 20% chance on every search. The original valuable odds and shared three-night cooldown remain in effect. Both settings are configurable.
On the farmer's third actual visit, counted from his first visit in the current save, he gives one ancient seed if you have never obtained one. This gift is given only once. Pending delivery is retained if there is no room. Existing saves count this new NPC's visits from the first one after installation.

Place an ancient seed in a native hydroponic unit to reveal the Harvest Mode button.
Regular harvest: all fruit, no seeds.
Breeding harvest: half the fruit, rounded down, plus seeds that inherit the parent's current traits. The initial strain yields 3 fruit and exactly 1 seed. Seed yield is calculated once for the whole batch, with native modifiers applied once and any fractional seed yield rolled once. Removed traits stay removed; editing heat is not inherited. Perennial parents continue fruiting.

The five added finished brews cannot be poured into liquid containers. Rejected drag/pour actions preserve the bottle, score, aging, value and target contents. Empty-bottle filling, fermentation, drinking, intentional drain use and native-only wines retain their original behavior. Wine already poured and mixed is not reconstructed.
Brewing uses native empty wine bottles, 750 ml of water, exactly two matching crops, and at most one optional native yeast. All sizes count as one ingredient each. Mixed recipes are rejected without consuming the inputs.
The ingredient grid expands from 6x2 (12 cells) to an 8x3 outline with the bottom-right two cells blocked, giving exactly 22 usable cells. Original ingredient coordinates remain usable. Two 3x3 giant crops fit beside a 2x2 yeast area. Internal expansion does not alter the bottle's external footprint.

Brew                Base price    Work    Natural / Ordinary / Super yeast
Wheat Beer               60          4             4 / 2 / 1 days
Pale Ale                100          6             6 / 3 / 2 days
Strawberry Wine         120          6             6 / 3 / 2 days
Starfruit Wine          320          8             8 / 4 / 2 days
Ancient Fruit Wine      200         10            10 / 5 / 3 days

Base prices are Homebrew values. Native condition, water, paired-input and yeast bonuses plus the added crop-size points determine quality. Illegal yeast retains the native counterfeit-wine behavior.
Wheat Beer and Pale Ale enter a powered fridge and gain the native Chilled feature (+20% value) at its daily cycle. Both zones follow native drink chilling, with no stacking from repeated cycles. Cold expiry after removal follows native rules. Drinking or emptying clears the chilled feature. The two beers no longer age or enter the wine ager.
Only the three added fruit wines (Strawberry, Starfruit and Ancient Fruit) use the native ager and aging value gains, without changing quality. They do not receive the fridge bonus. Native-only wines retain their original behavior. Saved beer retains its historical age and value but stops gaining more; beer already stored in an ager can be removed and will not be processed.
Beer retains its 2-by-3 footprint and can be rotated to fit the default chill zone. Clear room or upgrade fridge storage when needed.
Ancient Fruit Wine now has a light-blue bottle, retaining its dark teal neck, fruit label and slender silhouette. Wheat Beer and Pale Ale occupy 2-by-3 cells (six cells). The three fruit wines occupy 1-by-4 cells. Uniformly scaled original artwork retains its proportions and 128 texture pixels per logical cell. Native empty and fermenting bottles retain their native rules. Finished beer checks inventory capacity before expanding, safely relocates when needed, and keeps its previous footprint/value temporarily if every area is full. It retries when room is available. Successfully brewed strawberry, starfruit and ancient-fruit wines have the Substance and Luxury Item categories. Failed quality retains native handling.

Existing saves: after restarting the game, load your save to repair seed categories and refresh artwork, names and descriptions. Genetics, growing progress, aged prices, stock records and visit counts are retained. The visitor's save identifier is unchanged. His front-facing portrait is now 80% of its 0.1.2 display size.
Version 0.1.8 retains seed edits, heat and progress. Saved Small crops shrink to one cell, old Large becomes Regular (four cells, regular price/calories), Giant remains nine cells. Legacy IDs remain loadable. Price/calorie migration runs once and preserves strain modifiers and historical condition/care. The three fixed crops remain Regular, two cells.
Expanded crops check placement, try relocation or main/storage inventories, and retain their old footprint with a waiting hint if every area is full. They retry when space is available. Bottle expansion now uses the native body-storage API to reach the grid inside its machinery intake wrapper, fixing the failed direct cast in 0.1.7. Existing empty bottles expand to 22 cells while preserving ingredients. Size scores apply only to fermentation started after the update; fermenting/finished wines retain their prior scores, tiers and aged value.

Verified: compilation; 15,545 logic checks; 182 native Harmony integration checks; 57 native-calendar integration checks; native mod scan; 107 bilingual keys/placeholders; 39 transparent sprites. The native intake-wrapper regression reproduces the former failed cast and verifies correct body-storage traversal. Three footprints, retired-Large migration, targeted mod-info suppression, giant guarantees, score-only brewing and prior regressions passed. Artwork proportions and visible growth passed for 18 active crop exports.
The actual native fridge decisions, ager inventory filter, aging callbacks, Chilled +20% feature, non-stacking and empty-bottle cleanup passed. The headless fixture replaces only engine-dependent mechanic text lookup; device rules and value calculations remain native. Native pour-callback regressions passed. All 39 sprites match v1.0. The game was not controlled for in-game verification. Full initial trait effects are in Seed-traits.en.txt.
Hydroponic growth illustrations reuse native plant visuals. Produce, seed packets, all five bottles and the visitor use new artwork.

Artwork was generated with built-in imagegen, then mechanically sliced and resized. Selected originals, prompts and the asset preparation script are included in the source archive. Large originals and documentation are excluded from the runtime package.
