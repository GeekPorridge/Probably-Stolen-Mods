using System;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class Scent
    {
        public static int Strength(int native)=>native==1?15:native==2?25:native==20||native==30?native:0;
        public static int Current=>Progress.Ready&&PlayerStore.Instance.currentClientInstance!=null?Strength(PlayerStore.Instance.currentClientInstance.GetClientBlueprint().perfumeInfluence):0;
        public static bool CanUse(GameItem item,int strength)=>ModEntry.Enabled&&Progress.Ready&&GeneralHelper.IsItemOwned(item)&&PlayerStore.Instance.currentClientInstance!=null&&UseCountHelper.CanUse(item)&&Current<strength;
        public static void Init(GameItem item,int strength)
        {
            item.SetGameItemType("LUXURY_ITEM");ContrabandHelper.InitContrabandItem(item,2);AudioHelper.InitGlass(item);item.shortDescription=Expansion.L(strength==30?"fairy_perfume_desc":"poppy_perfume_desc");item.flavorText="";
            UseCountHelper.InitUseCountItem(item,3,true,true,true,1,strength==20?33:53);
            item.mayActivateSlotItemFunc=(s,inv,slot)=>CanUse(s,strength);item.canActivateSlotItemFunc=(s,inv,slot)=>CanUse(s,strength);
            item.onActivateSlotItemFunc=(s,inv,slot)=>{if(!CanUse(s,strength)){ModEntry.Message("v2_scent_no_need");return;}UseCountHelper.Use(s);PlayerStore.Instance.currentClientInstance.GetClientBlueprint().perfumeInfluence=strength;PlayerStore.Instance.RefreshCounterItem();};
        }
        public static void GuardNative(GameItem item,int strength)
        {
            if(!ModEntry.Enabled||item==null)return;
            var may=item.mayActivateSlotItemFunc;var can=item.canActivateSlotItemFunc;var action=item.onActivateSlotItemFunc;
            item.mayActivateSlotItemFunc=(s,i,m)=>(!ModEntry.Enabled||Current<strength)&&(may?.InvokeAllReduce(s,i,m,InvokeFuncExtensions.BoolReduceOR)??true);
            item.canActivateSlotItemFunc=(s,i,m)=>(!ModEntry.Enabled||Current<strength)&&(can?.InvokeAllReduce(s,i,m,InvokeFuncExtensions.BoolReduceOR)??true);
            item.onActivateSlotItemFunc=(s,i,m)=>{if(!ModEntry.Enabled||Current<strength)action?.Invoke(s,i,m);};
        }
        public static void Apply(GameItem item)
        {
            if(!ModEntry.Enabled||!Progress.Ready||PlayerStore.Instance.currentClientInstance==null)return;
            var client=PlayerStore.Instance.currentClientInstance.GetClientBlueprint();int pct=client.perfumeInfluence;if(pct!=20&&pct!=30)return;
            bool own=GeneralHelper.IsItemOwned(item);bool sell=own&&(client.clientIntent==StoreClient.ClientIntent.BUY||client.clientIntent==StoreClient.ClientIntent.SELLNBUY);
            bool buy=!own&&(client.clientIntent==StoreClient.ClientIntent.SELL||client.clientIntent==StoreClient.ClientIntent.SELLNBUY);if(!sell&&!buy)return;
            string category=sell?"PELICAN_SCENT_SELL":"PELICAN_SCENT_BUY";item.RemoveFeatureByCategory(category);
            var feature=new ItemFeature(sell?ItemFeature.FeatureType.TemporarySelling:ItemFeature.FeatureType.TemporaryBuying){identifier=category,category=category,valueStage=ItemFeature.ValueStage.Market,isFeatureMatch=true};
            feature.SetPublicDisplay(Expansion.L("scent_effect",pct)).SetActualDisplay(Expansion.L("scent_effect",pct)).SetPreExposedValueMod(sell?pct:-pct).SetValueModifier(sell?pct:-pct);item.AddItemFeature(feature);
        }
    }
    [HarmonyPatch(typeof(GameItem),nameof(GameItem.OnAddedToClientArea))]
    public static class ScentClientAreaPatch{public static void Postfix(GameItem __instance){Scent.Apply(__instance);}}
    [HarmonyPatch(typeof(GameItem),nameof(GameItem.OnAddedToWeightedArea))]
    public static class ScentWeightedAreaPatch{public static void Postfix(GameItem __instance){Scent.Apply(__instance);}}
    [HarmonyPatch(typeof(AmenitiesItemDirectory),"PheromonePerfume")]
    public static class ScentNativeStrongPatch{public static void Postfix(GameItem __result){Scent.GuardNative(__result,25);}}
    [HarmonyPatch(typeof(AmenitiesItemDirectory),"NeuroactivePerfume")]
    public static class ScentNativeWeakPatch{public static void Postfix(GameItem __result){Scent.GuardNative(__result,15);}}
    [HarmonyPatch(typeof(AmenitiesItemDirectory),"SelfRefillingNeuroactivePerfume")]
    public static class ScentNativeRefillPatch{public static void Postfix(GameItem __result){Scent.GuardNative(__result,15);}}
}
