using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

public static class CountBonusIntegration
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstanceHidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Assembly mod;
    private static Type rules;
    private static int checks, skillBonus, equipmentBonus;

    public static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("MONOMOD_DMDType", "cecil");
        string managed = Path.Combine(args[0], "Probably Stolen_Data/Managed");
        string modPath = args[1];
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            string name = new AssemblyName(e.Name).Name;
            string path = name == "StationScavenging" ? modPath : Path.Combine(managed, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { return Run(modPath); }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string modPath)
    {
        mod = Assembly.LoadFrom(modPath);
        rules = mod.GetType("StationScavenging.StationGame");
        var first = new Harmony("station_scavenging.test.skill-count-fixture");
        var second = new Harmony("station_scavenging.test.equipment-count-fixture");
        try
        {
            // Model two independent mods patching the native count API. These
            // are compatibility fixtures, not either author's original binary.
            first.Patch(typeof(ScavHelper).GetMethod("GetMaxScavAttempts"),
                postfix: new HarmonyMethod(typeof(CountBonusIntegration).GetMethod("SkillBonus")));
            second.Patch(typeof(ScavHelper).GetMethod("GetMaxScavAttempts"),
                postfix: new HarmonyMethod(typeof(CountBonusIntegration).GetMethod("EquipmentBonus")));
            foreach (var pair in new[] { new[] { 0, 0 }, new[] { 4, 0 }, new[] { 0, 6 }, new[] { 4, 6 } })
            {
                skillBonus = pair[0]; equipmentBonus = pair[1];
                var store = NewRun(150);
                int total = 10 + skillBonus + equipmentBonus;
                Check(Int(Night(), "Total") == total, "Independent native count bonuses stack into one shared total");
                var lower = Outing("Lower");
                var upper = Outing("Upper");
                var bridgeType = mod.GetType("StationScavenging.LowerLevelsLocation");
                var bridge = (AfterhourLocation)Activator.CreateInstance(bridgeType, InstanceHidden, null, new object[] { new Probe() }, null);
                var stateField = mod.GetType("StationScavenging.ScavengingLocation").GetField("state", InstanceHidden);
                var arrive = bridgeType.GetMethod("EnterNative", InstanceHidden);
                stateField.SetValue(bridge, lower);
                arrive.Invoke(bridge, null);
                Check((bool)Call("SpendSearch", lower), "Lower search uses combined allowance");
                for (int n = 0; n < 4; n++) Check((bool)Call("SpendSearch", upper), "Advanced search uses the same bonus allowance");
                store.visitLeftTonight = 3;
                bridge.Leave();
                for (int visit = 0; visit < 3; visit++)
                {
                    arrive.Invoke(bridge, null);
                    Check(!(bool)lower.GetType().GetProperty("Ended").GetValue(lower), "Repeated real lower arrival resumes a departed visit");
                    Check(Int(Night(), "Remaining") == total - 5 && store.scavengingAttempts == 5, "Repeated native reset cannot replenish the combined budget");
                    Check(store.visitLeftTonight == 3, "Shared searches never consume or grant external outing opportunities");
                    bridge.Leave();
                }
                Call("Reload");
                lower = Outing("Lower"); stateField.SetValue(bridge, lower); arrive.Invoke(bridge, null);
                Check(Int(Night(), "Remaining") == total - 5, "Native count patches and saved departure survive reload together");
                for (int n = 5; n < total; n++) Check((bool)Call("SpendSearch", lower), "All remaining bonus actions can be spent");
                bridge.Leave(); arrive.Invoke(bridge, null);
                Check(!(bool)Call("SpendSearch", lower) && Int(Night(), "Remaining") == 0, "Bonus actions cannot be refreshed after exhaustion");

                store = NewRun(151);
                var active = new AfterhourActiveEvent { eventId = "georgio_assistance", endsOnDay = 151 };
                store.GetAfterhourState("dumping_ground").activeEvents.Add(active);
                Check(Int(Night(), "Total") == total + 5, "Georgio's native +5 stacks once with both count patches");
            }
            skillBonus = 4; equipmentBonus = 6;
            var blocked = NewRun(152);
            blocked.startingPerks.Add(new StartingPerk { id = "maimed" });
            Check(Int(Night(), "Total") == 0, "Native disability cutoff remains zero with additive bonus fixtures");
            Console.WriteLine("Passed " + checks + " native Harmony compatibility checks: two independent count patches, repeat lower arrivals, reloads, exhaustion and Georgio stacking. External mod binaries were not loaded.");
            return 0;
        }
        finally { second.UnpatchAll(second.Id); first.UnpatchAll(first.Id); }
    }

    public static void SkillBonus(ref int __result) { if (__result > 0) __result += skillBonus; }
    public static void EquipmentBonus(ref int __result) { if (__result > 0) __result += equipmentBonus; }
    private static object Call(string name, params object[] args) { return rules.GetMethod(name, Hidden).Invoke(null, args); }
    private static object Night() { return rules.GetProperty("Night", Hidden).GetValue(null); }
    private static object Outing(string region) { return Call("GetOuting", Enum.Parse(mod.GetType("StationScavenging.Region"), region)); }
    private static int Int(object value, string name)
    {
        var field = value.GetType().GetField(name);
        return (int)(field != null ? field.GetValue(value) : value.GetType().GetProperty(name).GetValue(value));
    }
    private static void Check(bool pass, string message) { if (!pass) throw new Exception("FAIL: " + message); checks++; }
    private static PlayerStore NewRun(int day)
    {
        var store = (PlayerStore)RuntimeHelpers.GetUninitializedObject(typeof(PlayerStore));
        store.modData = new Dictionary<string, string> { { "station_scavenging:route_seed", "1234" } };
        store.startingPerks = new List<StartingPerk>();
        store.storeReputations = new List<StoreReputation>();
        store.afterhourState = new Dictionary<string, AfterhourLocationState>();
        store.healthData = new HealthData(); store.difficultyId = "normal";
        store.storeState = PlayerStore.StoreState.CLOSED;
        typeof(PlayerStore).GetField("instance", Hidden).SetValue(null, store);
        var station = (StoreStation)RuntimeHelpers.GetUninitializedObject(typeof(StoreStation));
        station.dayCounter = day;
        typeof(StoreStation).GetField("instance", Hidden).SetValue(null, station);
        Call("Reload"); return store;
    }
    private sealed class Probe : AfterhourLocation
    {
        public override string Id { get { return "dumping_ground"; } }
        public override string GetTitle() { return "Headless lower arrival fixture"; }
        public override void Arrive() { ScavHelper.ResetScavenging(); }
    }
}
