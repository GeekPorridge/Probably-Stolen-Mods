using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PelicanCultivation;

public static class TownDialogueVerification
{
    private static int checks,capabilities,draws;
    private static readonly Dictionary<string,int> data=new Dictionary<string,int>();
    private static RunStats stats;
    private static void Check(bool result,string context){if(!result) throw new Exception("DIALOGUE FAIL: "+context);checks++;}
    public static bool Get(string key,int fallback,ref int __result){__result=data.TryGetValue(key,out int value)?value:fallback;return false;}
    public static bool Set(string key,int value){data[key]=value;return false;}
    public static bool Ready(ref bool __result){__result=true;return false;}
    public static bool Capabilities(ref int __result){__result=capabilities;return false;}
    public static bool Farming(ref bool __result){__result=(capabilities&TownDialogue.Farming)!=0;return false;}
    public static bool Advanced(ref bool __result){__result=(capabilities&TownDialogue.AdvancedAquaculture)!=0;return false;}
    public static bool Stats(ref RunStats __result){__result=stats;return false;}
    public static bool Roll(int __0,int __1,ref int __result){draws++;__result=__0;return false;}
    public static string Run()
    {
        checks=0;
        Check(TownDialogue.Priority(1,true,31,0,5000,0)==1,"first arrival introduces the farmer before other events");
        Check(TownDialogue.Priority(3,true,31,0,5000,0)==2,"pending ancient-seed gift retains priority");
        foreach(var pair in new[]{(1,10),(2,11),(4,12),(8,13),(16,14)})
            Check(TownDialogue.Priority(2,false,pair.Item1,0,0,0)==pair.Item2,"each unlocked ability has its own announcement");
        Check(TownDialogue.Priority(2,false,31,31,999,0)==0 && TownDialogue.Priority(2,false,31,31,1000,0)==20,"first income threshold boundary");
        Check(TownDialogue.Priority(2,false,31,31,2000,1)==21 && TownDialogue.Priority(2,false,31,31,5000,2)==22,"higher revenue milestones");
        Check(TownDialogue.Priority(2,false,31,31,5000,0)==22 && TownDialogue.Priority(2,false,31,31,5000,3)==0,"crossed historical milestones use the highest one and never repeat");
        Check(TownDialogue.Revenue(new RunStats{totalSales=900,vendingIncome=200,fountainIncome=100})==1200,"revenue includes native counter, vending and fountain income");
        Check(TownDialogue.Revenue(new RunStats{totalSales=int.MaxValue,vendingIncome=int.MaxValue,fountainIncome=int.MaxValue})==3L*int.MaxValue,"revenue sum cannot overflow a 32-bit integer");
        var seeds=TownDialogue.CasualPool(3,1);Check(seeds.Length==6 && seeds.All(c=>c>=100 && c<200),"Wednesday uses only available farming chatter");
        var ranch=TownDialogue.CasualPool(6,4);Check(ranch.Length==6 && ranch.All(c=>c>=200 && c<300),"mammal-only Saturday never uses aquatic chatter");
        var aqua=TownDialogue.CasualPool(6,8);Check(aqua.Length==4 && aqua.All(c=>c>=300 && c<304),"base aquaculture never mentions advanced lobster stock");
        Check(TownDialogue.CasualPool(6,28).Length==12,"mixed advanced Saturday has both livestock and aquatic chatter");
        foreach(int previous in seeds) Check(TownDialogue.ChooseCasual(seeds,previous,0)!=previous,"casual text avoids an immediate repeat");
        var harmony=new Harmony("pelican_cultivation.dialogue_verification");
        try
        {
            Patch(harmony,AccessTools.Method(typeof(Progress),"Get"),"Get");
            Patch(harmony,AccessTools.Method(typeof(Progress),"Set"),"Set");
            Patch(harmony,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"Ready");
            Patch(harmony,AccessTools.PropertyGetter(typeof(TownDialogue),"Capabilities"),"Capabilities");
            Patch(harmony,AccessTools.PropertyGetter(typeof(FarmStock),"Farming"),"Farming");
            Patch(harmony,AccessTools.PropertyGetter(typeof(FarmStock),"Advanced"),"Advanced");
            Patch(harmony,AccessTools.PropertyGetter(typeof(RunStats),"Current"),"Stats");
            Patch(harmony,AccessTools.Method(typeof(RNG),"GetRandomInt",new[]{typeof(int),typeof(int)}),"Roll");
            data.Clear();stats=new RunStats();capabilities=1;draws=0;
            data["farmer_visits"]=0;TownDialogue.Initialize();
            data["stock_day"]=3;data["farmer_visits"]=1;TownDialogue.PrepareVisit();
            Check(TownDialogue.CurrentCode==1,"actual visit preparation persists the first-introduction code");
            stats.totalSales=5000;capabilities=31;TownDialogue.PrepareVisit();
            Check(TownDialogue.CurrentCode==1 && draws==0,"reload/preparation within a visit cannot change its dialogue or draw randomness");
            TownDialogue.Acknowledge(22,3);Check(!data.ContainsKey("farmer_dialogue_income_level"),"stale callbacks cannot acknowledge a different dialogue");
            capabilities=1;stats.totalSales=0;data["stock_day"]=10;data["farmer_visits"]=2;TownDialogue.PrepareVisit();
            Check(TownDialogue.CurrentCode==10,"newly learned farming gets its own visit");
            TownDialogue.Acknowledge(10,10);Check(data["farmer_dialogue_abilities"]==1,"ability announcements are acknowledged only after their dialogue");
            data["stock_day"]=17;data["farmer_visits"]=3;TownDialogue.PrepareVisit();Check(TownDialogue.CurrentCode==2,"third-visit gift is still announced");
            data["gift_delivered"]=1;data["stock_day"]=24;data["farmer_visits"]=4;stats.totalSales=5000;TownDialogue.PrepareVisit();
            Check(TownDialogue.CurrentCode==22,"native saved revenue chooses the highest earned milestone");
            TownDialogue.Acknowledge(22,24);TownDialogue.Acknowledge(22,24);Check(data["farmer_dialogue_income_level"]==3,"repeated acknowledgements cannot duplicate milestone rewards or reset progress");
            data["stock_day"]=31;data["farmer_visits"]=5;TownDialogue.PrepareVisit();int chosen=TownDialogue.CurrentCode;
            Check(chosen>=100 && chosen<106 && draws==1,"ordinary Wednesday selects contextual random chatter after milestones");
            TownDialogue.Acknowledge(chosen,31);
            var saved=new Dictionary<string,int>(data);data.Clear();foreach(var entry in saved) data[entry.Key]=entry.Value;
            TownDialogue.PrepareVisit();Check(TownDialogue.CurrentCode==chosen && draws==1,"restored visit state does not reroll its text");
            data["stock_day"]=38;data["farmer_visits"]=6;TownDialogue.PrepareVisit();Check(TownDialogue.CurrentCode!=chosen,"a subsequent ordinary visit avoids the previous spoken line");
            capabilities=31;data["stock_day"]=41;data["stock_left_nutrifruit_small"]=3;FarmStock.RefreshPending();Check(data["stock_left_nutrifruit_small"]==0,"saved unsent Saturday Nutrifruit is removed without rerolling stock");
            Check(data["stock_left_bait"]==5&&data["stock_left_premium_bait"]==3,"saved advanced Saturday receives the new ordinary and premium bait offer");
            data["stock_day"]=45;data["stock_left_nutrifruit_small"]=3;FarmStock.RefreshPending();Check(data["stock_left_nutrifruit_small"]==3,"Wednesday bonus Nutrifruit stays available");
            bool oldEnabled=ModEntry.Enabled;
            try{ModEntry.Enabled=true;Console.WriteLine(SeaFounderVerification.Run(data));}
            finally{ModEntry.Enabled=oldEnabled;}
        }
        finally{harmony.UnpatchAll(harmony.Id);data.Clear();}
        return "Passed "+checks+" dialogue and persisted-visit checks (abilities, revenue, replay, stock migration and contextual random text).";
    }
    private static void Patch(Harmony harmony,System.Reflection.MethodInfo target,string prefix)
    {harmony.Patch(target,new HarmonyMethod(typeof(TownDialogueVerification).GetMethod(prefix)));}
}
