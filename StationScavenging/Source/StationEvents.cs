using System;
using System.Collections.Generic;

namespace StationScavenging
{
    public sealed class RegionalEvent
    {
        public readonly Region Region;
        public readonly string Key;
        public readonly double PreciousBonus, DetectionPoints, WeightBonus;
        public readonly LootCategory Category;
        public string Id { get { return ModEntry.ModId + "." + Key; } }
        internal RegionalEvent(Region region, string key, double precious, double risk, LootCategory category = LootCategory.Waste, double weight = 0)
        { Region = region; Key = key; PreciousBonus = precious; DetectionPoints = risk; Category = category; WeightBonus = weight; }
    }

    public static class StationEvents
    {
        internal const string PreciousModifier = "station_scavenging.precious_bonus_pct";
        internal const string DetectionModifier = "station_scavenging.detection_points";
        private static readonly RegionalEvent[] events =
        {
            new RegionalEvent(Region.Upper, "upper_gala", 50, -1, LootCategory.Medical, 25),
            new RegionalEvent(Region.Upper, "upper_audit", -20, 2),
            new RegionalEvent(Region.Upper, "upper_baths", 20, 0, LootCategory.Medical, 40),
            new RegionalEvent(Region.Security, "security_shift", 25, -1, LootCategory.Food, 25),
            new RegionalEvent(Region.Security, "security_stocktake", 50, 2, LootCategory.Ammo, 35),
            new RegionalEvent(Region.Security, "security_cameras", 0, -1.5, LootCategory.Household, 35),
            new RegionalEvent(Region.Revolution, "revolution_relay", 40, 1, LootCategory.Ammo, 30),
            new RegionalEvent(Region.Revolution, "revolution_rehearsal", 15, -1, LootCategory.Tool, 35),
            new RegionalEvent(Region.Revolution, "revolution_password", -20, 1.5),
            new RegionalEvent(Region.BlackMarket, "black_auction", 50, 1, LootCategory.Substance, 25),
            new RegionalEvent(Region.BlackMarket, "black_blackout", 25, -1, LootCategory.Material, 35),
            new RegionalEvent(Region.BlackMarket, "black_informant", 20, 2)
        };
        public static IList<RegionalEvent> Catalogue { get { return Array.AsReadOnly(events); } }
        private static bool registered;
        private static PlayerStore syncedStore;
        private static int syncedDay = -1;

        public static RegionalEvent Select(Region region, int seed, int day)
        {
            if (region == Region.Lower) return null;
            if (StationRules.StableChoice(seed, day, 701 + (int)region, 100) < 60) return null;
            int index = StationRules.StableChoice(seed, day, 809 + (int)region, 3);
            return events[((int)region - 1) * 3 + index];
        }

        internal static void Register()
        {
            foreach (var data in events)
            {
                var e = data;
                var definition = new AfterhourEventDefinition
                {
                    id = e.Id, source = ModEntry.ModId, locationId = StationRules.LocationId(e.Region),
                    durationDays = 1, dailyChancePercent = 0,
                    title = () => ModEntry.L("event_" + e.Key + "_title"),
                    description = () => ModEntry.L("event_" + e.Key + "_desc")
                };
                if (e.PreciousBonus != 0)
                    definition.AddEffect(() => ModEntry.L("effect_precious", (1 + e.PreciousBonus / 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                        PreciousModifier, (float)e.PreciousBonus, e.PreciousBonus > 0 ? AfterhourEffectPolarity.Good : AfterhourEffectPolarity.Bad);
                if (e.DetectionPoints != 0)
                    definition.AddEffect(() => ModEntry.L(e.DetectionPoints > 0 ? "effect_risk_up" : "effect_risk_down", Math.Abs(e.DetectionPoints).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                        DetectionModifier, (float)e.DetectionPoints, e.DetectionPoints < 0 ? AfterhourEffectPolarity.Good : AfterhourEffectPolarity.Bad);
                if (e.WeightBonus != 0)
                    definition.AddEffect(() => ModEntry.L("effect_weight", ModEntry.L("category_" + e.Category.ToString().ToLowerInvariant()),
                        (1 + e.WeightBonus / 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                        WeightKey(e.Category), (float)e.WeightBonus, AfterhourEffectPolarity.Good);
                AfterhourEvents.Register(definition);
            }
            registered = true;
            Invalidate();
        }

        internal static void Invalidate() { syncedStore = null; syncedDay = -1; }

        internal static void Sync(int day, int seed)
        {
            if (!registered || !ModEntry.Enabled || !PlayerStore.HasInstance) return;
            var store = PlayerStore.Instance;
            if (ReferenceEquals(store, syncedStore) && day == syncedDay) return;
            // Set the cache before firing native hooks, which may query our odds.
            syncedStore = store; syncedDay = day;
            for (int i = 1; i <= 4; i++)
            {
                var selected = Select((Region)i, seed, day);
                foreach (var e in events)
                {
                    if (e.Region != (Region)i) continue;
                    if (selected == e) { ModHelper.StartAfterhourEvent(e.Id, 1); continue; }
                    if (store.GetAfterhourState(StationRules.LocationId(e.Region)).FindActive(e.Id) != null)
                        ModHelper.EndAfterhourEvent(e.Id);
                }
            }
        }

        internal static void Unregister()
        {
            if (registered && PlayerStore.HasInstance)
                foreach (var e in events) ModHelper.EndAfterhourEvent(e.Id);
            AfterhourEvents.RemoveSource(ModEntry.ModId);
            registered = false;
            Invalidate();
        }

        internal static string WeightKey(LootCategory category) { return "station_scavenging.weight_" + category.ToString().ToLowerInvariant(); }
        internal static double CategoryMultiplier(Region region, LootCategory category)
        {
            if (region == Region.Lower) return 1;
            return Math.Max(0, 1 + ModHelper.GetAfterhourModifier(StationRules.LocationId(region), WeightKey(category)) / 100.0);
        }
        public static double ApplyPrecious(double chance, double bonusPercent)
        {
            if (chance <= 0 || double.IsNaN(chance) || double.IsNaN(bonusPercent) || double.IsInfinity(bonusPercent)) return 0;
            return Math.Max(0, Math.Min(0.5, chance * Math.Max(0, 1 + bonusPercent / 100)));
        }
        public static double ApplyDetection(Region region, double chance, double points)
        {
            if (region == Region.Lower) return 0;
            if (double.IsNaN(points) || double.IsInfinity(points)) points = 0;
            return Math.Max(0, Math.Min(0.4, chance + points / 100));
        }
    }
}
