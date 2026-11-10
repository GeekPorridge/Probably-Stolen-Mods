using System;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class NightFishBalance
    {
        // Adult catch yields. Body mass still follows native feed absorption;
        // lifetime maintenance calories are not all harvestable meat.
        public static readonly int[] AdultCalories={600,900,1600,1200};
        public static int Limit(GameItem fish,int nativeCalories)
        {
            var species=AquacultureHelper.GetSpecies(fish);
            if(species==null || !AquacultureHelper.IsFish(fish)) return nativeCalories;
            int index=Array.FindIndex(ExpansionStock.FishKeys,key=>ExpansionStock.Fish.TryGetValue(key,out var current) && current.speciesID==species.speciesID);
            if(index<0) return nativeCalories;
            int adultWeight=Math.Max(1,species.stageWeightGrams[4]);
            int cap=(int)Math.Floor(AdultCalories[index]*(double)Math.Max(0,AquacultureHelper.GetWeightGrams(fish))/adultWeight);
            return Math.Max(0,Math.Min(nativeCalories,cap));
        }
    }
    [HarmonyPatch(typeof(AquacultureHelper),nameof(AquacultureHelper.GetMeatKcal))]
    public static class NightFishCaloriesPatch
    {
        public static void Postfix(GameItem item,ref int __result)
        {if(ModEntry.Enabled) __result=NightFishBalance.Limit(item,__result);}
    }
    [HarmonyPatch(typeof(AquacultureHelper),nameof(AquacultureHelper.CreateFilletPieces))]
    public static class NightFishCarcassPatch
    {
        public static void Prefix(GameItem fish)
        {
            if(!ModEntry.Enabled || !AquacultureHelper.IsDead(fish)) return;
            int old=fish.GetTagReadonly("FISH_CARCASS_KCAL_TAG").GetInt();
            int value=NightFishBalance.Limit(fish,old);
            if(value<old) Crops.SetInt(fish,"FISH_CARCASS_KCAL_TAG",value);
        }
    }
}
