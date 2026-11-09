using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PelicanCultivation;

public static class ManualStockVerification
{
    private static readonly Dictionary<string,int> data=new Dictionary<string,int>();
    private static bool advanced;
    private static int checks;
    private static void Check(bool value,string message){if(!value)throw new Exception("MANUAL/STOCK FAIL: "+message);checks++;}
    public static bool Get(string key,int fallback,ref int __result){__result=data.TryGetValue(key,out int value)?value:fallback;return false;}
    public static bool Set(string key,int value){data[key]=value;return false;}
    public static bool Advanced(ref bool __result){__result=advanced;return false;}
    public static string Run()
    {
        checks=0;
        Check(FarmStock.Entries.Single(e=>e.Key=="bait").Id=="pelican_bait"&&FarmStock.Entries.Single(e=>e.Key=="premium_bait").Id=="pelican_premium_bait","both merchant entries use real inventory bait");
        for(int day=1;day<=14;day++)for(int mask=0;mask<8;mask++)
        {
            bool mammals=(mask&1)!=0,aquaculture=(mask&2)!=0,upgraded=(mask&4)!=0;
            var stock=FarmStock.Quantities(day,true,mammals,aquaculture,upgraded,true,true);
            bool saturday=day%7==6&&(mammals||aquaculture);
            Check(stock["bait"]==(saturday?5:0),"ordinary bait is five units only on an eligible Saturday");
            Check(stock["premium_bait"]==(saturday&&upgraded?3:0),"advanced aquaculture adds three premium units without replacing ordinary bait");
        }
        var harmony=new Harmony("pelican_cultivation.manual_stock_verification");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(Progress),"Get"),new HarmonyMethod(typeof(ManualStockVerification),"Get"));
            harmony.Patch(AccessTools.Method(typeof(Progress),"Set"),new HarmonyMethod(typeof(ManualStockVerification),"Set"));
            harmony.Patch(AccessTools.PropertyGetter(typeof(FarmStock),"Advanced"),new HarmonyMethod(typeof(ManualStockVerification),"Advanced"));
            foreach(bool unlocked in new[]{false,true})
            {
                advanced=unlocked;data.Clear();data["stock_day"]=6;data["stock_schema"]=1;data["stock_left_eel"]=0;
                FarmStock.RefreshPending();
                Check(data["stock_left_bait"]==5&&data["stock_left_premium_bait"]==(unlocked?3:0)&&data["stock_schema"]==FarmStock.StockSchema,"old Saturday gains the exact newly introduced offer once");
                data["stock_left_bait"]=1;data["stock_left_premium_bait"]=0;
                FarmStock.RefreshPending();FarmStock.RefreshPending();
                Check(data["stock_left_bait"]==1&&data["stock_left_premium_bait"]==0&&data["stock_left_eel"]==0,"reload cannot replenish purchased bait or reroll existing stock");
            }
            data.Clear();data["stock_day"]=3;data["stock_schema"]=1;FarmStock.RefreshPending();
            Check(!data.ContainsKey("stock_left_bait")&&!data.ContainsKey("stock_left_premium_bait"),"old Wednesday visit receives no Saturday bait");
            data.Clear();FarmStock.RefreshPending();
            Check(!data.ContainsKey("stock_left_bait"),"a new save with no visit cannot receive pending bait");
        }
        finally{harmony.UnpatchAll(harmony.Id);data.Clear();}
        return "Passed "+checks+" Saturday bait inventory, upgrade and persisted-stock checks.";
    }
}
