using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace StationScavenging
{
    internal static class StationGame
    {
        private static PlayerStore boundStore;
        private static int day = -1;
        internal static int UnlockMask = 1;
        internal static int LastPreciousDay = -1;
        internal static int PendingPreciousDay = -1;
        internal static int Seed;
        private static NightState night;
        private static bool busy;
        private static readonly Dictionary<Region, OutingState> outings = new Dictionary<Region, OutingState>();
        private static readonly Dictionary<Region, string> messages = new Dictionary<Region, string>();

        private static int GetInt(string key, int fallback) { return ModHelper.GetModDataInt(ModEntry.ModId, key, fallback); }
        private static void SetInt(string key, int value) { ModHelper.SetModDataInt(ModEntry.ModId, key, value); }
        private static double GetNumber(string key, double fallback)
        {
            double value;
            string text = ModHelper.GetModData(ModEntry.ModId, key, "");
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : fallback;
        }
        private static void SetNumber(string key, double value) { ModHelper.SetModData(ModEntry.ModId, key, value.ToString("R", CultureInfo.InvariantCulture)); }
        private static string Prefix(Region region) { return "outing_" + (int)region + "_"; }

        internal static void ClearSession()
        {
            boundStore = null;
            day = -1;
            UnlockMask = 1;
            LastPreciousDay = -1;
            PendingPreciousDay = -1;
            outings.Clear();
            messages.Clear();
            busy = false;
            night = null;
            StationEvents.Invalidate();
        }

        internal static void Reload()
        {
            ClearSession();
            if (PlayerStore.HasInstance) EnsureRun();
        }

        internal static void EnsureRun()
        {
            if (!PlayerStore.HasInstance) return;
            var store = PlayerStore.Instance;
            int today = StoreStation.Instance.dayCounter;
            if (!ReferenceEquals(boundStore, store))
            {
                ClearSession();
                boundStore = store;
                // Reuse v1.0.0's permanent unlocks and shared rare cooldown.
                UnlockMask = (GetInt("unlocked", 1) & StationRules.AllRegionsMask) | 1;
                LastPreciousDay = GetInt("last_precious_day", -1);
                PendingPreciousDay = GetInt("pending_precious_day", -1);
                int settlementVersion = GetInt("precious_settlement_version", 0);
                if (settlementVersion < 2)
                {
                    // An old same-night discovery now remains eligible until sleep.
                    // Older completed-night cooldowns keep their original anchor.
                    if (LastPreciousDay == today)
                    {
                        PendingPreciousDay = today;
                        LastPreciousDay = -1;
                        SetInt("pending_precious_day", PendingPreciousDay);
                        SetInt("last_precious_day", LastPreciousDay);
                    }
                }
                if (settlementVersion < 3)
                {
                    // v1.5.1 counted lower crate finds in its saved outing but
                    // excluded them from rest. Recover the latest retained
                    // record once, without guessing from inventory contents.
                    string key = Prefix(Region.Lower);
                    int lowerDay = GetInt(key + "day", -1);
                    if (lowerDay >= 0 && lowerDay <= today && GetInt(key + "precious_finds", 0) > 0)
                    {
                        bool liveNight = lowerDay == today && store.storeState == PlayerStore.StoreState.CLOSED
                            && !(GetInt("night_budget_day", -1) == today && GetInt("night_closed", 0) != 0);
                        if (liveNight)
                        {
                            PendingPreciousDay = Math.Max(PendingPreciousDay, lowerDay);
                            SetInt("pending_precious_day", PendingPreciousDay);
                        }
                        else
                        {
                            LastPreciousDay = Math.Max(LastPreciousDay, lowerDay);
                            SetInt("last_precious_day", LastPreciousDay);
                        }
                    }
                    SetInt("precious_settlement_version", 3);
                }
                Seed = GetInt("route_seed", 0);
                if (Seed == 0) { Seed = UnityEngine.Random.Range(1, int.MaxValue); SetInt("route_seed", Seed); }
            }
            // Also settle after loading a later day or re-enabling after sleep.
            // The native sleep hook runs after storeState changes to MORNING.
            if (PendingPreciousDay >= 0 && (PendingPreciousDay < today || store.storeState == PlayerStore.StoreState.MORNING))
                CommitPreciousNight(today);
            if (day != today)
            {
                day = today;
                outings.Clear();
                messages.Clear();
                night = null;
            }
            EnsureNight(store);
            int mask = UnlockMask;
            for (int i = 1; i <= 4; i++)
                if (StationRules.MeetsUnlock((Region)i, day, Reputation((Region)i), ExchangeUnlocked())) mask |= 1 << i;
            if (mask != UnlockMask) { UnlockMask = mask; SetInt("unlocked", mask); }
            StationEvents.Sync(day, Seed);
        }

        private static void EnsureNight(PlayerStore store)
        {
            if (night == null)
            {
                night = new NightState { Day = day };
                int savedDay = GetInt("night_budget_day", -1);
                if (savedDay == day)
                {
                    night.Used = Math.Max(0, GetInt("night_used", 0));
                    night.Caught = GetInt("night_caught", 0) != 0;
                    night.Closed = GetInt("night_closed", 0) != 0;
                }
                else if (savedDay < 0)
                {
                    // A mid-night upgrade must not give a fresh allowance after
                    // searches in the old timed mode. Ignore previous-day counters.
                    AfterhourLocationState lowerState;
                    if (store.afterhourState != null && store.afterhourState.TryGetValue(AfterhourLocation.DUMPING_GROUND, out lowerState)
                        && lowerState.lastVisitDay == day)
                        night.Used = Math.Max(0, store.scavengingAttempts);
                    for (int i = 1; i <= 4; i++)
                    {
                        string old = "timed_" + i + "_";
                        if (GetInt(old + "day", -1) != day) continue;
                        night.Used = Math.Min(1000000, night.Used + Math.Max(0, GetInt(old + "searches", 0)));
                        if (GetInt(old + "departure", 0) == 3) night.Caught = true;
                    }
                }
                // A normal morning precedes this day's first night. Only an
                // already-used same-day pool can be a post-sleep fallback.
                night.Closed |= night.Used > 0 && store.storeState == PlayerStore.StoreState.MORNING;
                UpdateNightTotal();
                SaveNight();
            }
            else
            {
                bool changed = UpdateNightTotal();
                if (night.Used > 0 && store.storeState == PlayerStore.StoreState.MORNING && !night.Closed) { night.Closed = true; changed = true; }
                if (changed) SaveNight();
            }
            RestoreNativeAttempts();
        }

        private static bool UpdateNightTotal()
        {
            // Keep the native +2/-2 perks, arthritis cutoff and event modifiers.
            // Georgio's real scav_attempts +5 becomes 15, not a separate pool.
            int native = ScavHelper.GetMaxScavAttempts();
            int total = native <= 0 ? 0 : (int)Math.Min(1000000L, (long)native + 5);
            bool changed = night.Total != total;
            night.Total = total;
            return changed;
        }

        private static void SaveNight()
        {
            SetInt("night_budget_day", night.Day);
            SetInt("night_total", night.Total);
            SetInt("night_used", night.Used);
            SetInt("night_caught", night.Caught ? 1 : 0);
            SetInt("night_closed", night.Closed ? 1 : 0);
        }

        internal static NightState Night { get { EnsureRun(); return night; } }

        internal static void ResumeLowerVisit()
        {
            var state = GetOuting(Region.Lower);
            // Other mods may allow another trip to the native dumping ground.
            // Leaving ended that visit, not its remaining shared night. Resume
            // only on an actual arrival and retain every saved counter/find.
            if (state.Departure == Departure.Voluntary && state.Day == day
                && PlayerStore.Instance.storeState == PlayerStore.StoreState.CLOSED
                && night != null && !night.Caught && !night.Closed)
            {
                state.Departure = Departure.None;
                Save(state);
            }
        }

        internal static void RestoreNativeAttempts()
        {
            if (night != null && PlayerStore.HasInstance) PlayerStore.Instance.scavengingAttempts = night.Used;
        }

        internal static bool SpendSearch(OutingState state)
        {
            EnsureRun();
            if (!state.TrySearch(night)) return false;
            SaveNight();
            Save(state);
            RestoreNativeAttempts();
            return true;
        }

        internal static bool MarkCaught(OutingState state)
        {
            EnsureRun();
            if (state.Day != day || night.Caught || !state.End(Departure.Caught)) return false;
            night.Caught = true;
            SaveNight();
            Save(state);
            return true;
        }

        internal static OutingState GetOuting(Region region)
        {
            EnsureRun();
            OutingState state;
            if (outings.TryGetValue(region, out state)) return state;
            string key = Prefix(region);
            state = new OutingState { Day = day, Region = region };
            bool legacy = GetInt(key + "day", -1) != day;
            if (legacy) key = "timed_" + (int)region + "_";
            if (GetInt(key + "day", -1) == day)
            {
                state.Searches = Math.Max(0, GetInt(key + "searches", 0));
                state.PreciousFinds = Math.Max(0, GetInt(key + "precious_finds", 0));
                int departure = GetInt(key + "departure", 0);
                state.Departure = legacy ? (departure == 3 ? Departure.Caught : departure == 0 ? Departure.None : Departure.Voluntary)
                    : (Departure)Math.Max(0, Math.Min(2, departure));
                state.FoundValue = (long)Math.Max(0, Math.Min(1000000000, GetNumber(key + "value", 0)));
            }
            outings[region] = state;
            return state;
        }

        internal static void Save(OutingState state)
        {
            string key = Prefix(state.Region);
            SetInt(key + "day", state.Day);
            SetInt(key + "searches", state.Searches);
            SetInt(key + "precious_finds", state.PreciousFinds);
            SetInt(key + "departure", (int)state.Departure);
            SetNumber(key + "value", state.FoundValue);
        }

        internal static bool ExchangeUnlocked()
        {
            return PlayerStore.HasInstance && PlayerStore.Instance.exchangeManager != null && PlayerStore.Instance.exchangeManager.isExchangeUnlocked;
        }
        internal static double Reputation(Region region)
        {
            var rep = StoreReputation.GetStoreReputation(StationRules.FactionId(region));
            return rep == null ? 0 : rep.GetReputationExact();
        }
        internal static bool IsUnlocked(Region region)
        {
            if (!PlayerStore.HasInstance) return false;
            EnsureRun();
            return (UnlockMask & (1 << (int)region)) != 0;
        }
        internal static SupplyCondition Condition(Region region) { return (SupplyCondition)StationRules.StableChoice(Seed, day, 100 + (int)region, 4); }
        internal static bool BlackHasCards { get { return StationRules.StableChoice(Seed, day, 207, 2) == 0; } }
        internal static bool PreciousAvailable
        {
            get
            {
                EnsureRun();
                // A migrated lower find may coexist with a previous advanced
                // rest. Preserve repeat finds for that already-started night;
                // settling it will replace the shared rest anchor as usual.
                return (PendingPreciousDay == day && night != null && !night.Closed)
                    || StationRules.CanFindPrecious(day, LastPreciousDay, ModEntry.Settings().PreciousRestNights);
            }
        }
        internal static double PreciousChance(OutingState state)
        {
            EnsureRun();
            if (state.Ended || state.Day != day || night.Remaining <= 0) return 0;
            double chance = StationRules.PreciousChance(state.Region, night.Progress, PreciousAvailable, BlackHasCards, ModEntry.Settings());
            if (state.Region == Region.Lower) return chance;
            return StationEvents.ApplyPrecious(chance, ModHelper.GetAfterhourModifier(StationRules.LocationId(state.Region), StationEvents.PreciousModifier));
        }

        internal static double DetectionChance(OutingState state)
        {
            EnsureRun();
            return StationEvents.ApplyDetection(state.Region, StationRules.DetectionChance(state.Region, night.Progress, Condition(state.Region)),
                ModHelper.GetAfterhourModifier(StationRules.LocationId(state.Region), StationEvents.DetectionModifier));
        }

        internal static void RecordPreciousFind(OutingState state)
        {
            EnsureRun();
            if (state.Ended || state.Day != day) return;
            state.PreciousFinds++;
            PendingPreciousDay = day;
            SetInt("pending_precious_day", PendingPreciousDay);
            Save(state);
        }

        internal static void CompleteNight()
        {
            EnsureRun();
            CommitPreciousNight(day);
            if (night != null) { night.Closed = true; SaveNight(); }
        }

        private static void CommitPreciousNight(int completedDay)
        {
            if (PendingPreciousDay < 0 || PendingPreciousDay > completedDay) return;
            LastPreciousDay = Math.Max(LastPreciousDay, PendingPreciousDay);
            SetInt("last_precious_day", LastPreciousDay);
            PendingPreciousDay = -1;
            SetInt("pending_precious_day", -1);
        }
        internal static string Message(Region region) { string text; return messages.TryGetValue(region, out text) ? text : ""; }

        internal static Penalty Quote(OutingState state)
        {
            double faction = state.Region == Region.BlackMarket ? 2 : 1;
            if (state.Region == Region.Security && StoreReputation.IsPerkUnlocked("UL_PETITION")) faction *= 2;
            return StationRules.GetPenalty(state.FoundValue, Difficulty.Rules.reputationGainMultiplier, faction, ModEntry.Settings());
        }

        internal static string BlockReason(OutingState state)
        {
            EnsureRun();
            if (!ModEntry.Enabled || state.Ended || state.Day != day || night.Closed) return "ended";
            if (night.Caught) return "caught";
            if (night.Remaining <= 0) return "exhausted";
            if (!IsUnlocked(state.Region)) return "locked";
            if (state.Region == Region.Lower)
            {
                if (PlayerStore.Instance.healthData.isWoundedFresh) return "wounded";
            }
            else if (PlayerStore.Instance.GetCash() < Quote(state).Credits) return "no_cash";
            return null;
        }

        internal static void Search(OutingState state)
        {
            if (busy || !ModEntry.Enabled || !ModHelper.IsOutsideStore() || ModHelper.GetCurrentAfterhourLocationId() != StationRules.LocationId(state.Region)) return;
            string reason = BlockReason(state);
            if (reason != null) { messages[state.Region] = ModEntry.L("block_" + reason); StoreUIManager.Instance.Notify(messages[state.Region]); return; }
            busy = true;
            try
            {
                // Snapshot both probabilities before spending the action. The
                // final allowed search uses exactly the odds shown on its panel.
                double risk = DetectionChance(state);
                double preciousChance = PreciousChance(state);
                int usedBefore = night.Used;
                Penalty quote = Quote(state);
                if (!SpendSearch(state)) return;
                if (state.Region == Region.Lower)
                {
                    bool searchCompleted = false;
                    try
                    {
                        PlayerStore.Instance.scavengingAttempts = usedBefore;
                        ModHelper.SendMessage(ModEntry.ModId, "lower_search_started", state);
                        LowerScavenging.Search(state, preciousChance);
                        searchCompleted = true;
                    }
                    finally
                    {
                        try
                        {
                            ModHelper.SendMessage(ModEntry.ModId, searchCompleted ? "lower_search_finished" : "lower_search_aborted", state);
                        }
                        finally { RestoreNativeAttempts(); }
                    }
                    return;
                }
                RunStats.OnScavengeAttempt();
                if (UnityEngine.Random.value < risk)
                {
                    if (!MarkCaught(state)) return;
                    PlayerStore.Instance.ModCash(-quote.Credits);
                    StoreReputation.GetStoreReputation(StationRules.FactionId(state.Region))?.ModReputationRaw(-quote.Reputation);
                    string text = ModEntry.L("caught", quote.Credits, quote.Reputation.ToString("0.##", CultureInfo.InvariantCulture));
                    messages[state.Region] = text;
                    StoreUIManager.Instance.Notify(text, "sec");
                    PlayerStore.Instance.AddNightLog(text);
                    return;
                }
                LootChoice choice = null;
                string crate = null;
                if (PreciousAvailable)
                {
                    if (UnityEngine.Random.value < preciousChance)
                    {
                        if (state.Region == Region.BlackMarket && BlackHasCards)
                            choice = LootCatalog.Pick(LootCatalog.Keycards, LootProfile.Focused, day, SupplyCondition.Normal, UnityEngine.Random.value);
                        else crate = LootCatalog.CrateId(state.Region);
                    }
                }
                bool rare = choice != null || crate != null;
                if (!rare) choice = PickCommon(state.Region, UnityEngine.Random.value < 0.30 ? LootProfile.Focused : LootProfile.Ordinary);
                GameItem item = crate != null ? ModHelper.SpawnItem(crate) : Spawn(choice);
                if (item == null) throw new InvalidOperationException("Native item factory returned null.");
                if (rare) RecordPreciousFind(state);
                RecordAndPlace(state, item, crate ?? choice.ItemId);
                string foundName = item.name;
                bool extra = false;
                if (EmporiumEntry.Instance.IsAfterhourHaveOwnedItems("metal_scanner"))
                {
                    var scanner = EmporiumEntry.Instance.GetAfterhourItemById("metal_scanner");
                    int chance = scanner.GetTagReadonly(ScavHelper.SCAV_SCANNER_DOUBLE_CHANCE).GetInt();
                    if (UnityEngine.Random.value * 100 < Math.Max(0, Math.Min(100, chance)))
                    {
                        var bonus = PickCommon(state.Region, LootProfile.Ordinary);
                        var bonusItem = Spawn(bonus);
                        if (bonusItem != null) { RecordAndPlace(state, bonusItem, bonus.ItemId); extra = true; }
                    }
                }
                messages[state.Region] = ModEntry.L(extra ? "found_extra" : "found", foundName);
                StoreUIManager.Instance.Notify(messages[state.Region]);
                AudioManager.Instance.Play2("debris");
                EmporiumEntry.Instance.Validate();
            }
            catch (Exception ex)
            {
                messages[state.Region] = ModEntry.L("action_error");
                StoreUIManager.Instance.Notify(messages[state.Region]);
                ModEntry.Note("Search failed: " + ex);
            }
            finally { busy = false; Save(state); }
        }

        private static LootChoice PickCommon(Region region, LootProfile profile)
        {
            return LootCatalog.Pick(LootCatalog.Entries(region), profile, day, Condition(region), UnityEngine.Random.value, 1,
                category => StationEvents.CategoryMultiplier(region, category));
        }

        private static GameItem Spawn(LootChoice choice)
        {
            if (choice == null) return null;
            GameItem item;
            switch (choice.Style)
            {
                case SpawnStyle.PoorHandmade: item = PreBuiltItemHelper.CreateHandmadeGun(ItemFeatureList.EquipmentCondition.Poor); break;
                case SpawnStyle.PoorHeavyHandmade: item = PreBuiltItemHelper.CreateHeavyHandmadeGun(ItemFeatureList.EquipmentCondition.Poor); break;
                case SpawnStyle.PoorPolice: item = PreBuiltItemHelper.CreatePoliceHandgun(ItemFeatureList.EquipmentCondition.Poor); break;
                default: item = ModHelper.SpawnItem(choice.ItemId); break;
            }
            if (item != null && choice.Style == SpawnStyle.Stolen) StolenHelper.InitStolenItem(item);
            return item;
        }

        private static void RecordAndPlace(OutingState state, GameItem item, string id)
        {
            long value = LootCatalog.FixedRiskValue(id);
            if (value < 0) value = Math.Min(1000000000L, Math.Max(0, Math.Min(1000000000L, item.GetBaseValue())) * Math.Max(1, Math.Min(1000000, item.unitCount)));
            state.RecordValue(value);
            Save(state);
            ModHelper.MoveItemToAfterhours(item);
        }
    }
}
