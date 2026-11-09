Metal Wine Bottles v0.2.1
Author: 小粥

Turn two metal ingots into native empty wine bottles.
Use one owned metal ingot on another through the game's normal item-use interaction.
Each batch consumes one unit from each of the two selected items. The recipe has no other cost or processing timer.

PURITY AND OUTPUT
Native grades: Very Low 1, Low 2, Fair 3, High 4, Very High 5, Perfect 6.
Average the two true grades and round down, then use this output table:
Very Low: 1 bottle
Low: 3 bottles
Fair: 5 bottles
High: 7 bottles
Very High: 9 bottles
Perfect: 10 bottles

Examples:
Perfect 6 + Low 2 = High 4: 7 bottles.
Perfect 6 + Very Low 1 = Fair 3: 5 bottles.
Perfect 6 + Perfect 6 = Perfect 6: 10 bottles.
Use two distinct items; an ingot cannot target itself. If another mod adds stacks, one unit is taken from each selected stack.
The public or counterfeit purity label does not determine output.

INVENTORY SPACE
Keep enough free room for the entire batch before crafting.
Bottles go into the target ingot's inventory first, then the source inventory if it is different.
Existing inventory acceptance rules are respected.
If the complete batch cannot fit, temporary outputs are rolled back, both ingots are retained, and a message asks you to clear space.

SETTINGS AND LANGUAGE
Each grade's yield is adjustable from 1 to 50 in the mod settings.
Version 0.2.1 keeps the settings introduced in 0.2.0, including custom values.
In-game tips and setting labels follow the selected game language: Simplified Chinese or English. Other languages use the native English fallback.
The mod-list name and summary are bilingual because the native manifest has one name and one description field.

INSTALLATION
Copy Mods/MetalWineBottles into the game's Mods folder. Enable the mod in the mod menu and restart the game.
Compatibility target: Probably Stolen 0.46D. No other mod is required.
Existing ingots receive the interaction when the save is loaded.
When upgrading from 0.1.0, first remove any ingots left in a furnace input inventory, then replace the files and restart.
Ingots keep their original upgrade and expansion interactions.
To uninstall, disable the mod and restart. Crafted bottles are native items and remain usable.
Mods replacing the ingot factory or ingot-to-ingot interaction may require compatibility testing.

BALANCE AND SCOPE
Furnaces supply the ingots through the normal production chain, with the original material, equipment, power and quality-module investment.
Ten ingots allow five crafting batches, with output determined by mixed purity.
Selling or upgrading with ingots remains an alternative use of those resources.
Under ordinary base values, the default empty-bottle output is worth less than the two ingots for every purity combination. This comparison excludes customer preferences, negotiation, retail markup, events and other mods.
Wine ingredients, ratings, fermentation, aging and sales continue through the native systems.
This version contains no new crops, return-bottle system or cleaning mechanic.

VERIFICATION
Rule, transaction and native callback checks passed, together with the game's mod scanner.
In-game interaction, physical item placement and save/reload behavior still need a playtest.

DISTRIBUTION
The subscribed content contains only the mod DLL, manifest and two language files.
Documentation and Workshop artwork are kept outside the runtime folder.
No game, Unity or Harmony binaries are redistributed.
Independent, free release. Workshop item: https://steamcommunity.com/sharedfiles/filedetails/?id=3833937392
