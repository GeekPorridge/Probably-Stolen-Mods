using System;
using System.Collections.Generic;

namespace StationScavenging
{
    public enum LootCategory { Waste, Material, Household, Food, Tool, Medical, Substance, Weapon, Ammo, Module, Luxury }
    public enum SpawnStyle { Normal, PoorHandmade, PoorHeavyHandmade, PoorPolice, Stolen }

    public sealed class LootChoice
    {
        public readonly string ItemId;
        public readonly double OrdinaryWeight;
        public readonly double FocusedWeight;
        public readonly LootCategory Category;
        public readonly int MinimumDay;
        public readonly SpawnStyle Style;

        public LootChoice(string id, double perimeter, double deep, LootCategory category, int minimumDay = 1, SpawnStyle style = SpawnStyle.Normal)
        {
            ItemId = id;
            OrdinaryWeight = perimeter;
            FocusedWeight = deep;
            Category = category;
            MinimumDay = minimumDay;
            Style = style;
        }
    }

    public static class LootCatalog
    {
        public const string LowerCrateTable = "station_scavenging.lower_service_crate";
        public const float LowerCrateWeight = 1000f / 99f;
        private static LootChoice E(string id, double p, double d, LootCategory c, int day = 1, SpawnStyle style = SpawnStyle.Normal)
        {
            return new LootChoice(id, p, d, c, day, style);
        }

        private static readonly LootChoice[][] Pools =
        {
            // Lower Levels rolls the native group, extended by the service-crate table.
            new LootChoice[0],
            new[]
            {
                E("bandage_item", 15, 10, LootCategory.Medical), E("caffeine_pill", 12, 10, LootCategory.Medical),
                E("red_beer", 16, 16, LootCategory.Substance), E("packet_red_cigarette", 4, 10, LootCategory.Substance),
                E("hemostatic_bandage_item", 5, 12, LootCategory.Medical), E("rubbing_alcohol", 3, 10, LootCategory.Medical),
                E("nudka", 0, 4, LootCategory.Substance), E("blank_module", 0, 3, LootCategory.Module),
                E("random_performance_module", 0, 1.2, LootCategory.Module, 25),
                E("random_efficiency_module", 0, 1.2, LootCategory.Module, 25),
                E("skincare_cream", 0, 0.5, LootCategory.Luxury, 30)
            },
            new[]
            {
                E("small_pistol_ammo", 23, 22, LootCategory.Ammo),
                E("heavy_pistol_ammo", 10, 16, LootCategory.Ammo), E("cup_noodle", 15, 12, LootCategory.Food),
                E("cat_bar", 10, 8, LootCategory.Food), E("toilet_paper", 8, 5, LootCategory.Household),
                E("paper_towel", 6, 5, LootCategory.Household), E("shampoo", 3, 4, LootCategory.Household),
                E("handmade_pistol", 0, 2.5, LootCategory.Weapon, 15, SpawnStyle.PoorHandmade),
                E("heavy_handmade_pistol", 0, 1, LootCategory.Weapon, 25, SpawnStyle.PoorHeavyHandmade),
                E("glock_receiver", 0, 0.5, LootCategory.Weapon, 35, SpawnStyle.PoorPolice)
            },
            new[]
            {
                E("small_pistol_ammo", 25, 22, LootCategory.Ammo),
                E("heavy_pistol_ammo", 14, 20, LootCategory.Ammo), E("screwdriver", 18, 18, LootCategory.Tool),
                E("wire_cutter", 12, 18, LootCategory.Tool), E("welder", 0, 3, LootCategory.Tool),
                E("handmade_pistol", 0, 4, LootCategory.Weapon, 18, SpawnStyle.PoorHandmade),
                E("heavy_handmade_pistol", 0, 1.5, LootCategory.Weapon, 28, SpawnStyle.PoorHeavyHandmade),
                E("glock_receiver", 0, 0.4, LootCategory.Weapon, 40, SpawnStyle.PoorPolice)
            },
            new[]
            {
                E("dream_dust", 26, 28, LootCategory.Substance), E("nightmare_dust", 8, 15, LootCategory.Substance),
                E("red_beer", 8, 10, LootCategory.Substance, 1, SpawnStyle.Stolen),
                E("shampoo", 6, 8, LootCategory.Household, 1, SpawnStyle.Stolen),
                E("packet_red_cigarette", 3, 8, LootCategory.Substance, 1, SpawnStyle.Stolen),
                E("common_electronic", 1, 7, LootCategory.Material, 1, SpawnStyle.Stolen),
                E("nudka", 0, 2, LootCategory.Substance, 1, SpawnStyle.Stolen)
            }
        };

        private static readonly LootChoice[] Cards =
        {
            E("blank_keycard", 40, 40, LootCategory.Tool), E("ser_keycard", 20, 20, LootCategory.Tool),
            E("eng_keycard", 16, 16, LootCategory.Tool), E("med_keycard", 16, 16, LootCategory.Tool),
            E("sci_keycard", 6, 6, LootCategory.Tool), E("sec_keycard", 8, 8, LootCategory.Tool),
            E("cmd_keycard", 1, 1, LootCategory.Tool)
        };

        public static IList<LootChoice> Entries(Region region) { return Pools[(int)region]; }
        public static IList<LootChoice> Keycards { get { return Cards; } }

        public static string CrateId(Region region)
        {
            switch (region)
            {
                case Region.Upper: return "loot_crate_medical";
                case Region.Security: return "loot_crate_security";
                case Region.Revolution: return "loot_crate_engineering";
                case Region.BlackMarket: return "loot_crate_evidence";
                default: return "loot_crate_service";
            }
        }

        public static long FixedRiskValue(string id)
        {
            switch (id)
            {
                case "loot_crate_service": return 400;
                case "loot_crate_medical": return 650;
                case "loot_crate_security": return 650;
                case "loot_crate_engineering": return 650;
                case "loot_crate_evidence": return 800;
                case "blank_keycard": return 15;
                case "ser_keycard": return 80;
                case "eng_keycard": case "med_keycard": return 150;
                case "sci_keycard": case "sec_keycard": return 200;
                case "cmd_keycard": return 300;
                default: return -1;
            }
        }

        public static double Weight(LootChoice entry, LootProfile mode, int day, SupplyCondition condition, double junkScale = 1)
        {
            if (day < entry.MinimumDay) return 0;
            double w = mode == LootProfile.Focused ? entry.FocusedWeight : entry.OrdinaryWeight;
            if (entry.Category == LootCategory.Waste) w *= Math.Max(0, junkScale);
            if (condition == SupplyCondition.Household && (entry.Category == LootCategory.Household || entry.Category == LootCategory.Food || entry.Category == LootCategory.Medical || entry.Category == LootCategory.Substance)) w *= 1.5;
            if (condition == SupplyCondition.Equipment && (entry.Category == LootCategory.Tool || entry.Category == LootCategory.Material || entry.Category == LootCategory.Ammo || entry.Category == LootCategory.Module)) w *= 1.5;
            return w;
        }

        public static LootChoice Pick(IList<LootChoice> entries, LootProfile mode, int day, SupplyCondition condition, double roll, double junkScale = 1, Func<LootCategory, double> categoryMultiplier = null)
        {
            double total = 0;
            foreach (var entry in entries) total += Weight(entry, mode, day, condition, junkScale) * (categoryMultiplier == null ? 1 : Math.Max(0, categoryMultiplier(entry.Category)));
            if (total <= 0) return null;
            double target = Math.Max(0, Math.Min(0.999999999999, roll)) * total;
            LootChoice last = null;
            foreach (var entry in entries)
            {
                double weight = Weight(entry, mode, day, condition, junkScale) * (categoryMultiplier == null ? 1 : Math.Max(0, categoryMultiplier(entry.Category)));
                if (weight <= 0) continue;
                last = entry;
                target -= weight;
                if (target < 0) return entry;
            }
            return last;
        }
    }
}
