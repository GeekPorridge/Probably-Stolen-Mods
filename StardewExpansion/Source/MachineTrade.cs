using System;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class MachineTrade
    {
        public static int Kind(GameItem item)=>item?.identifier!=null&&item.identifier.StartsWith("pelican_")?Array.IndexOf(ArtisanMachines.Keys,item.identifier.Substring(8)):-1;
        public static long Resale(int kind)=>ArtisanMachines.Prices[kind]*16L/100;
        public static void Refresh(GameItem item)
        {int kind=Kind(item);if(kind>=0)item.unitValue=GeneralHelper.IsItemOwned(item)?Resale(kind):ArtisanMachines.Prices[kind];}
        public static long Clamp(GameItem item,long value,bool withChild=true)
        {
            int kind=Kind(item);if(!ModEntry.Enabled||kind<0||!GeneralHelper.IsItemOwned(item))return value;
            // Limit the chassis only. Player-supplied batteries, modules, wine
            // and other contents retain their own value rather than vanishing
            // into the used-machine discount. No inherited trader multiplier
            // is added to this ceiling.
            long ceiling=Resale(kind)+item.GetComplexeItemValue()+item.GetItemMiscValue(false,withChild,false,false,false);
            return Math.Min(value,Math.Max(0,ceiling));
        }
    }
    [HarmonyPatch(typeof(GeneralHelper),nameof(GeneralHelper.SetItemOwned))]
    public static class ArtisanOwnershipPricePatch
    {public static void Postfix(GameItem item){if(ModEntry.Enabled)MachineTrade.Refresh(item);}}
    [HarmonyPatch(typeof(GameItem),nameof(GameItem.GetNegociatedValue))]
    public static class ArtisanNegotiatedPricePatch
    {
        public static void Prefix(GameItem __instance){if(ModEntry.Enabled)MachineTrade.Refresh(__instance);}
        public static void Postfix(GameItem __instance,ref long __result){__result=MachineTrade.Clamp(__instance,__result);}
    }
    [HarmonyPatch(typeof(GameItem),nameof(GameItem.GetCurrentValue))]
    public static class ArtisanCurrentPricePatch
    {
        public static void Prefix(GameItem __instance){if(ModEntry.Enabled)MachineTrade.Refresh(__instance);}
        public static void Postfix(GameItem __instance,bool withChild,ref long __result){__result=MachineTrade.Clamp(__instance,__result,withChild);}
    }
    [HarmonyPatch(typeof(GameItem),nameof(GameItem.GetRefreshedValue))]
    public static class ArtisanRefreshedPricePatch
    {
        public static void Prefix(GameItem __instance){if(ModEntry.Enabled)MachineTrade.Refresh(__instance);}
        public static void Postfix(GameItem __instance,ref long __result){__result=MachineTrade.Clamp(__instance,__result);}
    }
}
