using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

public static class InstalledPeerIntegration
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags InstanceHidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static Assembly station, better, skills;
    private static Harmony env, first, second, own;
    private static Type game, lower;
    private static int checks, frame = 100;
    private static GameItem gloves;
    private static double groupRoll = 0.999, tableRoll = 0.1;
    private static float boxRoll = 90f;
    private static string currentLocation = "dumping_ground";
    private static List<GameItem> placed;
    private static string[] args;

    public static int Main(string[] values)
    {
        args = values;
        Environment.SetEnvironmentVariable("MONOMOD_DMDType", "cecil");
        string managed = Path.Combine(args[0], "Probably Stolen_Data/Managed");
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            string name = new AssemblyName(e.Name).Name;
            string path = name == "StationScavenging" ? args[1] : name == "BetterScavenging" ? args[2] : name == "ScavengerSkills" ? args[3] : Path.Combine(managed, name + ".dll");
            string fixture = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "EngineFixture", name + ".dll");
            if (File.Exists(fixture)) path = fixture;
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { return Run(); }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run()
    {
        placed = new List<GameItem>();
        station = Assembly.LoadFrom(args[1]); better = Assembly.LoadFrom(args[2]); skills = Assembly.LoadFrom(args[3]);
        game = station.GetType("StationScavenging.StationGame");
        lower = station.GetType("StationScavenging.LowerScavenging");
        env = new Harmony("station_scavenging.test.engine-boundaries");
        first = new Harmony("better_scavenging"); second = new Harmony("eotn_scavenger_skills"); own = new Harmony("station_scavenging");
        try
        {
            EnvironmentBoundaries();
            SeedLoot(); NewRun(200);
            var stationEntry = (IMod)Activator.CreateInstance(station.GetType("StationScavenging.ModEntry"));
            AddMod("station_scavenging", "1.5.5", station, stationEntry);
            station.GetType("StationScavenging.ModEntry").GetField("log", Hidden).SetValue(null, new ModLog(new ModManifest { Name = "Station fixture" }));
            var betterEntry = Activator.CreateInstance(better.GetType("BetterScavenging.ModEntry"));
            better.GetType("BetterScavenging.ModEntry").GetMethod("RegisterPreBuilt", InstanceHidden).Invoke(betterEntry, null);
            AddMod("better_scavenging", "1.13.0", better, (IMod)betterEntry);
            AddMod("eotn_scavenger_skills", "1.0.7", skills, null);
            skills.GetType("ScavengerSkills.Core").GetField("Enabled", Hidden).SetValue(null, true);
            PatchClass(first, better, "BetterScavenging.BoxDropPatch");
            PatchClass(first, better, "BetterScavenging.MetalGlovesPatch");
            PatchClass(second, skills, "ScavengerSkills.EnduranceSkillPatch");
            PatchClass(second, skills, "ScavengerSkills.LootPatch");
            PatchClass(second, skills, "ScavengerSkills.JunkWeightSkillPatch");
            own.PatchAll(station);
            stationEntry.OnEnable();
            var compat = skills.GetType("ScavengerSkills.StationCompat");
            compat.GetMethod("Tick", Hidden).Invoke(null, new object[] { true });
            Check((bool)compat.GetField("ready", Hidden).GetValue(null), "Original Scavenging Expansion recognizes the new Station contract and installs all patches");

            Check(PreBuiltItemHelper.preBuiltItems["battered_wooden_crate"].Method.DeclaringType.Assembly == better, "Wooden crate keeps the original Better Scavenging factory");
            Check(PreBuiltItemHelper.preBuiltItems["pristine_metal_case"].Method.DeclaringType.Assembly == better, "Metal case keeps the original Better Scavenging factory");
            if (args.Length > 4 && args[4] == "reentry-only")
            {
                CountAndReentryChecks();
                Console.WriteLine("PASSED original-peer repeat-entry checks: " + checks);
                return 0;
            }
            PoolAndRollChecks();
            Console.WriteLine("PASS: Original wooden/metal crate odds, electronic upgrades and expedition rolls remain independent of the Service core pool");
            CountAndReentryChecks();
            Console.WriteLine("PASS: Real endurance, glove and extra-outing logic, second Lower entry, saved departures and exhaustion");
            DetectionAndExperienceChecks();
            Console.WriteLine("PASS: Real Expansion adapter, one XP settlement, optional crates never trigger or obey core rest; Service rest retained");
            Console.WriteLine("PASSED " + checks + " installed-peer integration checks. Original BetterScavenging 1.13.0 and ScavengerSkills 1.0.7 DLLs loaded and their relevant Harmony patches executed. Unity rendering/audio, physical inventory placement, deterministic RNG and scene readiness use isolated fixtures; no user saves/settings accessed.");
            return 0;
        }
        finally
        {
            var compat = skills.GetType("ScavengerSkills.StationCompat");
            compat.GetMethod("Clear", Hidden).Invoke(null, null);
            own.UnpatchAll(own.Id); second.UnpatchAll(second.Id); first.UnpatchAll(first.Id); env.UnpatchAll(env.Id);
        }
    }

    private static void PoolAndRollChecks()
    {
        NewRun(200); gloves = null; NextFrame(); groupRoll = 0; tableRoll = 0;
        Update(0.05);
        var pool = LootRegistry.GetTableEntries("station_scavenging.lower_service_crate");
        Check(pool.Count == 1 && pool[0].id == "loot_crate_service", "Core pool contains only our native Service crate with both peers enabled");
        Check(station.GetType("StationScavenging.LowerCoreLoot") == null && station.GetType("StationScavenging.LowerCoreRollPatch") == null, "Withdrawn box interception and optional core-table code are absent from the release");
        int before = Hits();
        foreach (var pair in new[] {
            new object[] { 0f, "battered_wooden_crate" },
            new object[] { 2.999f, "battered_wooden_crate" },
            new object[] { 3f, "pristine_metal_case" },
            new object[] { 3.999f, "pristine_metal_case" },
            new object[] { 4f, "junk" }
        })
        {
            boxRoll = (float)pair[0];
            Check(NormalRoll(null) == (string)pair[1], "Original Better prefix retains its Lower 3% wooden / 1% metal boundaries");
        }
        Check(Hits() == before + 4, "The author's original box prefix runs without interception");
        Update(0);
        foreach (var pair in new[] { new object[] { 0f, "battered_wooden_crate" }, new object[] { 3.5f, "pristine_metal_case" } })
        {
            boxRoll = (float)pair[0];
            Check(NormalRoll(null) == (string)pair[1], "Core rest leaves the original Lower optional box roll enabled");
        }
        Check(LootRegistry.GetGroupEntries("dumpingGroundTG").Find(e => e.id == "station_scavenging.lower_service_crate").weight == 0, "Only the Service core entry is zero during shared rest");
        boxRoll = 4.5f;
        Check(NormalRoll(null) == "junk", "Unupgraded gloves keep the original 4% combined box boundary");
        gloves = Item("scav_gloves"); gloves.ModifyTag("SCAV_GLOVE_ELEC_LEVEL", t => t.Enable().SetInt(2));
        Check(NormalRoll(null) == "pristine_metal_case", "Original electronic glove code increases its own absolute metal-box chance");
        boxRoll = 5.5f;
        Check(NormalRoll(null) == "junk", "Original upgraded metal-box upper boundary is unchanged");
        gloves = null;
        boxRoll = 0; Samples();
        Check(LootRegistry.RollGroup("dumpingGroundFreshTG") == "battered_wooden_crate", "Original Fresh Scrap expedition wooden crate is retained");
        boxRoll = 7; Samples();
        Check(LootRegistry.RollGroup("dumpingGroundFreshTG") == "pristine_metal_case", "Original Fresh Scrap expedition metal crate is retained");
        Update(0.05);
        Check(LootRegistry.GetTableEntries("station_scavenging.lower_service_crate").Count == 1, "Updating our core chance never adds third-party crate IDs");
        boxRoll = 90;
    }

    private static void CountAndReentryChecks()
    {
        foreach (int rank in new[] { 0, 4 }) foreach (int gloveRank in new[] { -1, 0, 2 })
        {
            var store = NewRun(210); SetRank("endurance", rank); SetRank("outings", 2);
            gloves = gloveRank < 0 ? null : Item("scav_gloves");
            if (gloves != null) gloves.ModifyTag("SCAV_GLOVE_LEVEL", s => s.Enable().SetInt(gloveRank));
            NextFrame();
            int total = 10 + rank + (gloveRank < 0 ? 0 : 1 + gloveRank);
            Near(Int(Night(), "Total"), total, "Original endurance and metal-glove postfixes stack into the shared total");
            var skillsType = skills.GetType("ScavengerSkills.Skills");
            store.visitLeftTonight = 1;
            skillsType.GetMethod("OnClosed", Hidden).Invoke(null, null);
            Check(store.visitLeftTonight == 3, "Original Extra Outings skill grants travel opportunities");
            skillsType.GetMethod("OnClosed", Hidden).Invoke(null, null);
            Check(store.visitLeftTonight == 3, "Repeated close hook does not award outings twice");
            var state = Outing("Lower"); var bridgeType = station.GetType("StationScavenging.LowerLevelsLocation");
            var bridge = (AfterhourLocation)Activator.CreateInstance(bridgeType, InstanceHidden, null, new object[] { new Probe() }, null);
            var stateField = station.GetType("StationScavenging.ScavengingLocation").GetField("state", InstanceHidden);
            var arrive = bridgeType.GetMethod("EnterNative", InstanceHidden);
            stateField.SetValue(bridge, state); arrive.Invoke(bridge, null);
            Check((bool)Call("SpendSearch", state), "First Lower visit spends the actual combined budget");
            Check((bool)Call("SpendSearch", Outing("Upper")), "A different region spends the same combined budget");
            bridge.Leave(); arrive.Invoke(bridge, null);
            Check(Call("BlockReason", state) == null, "Second actual Lower arrival is searchable with both original mods loaded");
            Check(Int(Night(), "Remaining") == total - 2 && store.scavengingAttempts == 2, "Second arrival preserves all used searches under the real count postfixes");
            bridge.Leave(); Call("Reload"); state = Outing("Lower"); stateField.SetValue(bridge, state); arrive.Invoke(bridge, null);
            Check(Call("BlockReason", state) == null && Int(Night(), "Remaining") == total - 2, "Reloaded voluntary departure resumes without a fresh pool");
            for (int n = 2; n < total; n++) Check((bool)Call("SpendSearch", state), "Remaining external bonus searches are spendable");
            bridge.Leave(); arrive.Invoke(bridge, null);
            Check(!(bool)Call("SpendSearch", state), "Real bonus postfixes cannot refill an exhausted shared night");
        }
    }

    private static void DetectionAndExperienceChecks()
    {
        var store = NewRun(220); gloves = null; NextFrame();
        SetRank("endurance", 2); SetRank("caution", 2);
        var upper = Outing("Upper");
        double withSkill = (double)Call("DetectionChance", upper);
        SetRank("caution", 0); double withoutSkill = (double)Call("DetectionChance", upper);
        Check(withSkill < withoutSkill, "Original Expansion adapter still reduces advanced-region detection");
        foreach (var pair in new[] { new object[] { 0f, "battered_wooden_crate" }, new object[] { 3.5f, "pristine_metal_case" } })
        {
            store = NewRun(221); SetRank("endurance", 2); NextFrame();
            store.dumpingGroundMinorWoundImmunitySearchesLeft = 100;
            store.dumpingGroundMajorWoundImmunitySearchesLeft = 100;
            currentLocation = "dumping_ground"; groupRoll = 0.999999; tableRoll = 0.1; boxRoll = (float)pair[0]; placed.Clear();
            var state = Outing("Lower"); Samples(); Call("Search", state);
            Check(placed.Any(i => i.identifier == (string)pair[1]), "Actual Lower search receives an original third-party box normally");
            Check(Int(state, "PreciousFinds") == 0 && (int)Field(game, "PendingPreciousDay") == -1, "A third-party box never records a core valuable discovery");
            Check(ModHelper.GetModDataInt("eotn_scavenger_skills", "scav_count", 0) == 1, "Original Expansion records exactly one attempt for the actual search");
            Call("CompleteNight"); StoreStation.Instance.dayCounter = 222;
            Check((bool)game.GetProperty("PreciousAvailable", Hidden).GetValue(null), "A third-party box does not start any core rest next night");
        }
        store = NewRun(230); NextFrame(); boxRoll = 90; groupRoll = 0.999999; tableRoll = 0.1;
        store.dumpingGroundMinorWoundImmunitySearchesLeft = 100;
        store.dumpingGroundMajorWoundImmunitySearchesLeft = 100;
        placed.Clear(); var ownState = Outing("Lower"); Samples(); Call("Search", ownState);
        Check(placed.Any(i => i.identifier == "loot_crate_service") && Int(ownState, "PreciousFinds") == 1, "Our native Service crate still triggers core valuable rest");
        Call("CompleteNight");
        for (int day = 231; day <= 233; day++)
        {
            StoreStation.Instance.dayCounter = day;
            Check(!(bool)game.GetProperty("PreciousAvailable", Hidden).GetValue(null), "Service core rest still lasts three full nights");
            foreach (var pair in new[] { new object[] { 0f, "battered_wooden_crate" }, new object[] { 3.5f, "pristine_metal_case" } })
            {
                boxRoll = (float)pair[0]; placed.Clear(); var state = Outing("Lower");
                Samples(); Call("Search", state);
                Check(placed.Any(i => i.identifier == (string)pair[1]), "Actual Lower search can find original optional boxes during core rest");
                Check(Int(state, "PreciousFinds") == 0 && (int)Field(game, "LastPreciousDay") == 230, "Optional boxes do not add or extend existing core rest");
            }
        }
        StoreStation.Instance.dayCounter = 234;
        Check((bool)game.GetProperty("PreciousAvailable", Hidden).GetValue(null), "Service core loot recovers on its original fourth-night schedule");
    }

    private static void EnvironmentBoundaries()
    {
        Console.WriteLine("Binding engine fixture: logging");
        foreach (string name in new[] { "Log", "LogWarning", "LogError" })
            env.Patch(typeof(UnityEngine.Debug).GetMethod(name, new[] { typeof(object) }), prefix: HM("Skip"));
        foreach (string name in new[] { "Log", "Warning", "Error" }) env.Patch(typeof(ModLog).GetMethod(name), prefix: HM("LogMessage"));
        env.Patch(station.GetType("StationScavenging.ModEntry").GetMethod("L", Hidden), prefix: HM("Localized"));
        foreach (var method in typeof(LocHelper).GetMethods().Where(m => m.Name == "GetLocalizedMechanic" && m.ReturnType == typeof(string))) env.Patch(method, prefix: HM("Localized"));
        Console.WriteLine("Binding engine fixture: object lifetime");
        foreach (string name in new[] { "op_Equality", "op_Inequality" })
            env.Patch(typeof(UnityEngine.Object).GetMethod(name, new[] { typeof(UnityEngine.Object), typeof(UnityEngine.Object) }), prefix: HM(name == "op_Equality" ? "EqualObject" : "UnequalObject"));
        Console.WriteLine("Binding engine fixture: inventory and location");
        env.Patch(typeof(EmporiumEntry).GetMethod("GetAfterhourItemById"), prefix: HM("FindItem"));
        env.Patch(typeof(EmporiumEntry).GetMethod("IsAfterhourHaveOwnedItems"), prefix: HM("HasItem"));
        env.Patch(typeof(ModHelper).GetMethod("IsOutsideStore"), prefix: HM("IsOutside"));
        env.Patch(typeof(ModHelper).GetMethod("GetCurrentAfterhourLocationId"), prefix: HM("Location"));
        env.Patch(typeof(HealthData).GetMethod("RollBadLuck"), prefix: HM("FalseResult"));
        Console.WriteLine("Binding engine fixture: random numbers");
        env.Patch(typeof(RNG).GetMethod("Roll", new[] { typeof(int) }), transpiler: HM("FalseBody"));
        Console.WriteLine("Binding native loot random sampler");
        Console.WriteLine("Binding engine fixture: item placement and scene UI");
        env.Patch(typeof(ItemSpawner).GetMethod("Spawn", new[] { typeof(string) }), prefix: HM("SpawnItem"));
        env.Patch(typeof(GraphUtils).GetMethod("TryAcceptAll", new[] { typeof(GraphNodeStorage), typeof(GameItem), typeof(int) }), prefix: HM("Place"));
        foreach (var method in typeof(StoreUIManager).GetMethods().Where(m => m.Name == "Notify")) env.Patch(method, prefix: HM("Skip"));
        foreach (var method in typeof(AudioManager).GetMethods().Where(m => m.Name == "Play2" || m.Name == "Play3")) env.Patch(method, prefix: HM("Skip"));
        env.Patch(typeof(RunStats).GetMethod("OnScavengeAttempt"), prefix: HM("Skip"));
        env.Patch(typeof(RunStats).GetMethod("OnScavengedItems"), prefix: HM("Skip"));
        env.Patch(skills.GetType("ScavengerSkills.Core").GetProperty("SkillsActive", Hidden).GetMethod, prefix: HM("TrueResult"));
    }

    private static void SeedLoot()
    {
        var groups = (Dictionary<string, List<LootEntry>>)typeof(LootRegistry).GetField("vanillaGroups", Hidden).GetValue(null);
        groups["dumpingGroundTG"] = new List<LootEntry> { new LootEntry("junkTable", 650), new LootEntry("materialTable", 350) };
        groups["dumpingGroundFreshTG"] = new List<LootEntry> { new LootEntry("junkTable", 650), new LootEntry("materialTable", 350) };
        var tables = (Dictionary<string, List<LootEntry>>)typeof(LootRegistry).GetField("vanillaTables", Hidden).GetValue(null);
        tables["junkTable"] = new List<LootEntry> { new LootEntry("junk", 1) };
        tables["materialTable"] = new List<LootEntry> { new LootEntry("nuts_metal", 1) };
        typeof(LootRegistry).GetField("dirty", Hidden).SetValue(null, true);
    }
    private static PlayerStore NewRun(int day)
    {
        var store = (PlayerStore)RuntimeHelpers.GetUninitializedObject(typeof(PlayerStore));
        store.modData = new Dictionary<string, string> { { "station_scavenging:route_seed", "1234" } };
        store.startingPerks = new List<StartingPerk>(); store.storeReputations = new List<StoreReputation>();
        store.afterhourState = new Dictionary<string, AfterhourLocationState>(); store.healthData = new HealthData();
        store.difficultyId = "normal"; store.storeState = PlayerStore.StoreState.CLOSED;
        typeof(PlayerStore).GetField("instance", Hidden).SetValue(null, store);
        var calendar = (StoreStation)RuntimeHelpers.GetUninitializedObject(typeof(StoreStation)); calendar.dayCounter = day;
        typeof(StoreStation).GetField("instance", Hidden).SetValue(null, calendar);
        SetSingleton(typeof(EmporiumEntry)); SetSingleton(typeof(StoreUIManager)); SetSingleton(typeof(AudioManager));
        typeof(UnityEngine.Time).GetField("CodexFrame").SetValue(null, frame);
        typeof(UnityEngine.Time).GetField("CodexTime").SetValue(null, (float)frame);
        Call("Reload"); return store;
    }
    private static void SetSingleton(Type type)
    {
        var field = type.GetField("instance", Hidden) ?? type.GetField("<Instance>k__BackingField", Hidden) ?? type.GetField("mInstance", Hidden);
        field.SetValue(null, RuntimeHelpers.GetUninitializedObject(type));
    }
    private static void SetRank(string id, int value)
    {
        skills.GetType("ScavengerSkills.Skills").GetMethod("Read", Hidden).Invoke(null, null);
        ModHelper.SetModDataInt("eotn_scavenger_skills", "skills.rank." + id, value);
    }
    private static void AddMod(string id, string version, Assembly asm, IMod instance)
    {
        ModLoader.Mods.Add(new LoadedMod { Manifest = new ModManifest { ID = id, Name = id, ModVersion = version }, Assembly = asm, Instance = instance, IsEnabled = true });
    }
    private static void PatchClass(Harmony harmony, Assembly asm, string name) { harmony.CreateClassProcessor(asm.GetType(name, true)).Patch(); }
    private static HarmonyMethod HM(string name) { return new HarmonyMethod(typeof(InstalledPeerIntegration).GetMethod(name)); }
    private static object Call(string name, params object[] values) { return game.GetMethod(name, Hidden).Invoke(null, values); }
    private static object Night() { return game.GetProperty("Night", Hidden).GetValue(null); }
    private static object Outing(string region) { return Call("GetOuting", Enum.Parse(station.GetType("StationScavenging.Region"), region)); }
    private static object Field(Type type, string name) { return type.GetField(name, Hidden).GetValue(null); }
    private static int Int(object value, string name) { var f = value.GetType().GetField(name); return (int)(f == null ? value.GetType().GetProperty(name).GetValue(value) : f.GetValue(value)); }
    private static void Update(double chance) { lower.GetMethod("UpdateCrateWeight", Hidden).Invoke(null, new object[] { chance }); }
    private static int Hits() { return (int)better.GetType("BetterScavenging.BoxDrop").GetField("_hits", Hidden).GetValue(null); }
    private static string NormalRoll(Func<string, float> scale)
    {
        Samples(); return LootRegistry.RollGroup("dumpingGroundTG", scale);
    }
    private static void Samples()
    {
        typeof(UnityEngine.Random).GetField("CodexSamples").SetValue(null, new float[] { (float)groupRoll, (float)tableRoll });
        typeof(UnityEngine.Random).GetField("CodexSampleIndex").SetValue(null, 0);
        typeof(UnityEngine.Random).GetField("CodexRangePercent").SetValue(null, boxRoll);
        typeof(UnityEngine.Time).GetField("CodexFrame").SetValue(null, frame);
    }
    private static void NextFrame() { frame++; typeof(UnityEngine.Time).GetField("CodexFrame").SetValue(null, frame); }
    private static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); checks++; }
    private static void Near(double actual, double expected, string label) { Check(Math.Abs(actual - expected) < 0.000001, label + " actual=" + actual); }
    public static bool Skip() { return false; }
    public static bool Localized(string __0, ref string __result) { __result = __0; return false; }
    public static bool LogMessage(object __0) { if (__0 != null && __0.ToString().Contains("failed")) Console.WriteLine(__0); return false; }
    public static bool TrueResult(ref bool __result) { __result = true; return false; }
    public static bool FalseResult(ref bool __result) { __result = false; return false; }
    public static bool ZeroFloat(ref float __result) { __result = 0; return false; }
    public static bool EqualObject(UnityEngine.Object __0, UnityEngine.Object __1, ref bool __result) { __result = ReferenceEquals(__0, __1); return false; }
    public static bool UnequalObject(UnityEngine.Object __0, UnityEngine.Object __1, ref bool __result) { __result = !ReferenceEquals(__0, __1); return false; }
    public static bool FindItem(string identifier, ref GameItem __result) { __result = identifier == "scav_gloves" ? gloves : null; return false; }
    public static bool HasItem(string identifier, ref bool __result) { __result = identifier == "scav_gloves" && gloves != null; return false; }
    public static bool IsOutside(ref bool __result) { __result = currentLocation != null; return false; }
    public static bool Location(ref string __result) { __result = currentLocation; return false; }
    public static bool SpawnItem(string __0, ref GameItem __result) { __result = Item(__0); return false; }
    public static bool Place(GameItem __1, ref int __result) { placed.Add(__1); __result = 0; return false; }
    public static float FixedTime() { return frame; }
    public static float FixedRng(float min, float max) { return 0; }
    public static IEnumerable<CodeInstruction> FalseBody(IEnumerable<CodeInstruction> source) { return new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Ret) }; }
    public static IEnumerable<CodeInstruction> ZeroBody(IEnumerable<CodeInstruction> source) { return new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldc_R4, 0f), new CodeInstruction(OpCodes.Ret) }; }
    public static int FixedFrame() { return frame; }
    public static float NextValue() { return (float)(randomCalls++ % 2 == 0 ? groupRoll : tableRoll); }
    private static int randomCalls;
    public static IEnumerable<CodeInstruction> TimeBoundary(IEnumerable<CodeInstruction> source)
    {
        var result = new List<CodeInstruction>();
        foreach (var ins in source)
        {
            if (ins.operand is MethodInfo m && m.DeclaringType == typeof(UnityEngine.Time) && (m.Name == "get_frameCount" || m.Name == "get_unscaledTime"))
            { ins.opcode = OpCodes.Call; ins.operand = typeof(InstalledPeerIntegration).GetMethod(m.Name == "get_frameCount" ? "FixedFrame" : "FixedTime"); result.Add(ins); }
            else if (ins.operand is MethodInfo rng && rng.DeclaringType == typeof(RNG) && rng.Name == "GetRandomFloat")
            { ins.opcode = OpCodes.Call; ins.operand = typeof(InstalledPeerIntegration).GetMethod("FixedRng"); result.Add(ins); }
            else result.Add(ins);
        }
        return result;
    }
    public static IEnumerable<CodeInstruction> RandomBoundary(IEnumerable<CodeInstruction> source)
    {
        var result = new List<CodeInstruction>();
        foreach (var ins in source)
        {
            if (ins.operand is MethodInfo m && m.DeclaringType == typeof(UnityEngine.Random) && m.Name == "get_value")
            { ins.opcode = OpCodes.Call; ins.operand = typeof(InstalledPeerIntegration).GetMethod("NextValue"); result.Add(ins); }
            else result.Add(ins);
        }
        return result;
    }
    private static GameItem Item(string id) { return new ManagedItem(id); }
    private sealed class ManagedItem : GameItem
    {
        internal ManagedItem(string id) : base(id) { identifier = id; state = new TagSystem(); state.EnableTag("IS_OWNED_TAG"); modifiedState = state; unitValue = 10; unitCount = 1; }
        public override void Destroy() { unitCount = 0; parentInventory = null; }
        public override void Validate() { }
    }
    private sealed class Probe : AfterhourLocation
    {
        public override string Id { get { return "dumping_ground"; } }
        public override string GetTitle() { return "Native arrival fixture"; }
        public override void Arrive() { ScavHelper.ResetScavenging(); }
    }
}
