using System;

namespace StationScavenging
{
    // The native entry point hard-caps its own five-attempt pool. Retain its
    // outcome order and injury/luck/loot helpers under the shared night budget.
    internal static class LowerScavenging
    {
        internal static void Search(OutingState state, double crateChance)
        {
            var store = PlayerStore.Instance;
            bool minorImmune = store.dumpingGroundMinorWoundImmunitySearchesLeft > 0;
            bool majorImmune = store.dumpingGroundMajorWoundImmunitySearchesLeft > 0;
            try
            {
                if (!minorImmune && ScavHelper.RollMinorWound())
                {
                    if (HealthData.RollLuck()) Notify("mech_scav_luck_saved");
                    else
                    {
                        HealthData.ReceiveMinorWound();
                        RunStats.OnScavengeInjury(false);
                        AudioManager.Instance.Play2("debris_impact2");
                    }
                }
                else if (!majorImmune && ScavHelper.RollMajorWound())
                {
                    if (HealthData.RollLuck()) Notify("mech_scav_luck_saved");
                    else
                    {
                        HealthData.ReceiveMajorWound();
                        RunStats.OnScavengeInjury(true);
                        AudioManager.Instance.Play2("debris_impact");
                    }
                }
                else if (HealthData.RollBadLuck())
                {
                    StoreUIManager.Instance.Notify(LocHelper.GetLocalizedMechanic("mech_scav_bad_luck_empty"), "sec");
                    AudioManager.Instance.Play2("debris");
                }
                else
                {
                    UpdateCrateWeight(crateChance);
                    Notify("mech_scav_success");
                    GameItem backpack;
                    var items = ScavHelper.GetRandomScavengedItem(out backpack);
                    RunStats.OnScavengedItems(items);
                    foreach (var item in items)
                    {
                        if (item == null) continue;
                        if (item.identifier == LootCatalog.CrateId(Region.Lower)) StationGame.RecordPreciousFind(state);
                        GraphUtils.TryAcceptAll(EmporiumEntry.Instance.afterhourInventory, item);
                    }
                    if (backpack != null && backpack.parentInventory != null)
                    {
                        if (backpack.identifier == ScavHelper.BACKPACK_FIND_SATCHEL) store.dumpingGroundSatchelFound = true;
                        else if (backpack.identifier == ScavHelper.BACKPACK_FIND_KIDS) store.dumpingGroundKidsBackpackFound = true;
                    }
                    AudioManager.Instance.Play2("debris");
                }
            }
            finally
            {
                if (minorImmune) store.dumpingGroundMinorWoundImmunitySearchesLeft--;
                if (majorImmune) store.dumpingGroundMajorWoundImmunitySearchesLeft--;
                RunStats.OnScavengeAttempt();
            }
        }

        internal static void UpdateCrateWeight(double chance)
        {
            double ordinaryWeight = 0;
            double junkScale = Math.Max(0, 1 + ModHelper.GetAfterhourModifier(AfterhourLocation.DUMPING_GROUND, ScavHelper.MODIFIER_JUNK_CHANCE) / 100.0);
            foreach (var row in ModHelper.GetLootGroupEntries("dumpingGroundTG"))
            {
                if (row.id == LootCatalog.LowerCrateTable || row.weight <= 0) continue;
                ordinaryWeight += row.weight * (row.id == "junkTable" ? junkScale : 1);
            }
            // Match the shared curve even during native junk-weight events;
            // ordinary items retain their existing relative weights.
            chance = Math.Max(0, Math.Min(0.99, chance));
            float weight = (float)(ordinaryWeight * chance / (1 - chance));
            LootRegistry.SetGroupWeight(ModEntry.ModId, "dumpingGroundTG", LootCatalog.LowerCrateTable, weight);
        }

        private static void Notify(string key) { StoreUIManager.Instance.Notify(LocHelper.GetLocalizedMechanic(key)); }
    }
}
