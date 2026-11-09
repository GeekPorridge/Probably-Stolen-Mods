using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using Newtonsoft.Json;
using PelicanCultivation;

public static class ReservoirEventVerification
{
    private static int checks,day,rolls;
    private static float sample;
    private static bool unlocked;
    private static PlayerStore store;
    private static void Check(bool condition,string message){if(!condition)throw new Exception("RESERVOIR EVENT FAIL: "+message);checks++;}
    public static bool Ready(ref bool __result){__result=true;return false;}
    public static bool Unlocked(ref bool __result){__result=unlocked;return false;}
    public static bool Day(ref int __result){__result=day;return false;}
    public static bool Store(ref PlayerStore __result){__result=store;return false;}
    public static bool Roll(float __0,ref bool __result){rolls++;__result=sample<__0;return false;}
    private static void Patch(Harmony h,System.Reflection.MethodBase method,string prefix)=>h.Patch(method,new HarmonyMethod(typeof(ReservoirEventVerification),prefix));
    private static PlayerStore NewStore()=>(PlayerStore)FormatterServices.GetUninitializedObject(typeof(PlayerStore));
    private static void Reload()
    {
        // Both dictionaries are native per-run save fields, not process caches.
        string modData=JsonConvert.SerializeObject(store.ModData());
        string events=JsonConvert.SerializeObject(AccessTools.Field(typeof(PlayerStore),"afterhourState").GetValue(store));
        store=NewStore();foreach(var pair in JsonConvert.DeserializeObject<Dictionary<string,string>>(modData))store.ModData()[pair.Key]=pair.Value;
        AccessTools.Field(typeof(PlayerStore),"afterhourState").SetValue(store,JsonConvert.DeserializeObject<Dictionary<string,AfterhourLocationState>>(events));
    }
    public static string Run()
    {
        checks=rolls=0;day=7;unlocked=true;sample=0;store=NewStore();
        var locations=AfterhourLocation.GetAll();var previousLocations=locations.ToArray();
        var definitions=AfterhourEvents.GetAll();var previousDefinitions=definitions.ToArray();
        var h=new Harmony("pelican_cultivation.reservoir_event_verification");
        try
        {
            Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"Ready");Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Day"),"Day");
            Patch(h,AccessTools.PropertyGetter(typeof(PlayerStore),"Instance"),"Store");Patch(h,AccessTools.PropertyGetter(typeof(PlayerStore),"HasInstance"),"Ready");
            Patch(h,AccessTools.PropertyGetter(typeof(FarmStock),"Aquaculture"),"Unlocked");Patch(h,AccessTools.PropertyGetter(typeof(AfterhourEvents),"Today"),"Day");
            Patch(h,AccessTools.Method(typeof(RNG),"Roll",new[]{typeof(float)}),"Roll");
            locations.Clear();locations.Add(new ReservoirLocation());definitions.Clear();ReservoirEvents.Enable();
            var definition=AfterhourEvents.GetDefinition(ReservoirEvents.ActivityId);
            Check(definition!=null&&definition.source==ModEntry.ModId&&definition.locationId==Fishing.LocationId&&definition.durationDays==1&&definition.dailyChancePercent==20,"activity is a native twenty-percent one-night reservoir event");
            Check(definition.GetTone()==AfterhourEventTone.Good&&definition.GetModifier(ReservoirEvents.Modifier)==1&&!definition.closesLocation,"positive native event exposes a doubling modifier without closing the destination");
            Check(!string.IsNullOrWhiteSpace(definition.GetTitle())&&!string.IsNullOrWhiteSpace(definition.GetDescription())&&definition.GetEffectTexts().Count==1,"native transit cards have title, description and effect text");
            AfterhourEvents.OnNewDay();
            Check(rolls==1&&Progress.Get(ReservoirEvents.RollDay)==day&&ReservoirEvents.Multiplier==2&&AfterhourEvents.GetDaysLeft(ReservoirEvents.ActivityId)==1,"actual native new-day loop starts a winning roll and stores today's marker");
            for(int n=0;n<4;n++){ReservoirEvents.EnsureToday();AfterhourEvents.OnNewDay();}
            Check(rolls==1&&store.GetAfterhourState(Fishing.LocationId).activeEvents.Count==1,"reopening and duplicate new-day calls cannot reroll or stack today's event");
            Reload();ReservoirEvents.Enable();ReservoirEvents.EnsureToday();
            Check(rolls==1&&ReservoirEvents.Multiplier==2&&AfterhourEvents.GetActive(Fishing.LocationId).Count==1,"native saved event and per-run roll marker survive reload without rerolling");
            day++;sample=99;AfterhourEvents.OnNewDay();
            Check(rolls==2&&ReservoirEvents.Multiplier==1&&AfterhourEvents.GetActive(Fishing.LocationId).Count==0&&Progress.Get(ReservoirEvents.RollDay)==day,"next native day expires the activity and persists a losing roll");
            Reload();ReservoirEvents.EnsureToday();AfterhourEvents.OnNewDay();
            Check(rolls==2&&ReservoirEvents.Multiplier==1,"an unsuccessful day's save also prevents rerolling");
            day++;unlocked=false;ReservoirEvents.EnsureToday();AfterhourEvents.OnNewDay();
            Check(rolls==2&&Progress.Get(ReservoirEvents.RollDay)==day-1,"locked aquaculture neither consumes a roll nor activates the reservoir event");
            unlocked=true;sample=0;ReservoirEvents.EnsureToday();
            Check(rolls==3&&ReservoirEvents.Multiplier==2&&AfterhourEvents.GetActive(Fishing.LocationId).Count==1,"unlock/install-day fallback starts the same native saved event once");
            ReservoirEvents.EnsureToday();AfterhourEvents.OnNewDay();Check(rolls==3,"fallback and the native day loop share one persisted roll");
            foreach(bool premium in new[]{false,true})
            {
                int[] counts=new int[9];int[] ready=new int[4];
                for(int n=0;n<100000;n++)counts[Fishing.Select((n+.5)/100000,.2,premium,day,ready,2)]++;
                Check(counts[4]==(premium?1500:1000)&&counts[5]==(premium?3000:2000)&&counts[6]==counts[5]&&counts[7]==counts[5],"every legend interval doubles while premium keeps its independent x1.5 multiplier");
                Check(counts[8]==(premium?3000:8000),"event leaves the ordinary/premium junk chance unchanged");
                ready[0]=day+1;
                Check(Fishing.Select(.001,.8,premium,day,ready,2)==2&&Fishing.Select(.02,.8,premium,day,ready,2)==5,"legend cooldown still redirects only its own probability to ordinary fish");
            }
            ReservoirEvents.Disable();Check(AfterhourEvents.GetDefinition(ReservoirEvents.ActivityId)==null,"disable removes event definitions without changing another source");
            ReservoirEvents.Enable();Check(ReservoirEvents.Multiplier==2&&store.GetAfterhourState(Fishing.LocationId).activeEvents.Count==1,"reenable retains a saved active night without adding another event");
            return "Passed "+checks+" native reservoir event, saved daily-roll, probability and cooldown checks.";
        }
        finally{h.UnpatchAll(h.Id);locations.Clear();locations.AddRange(previousLocations);definitions.Clear();definitions.AddRange(previousDefinitions);store=null;}
    }
}
