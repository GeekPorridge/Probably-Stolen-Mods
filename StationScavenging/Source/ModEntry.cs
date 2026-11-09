using System;
using System.Collections.Generic;

namespace StationScavenging
{
    public sealed class ModEntry : IMod
    {
        public const string ModId = "station_scavenging";
        public const string Version = "1.5.6";
        private static ModLog log;
        private static AfterhourLocation original;
        private static LowerLevelsLocation lower;
        private static readonly List<ScavengingLocation> locations = new List<ScavengingLocation>();
        internal static bool Enabled;
        private static ConfigEntry<int> fineBase, fineCap, preciousRest;
        private static ConfigEntry<float> fineRate, reputationCap, crateChanceAtEnd, cardChanceAtEnd;

        public void Init(ModManifest manifest)
        {
            log = new ModLog(manifest);
            ModConfig config = ModHelper.GetConfig(ModId);
            const string section = "Balance / 平衡";
            fineBase = config.Define("fine_base", 20, section, () => L("config_fine_base"), min: 0, max: 200);
            fineCap = config.Define("fine_cap", 200, section, () => L("config_fine_cap"), min: 0, max: 2000);
            fineRate = config.Define("fine_rate", 0.25f, section, () => L("config_fine_rate"), min: 0f, max: 1f);
            reputationCap = config.Define("reputation_cap", 3f, section, () => L("config_rep_cap"), min: 0f, max: 10f);
            preciousRest = config.Define("precious_rest_nights", 3, section, () => L("config_rest"), min: 0, max: 14);
            crateChanceAtEnd = config.Define("crate_chance_end_percent", 5f, section, () => L("config_crate_end"), min: 1f, max: 50f);
            cardChanceAtEnd = config.Define("card_chance_end_percent", 7.5f, section, () => L("config_card_end"), min: 1.5f, max: 50f);
        }

        public void OnEnable()
        {
            if (Enabled) return;
            Enabled = true;
            ModHelper.OnMessage(ModId, "pelican_cultivation:record_precious", RecordExpansionPreciousFind);
            InstallLowerCrate();
            InstallLocations();
            StationEvents.Register();
            StationTransitUI.Ensure();
            ModHook.OnGameLoadedLate += Reload;
            ModHook.OnNewGameStarted += OnNewGame;
            ModHook.OnShutterClosedLate += OnNightStart;
            ModHook.OnGoingSleepLate += OnNightEnd;
            Note("Station Scavenging " + Version + ": repeat lower visits resume remaining shared searches; five-region valuable rest and caught locks retained.");
        }

        public void OnDisable()
        {
            Enabled = false;
            ModHelper.OffMessage(ModId, "pelican_cultivation:record_precious", RecordExpansionPreciousFind);
            ModHook.OnGameLoadedLate -= Reload;
            ModHook.OnNewGameStarted -= OnNewGame;
            ModHook.OnShutterClosedLate -= OnNightStart;
            ModHook.OnGoingSleepLate -= OnNightEnd;
            StationTransitUI.Stop();
            StationArt.Dispose();
            StationEvents.Unregister();
            ModHelper.RemoveLootGroupEntry(ModId, "dumpingGroundTG", LootCatalog.LowerCrateTable);
            foreach (var location in locations)
            {
                location.DisposePanel();
                if (ReferenceEquals(AfterhourLocation.Get(location.Id), location)) AfterhourLocation.GetAll().Remove(location);
            }
            locations.Clear();
            if (lower != null) lower.DisposePanel();
            if (lower != null && ReferenceEquals(AfterhourLocation.Get(AfterhourLocation.DUMPING_GROUND), lower))
                AfterhourLocation.Register(original ?? new DumpingGroundLocation());
            lower = null;
            StationGame.ClearSession();
        }

        private static void InstallLowerCrate()
        {
            if (!LootRegistry.HasTable(LootCatalog.LowerCrateTable))
                ModHelper.RegisterLootTable(ModId, LootCatalog.LowerCrateTable, (LootCatalog.CrateId(Region.Lower), 1f));
            // Register at the first-search odds. Each lower search adjusts only
            // our table weight to the shared progress before asking native loot helpers.
            ModHelper.AddLootGroupEntry(ModId, "dumpingGroundTG", LootCatalog.LowerCrateTable, LootCatalog.LowerCrateWeight);
        }

        private static void InstallLocations()
        {
            if (lower == null)
            {
                original = AfterhourLocation.Get(AfterhourLocation.DUMPING_GROUND) ?? new DumpingGroundLocation();
                lower = new LowerLevelsLocation(original);
                AfterhourLocation.Register(lower);
            }
            if (locations.Count == 0)
            {
                for (int i = 1; i < 5; i++)
                {
                    var location = new ScavengingLocation((Region)i, original);
                    locations.Add(location);
                    ModHelper.AddAfterhourLocation(location);
                }
            }
        }

        private static void Reload()
        {
            if (!Enabled) return;
            InstallLocations();
            StationGame.Reload();
            StationTransitUI.Ensure();
            StationTransitUI.Invalidate();
        }

        private static void OnNewGame(string scenario) { Reload(); }
        private static void OnNightStart() { if (Enabled && PlayerStore.HasInstance) { StationGame.EnsureRun(); StationTransitUI.Ensure(); StationTransitUI.Invalidate(); } }
        private static void OnNightEnd() { if (Enabled && PlayerStore.HasInstance) StationGame.CompleteNight(); }

        private static void RecordExpansionPreciousFind(string sender, object payload)
        {
            if (sender == "pelican_cultivation" && payload is OutingState state)
                StationGame.RecordPreciousFind(state);
        }

        internal static BalanceSettings Settings()
        {
            return new BalanceSettings
            {
                BaseFine = fineBase == null ? 20 : fineBase.Value,
                FineCap = fineCap == null ? 200 : fineCap.Value,
                FineRate = fineRate == null ? 0.25 : fineRate.Value,
                StandardReputationCap = reputationCap == null ? 3 : reputationCap.Value,
                PreciousRestNights = preciousRest == null ? 3 : preciousRest.Value,
                CrateChanceAtEnd = crateChanceAtEnd == null ? 0.05 : crateChanceAtEnd.Value / 100.0,
                KeycardChanceAtEnd = cardChanceAtEnd == null ? 0.075 : cardChanceAtEnd.Value / 100.0
            };
        }

        internal static string L(string key, params object[] args) { return ModHelper.GetLocalized(ModId, key, args); }
        internal static void Note(string text) { log?.Log(text); }
    }
}
