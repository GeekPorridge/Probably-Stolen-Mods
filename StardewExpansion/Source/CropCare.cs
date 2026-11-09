using System;
using System.Collections.Generic;
using System.Globalization;

namespace PelicanCultivation
{
    public static class CropCare
    {
        public static void ClampGrowingProgress(GameItem seed,ref int progress)
        {
            if(seed==null || Rules.BySeed(HydroponicHelper.GetPlantId(seed))==null || !seed.IsTag(HydroponicHelper.REQUIRED_PROGRESS_INT)) return;
            int required=seed.GetTagReadonly(HydroponicHelper.REQUIRED_PROGRESS_INT).GetInt();
            int current=seed.GetTagReadonly(HydroponicHelper.GROWING_PROGRESS_INT).GetInt();
            // MachineHydroponic already advances its own progress. Only bound
            // the native care loop, which otherwise charges the entire speed
            // again even when the crop matures partway through this cycle.
            progress=Math.Max(0,Math.Min(progress,Math.Max(0,required-current)));
        }
        public static int Score(GameItem seed,int fallbackHealth=100)
        {
            if(seed==null) return Math.Max(0,Math.Min(100,fallbackHealth));
            int raw=seed.IsTag(HydroponicHelper.PLANT_HEALTH_INT)?seed.GetTagReadonly(HydroponicHelper.PLANT_HEALTH_INT).GetInt():fallbackHealth*100;
            int work=seed.GetTagReadonly(HydroponicHelper.REQUIRED_PROGRESS_INT).GetInt();
            if(work<=0) work=HydroponicHelper.GetRequiredProgress(seed);
            return Rules.CareScore(HydroponicHelper.GetStartingHealthPercent(seed),raw,work);
        }
        public static void RemoveRedundantTrait(GameItem seed)
        {
            if(seed!=null && Rules.BySeed(HydroponicHelper.GetPlantId(seed))!=null && PlantGeneticsHelper.GetTraitRank(seed,"edible")>0)
                PlantGeneticsHelper.SetTraitRank(seed,"edible",0);
        }
        public static int MineralState(int actual,int level,int range) => actual<level-range?-1:actual>level+range?1:0;
        public static bool HarmfulConcentration(int concentration,double sensitivity) => Math.Round(concentration*sensitivity)>10;
        public static void AddWaterDetails(RichTextBuilder builder,GameItem seed,GameItem water)
        {
            if(seed==null || water==null) return;
            int mineral=WaterHelper.GetContaminantPercentage(water,"mineral");
            int state=MineralState(mineral,HydroponicHelper.GetNutrimentLevel(seed),HydroponicHelper.GetNutrimentRange(seed));
            builder.AddLine(ModEntry.L("care_mineral_actual",(mineral/100.0).ToString("0.##",CultureInfo.InvariantCulture),ModEntry.L(state<0?"care_mineral_low":state>0?"care_mineral_high":"care_mineral_fit")));
            var risks=new List<string>();
            double sensitivity=HydroponicHelper.GetContaminantSensitivity(seed);
            foreach(string id in new[]{"heavy_metal","chemical_contaminant","microbe","organic_waste","acid","base"})
                if(HarmfulConcentration(WaterHelper.GetContaminantPercentage(water,id),sensitivity)) risks.Add(ModEntry.L("care_pollutant_"+id));
            if(risks.Count>0) builder.AddLine(ModEntry.L("care_pollution_risks",string.Join(", ",risks)));
        }
    }
}
