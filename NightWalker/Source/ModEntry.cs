using System.Collections.Generic;
using HarmonyLib;

namespace NightWalker
{
    public sealed class ModEntry : IMod
    {
        public const string ModId = "night_walker";
        public const string PerkId = "night_walker.night_walker";
        public const string IconKey = ModId + ":night_walker_icon";
        public const int VisitsPerNight = 3;

        private ModLog log;
        private static ConfigEntry<int> perkCost;

        internal static int StartingCost => perkCost == null ? 2 : perkCost.Value;

        public void Init(ModManifest manifest)
        {
            log = new ModLog(manifest);
            perkCost = ModHelper.GetConfig(ModId).Define("perk_cost", 2,
                "Starting perk / 起始特性",
                () => ModHelper.GetLocalized(ModId, "config_perk_cost"),
                min: 1, max: 2,
                tooltip: () => ModHelper.GetLocalized(ModId, "config_perk_cost_hint"),
                requiresRestart: true);
        }

        public void OnEnable()
        {
            if (NightRules.Enabled) return;

            ModHelper.AddStartingPerk(
                ModId, PerkId,
                cost: StartingCost,
                type: StartingPerk.StartingPerkType.POSITIVE,
                name: () => ModHelper.GetLocalized(ModId, "perk_name"),
                description: () => ModHelper.GetLocalized(ModId, "perk_description"),
                iconKey: IconKey);

            NightRules.Enabled = true;
            ModHook.OnShutterClosedLate += NightRules.SyncRemainingVisits;
            ModHook.OnReturningStoreLate += NightRules.SyncRemainingVisits;
            ModHook.OnGameLoadedLate += NightRules.SyncRemainingVisits;
            // These patches are applied by the game's loader, as documented.
            // Do not create a second Harmony instance or call PatchAll here.
            log?.Log("Night Walker enabled: up to 3 outings per night; perk cost " + StartingCost + ".");
        }

        public void OnDisable()
        {
            NightRules.Enabled = false;
            ModHook.OnShutterClosedLate -= NightRules.SyncRemainingVisits;
            ModHook.OnReturningStoreLate -= NightRules.SyncRemainingVisits;
            ModHook.OnGameLoadedLate -= NightRules.SyncRemainingVisits;
            ModHelper.RemoveStartingPerks(ModId);
        }
    }

    [HarmonyPatch(typeof(StartingPerkIconLoader),nameof(StartingPerkIconLoader.GetIcon))]
    internal static class SavedPerkIconPatch
    {
        internal static void Prefix(StartingPerk perk)
        {
            // Native saves deliberately omit iconKey. Restore the resource key
            // before the resolver falls back to the generic people/paw icon.
            if(NightRules.Enabled && perk!=null && perk.id==ModEntry.PerkId) perk.iconKey=ModEntry.IconKey;
        }
    }

    internal static class NightRules
    {
        internal static bool Enabled;

        internal static bool Applies
        {
            get
            {
                // Avoid constructing PlayerStore at the main menu. No cached
                // perk flag or visited set can leak between saves or new runs.
                return Enabled && PlayerStore.HasInstance
                    && PlayerStore.Instance.storeState == PlayerStore.StoreState.CLOSED
                    && ModHelper.IsStartingPerkActive(ModEntry.PerkId);
            }
        }

        private static int RemainingVisits(Dictionary<string, AfterhourLocationState> history, int today)
        {
            int remaining = ModEntry.VisitsPerNight;
            if (history == null) return remaining;
            // The native dictionary already has one entry per destination ID.
            // Count directly and stop at the nightly cap; no temporary set.
            foreach (var entry in history)
            {
                if (!string.IsNullOrEmpty(entry.Key) && entry.Value != null
                    && entry.Value.lastVisitDay == today)
                {
                    if (--remaining == 0) break;
                }
            }
            return remaining;
        }

        private static bool WasVisited(Dictionary<string, AfterhourLocationState> history, string id, int today)
        {
            return history != null && !string.IsNullOrEmpty(id)
                && history.TryGetValue(id, out var state)
                && state != null && state.lastVisitDay == today;
        }

        internal static void SyncRemainingVisits()
        {
            if (!Applies) return;
            // Normal returns spend a trip in EmporiumEntry.OnArriveStore.
            // Reconcile AFTER that step; canceling the map spends no trip.
            var store = PlayerStore.Instance;
            store.visitLeftTonight = RemainingVisits(store.afterhourState, StoreStation.Instance.dayCounter);
        }

        internal static void FilterDestinations(List<AfterhourLocation> locations)
        {
            if (!Applies || locations == null) return;
            var history = PlayerStore.Instance.afterhourState;
            int today = StoreStation.Instance.dayCounter;
            bool exhausted = RemainingVisits(history, today) == 0;
            // Work backwards in the game's fresh list. Avoid allocating a
            // captured predicate and do not touch the location registry.
            for (int i = locations.Count - 1; i >= 0; i--)
            {
                var location = locations[i];
                if (location != null && (exhausted || WasVisited(history, location.Id, today)))
                    locations.RemoveAt(i);
            }
        }

        internal static bool CanTravel(AfterhourLocation location)
        {
            if (!Applies) return true;
            if (location == null || string.IsNullOrEmpty(location.Id)) return false;
            var history = PlayerStore.Instance.afterhourState;
            int today = StoreStation.Instance.dayCounter;
            return !WasVisited(history, location.Id, today) && RemainingVisits(history, today) > 0;
        }
    }

    [HarmonyPatch(typeof(AfterhourLocation), nameof(AfterhourLocation.GetDiscovered))]
    internal static class DestinationListPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(ref List<AfterhourLocation> __result)
        {
            // GetDiscovered returns a fresh list; never modify the registry.
            NightRules.FilterDestinations(__result);
        }
    }

    [HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.Travel))]
    internal static class TravelGuardPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(AfterhourLocation location)
        {
            // Also cover stale selections and other mods invoking shared Travel
            // directly. The original method retains availability/transition checks.
            return NightRules.CanTravel(location);
        }
    }
}
