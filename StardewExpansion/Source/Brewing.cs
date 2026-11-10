using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PelicanCultivation
{
    public sealed class BrewPlan
    {
        public int Kind, Score, ConditionScore, SizeScore, YeastPower=1, IngredientCount, YeastCount;
        public bool HasCustom, Valid;
    }
    public static class Brewing
    {
        public static GameItem DebugFinished(int kind)
        {
            if(kind<1||kind>=Rules.WineKeys.Length)throw new ArgumentOutOfRangeException(nameof(kind));
            var bottle=WineHelper.CreateFinishedWineForDebug(3);
            Crops.SetInt(bottle,Rules.WineTag,kind);RefreshFinished(bottle,true);return bottle;
        }
        public static void RestoreNativeBottleId(GameItem bottle)
        {
            // Directory.Create stamps the cheat catalog alias on the item.
            // Once emptied, it must reload as an empty native bottle.
            if(Array.IndexOf(Rules.WineKeys,bottle.identifier!=null&&bottle.identifier.StartsWith("pelican_")?bottle.identifier.Substring(8):"")>0)bottle.identifier="wine_bottle";
        }
        public static BrewPlan Plan(IList<GameItem> items,int waterScore=0)
        {
            var plan=new BrewPlan();
            bool wrong=false;
            foreach(var item in items)
            {
                if(item==null) continue;
                int cropIndex=Crops.Index(item);
                if(cropIndex>=0 && cropIndex<Rules.Crops.Length)
                {
                    plan.HasCustom=true;
                    int kind=Rules.Crops[cropIndex].WineKind;
                    if(kind==0 || (plan.Kind!=0 && kind!=plan.Kind) || item.unitCount!=1) wrong=true;
                    if(plan.Kind==0) plan.Kind=kind;
                    plan.IngredientCount++;
                    plan.ConditionScore+=Rules.IngredientScore(item.GetTagReadonly(WineHelper.BERRY_CONDITION_INT).GetInt());
                    plan.SizeScore+=Rules.SizeScore(Rules.Crops[cropIndex],item.GetTagReadonly(Rules.SizeTag).GetInt());
                }
                else if(item.IsTag(WineHelper.WINE_YEAST_TAG))
                {
                    plan.YeastCount+=item.unitCount;
                    plan.YeastPower=Math.Max(1,item.GetTagReadonly(WineHelper.WINE_YEAST_POWER_INT).GetInt());
                }
                else wrong=true;
            }
            plan.Score=plan.ConditionScore+plan.SizeScore+1+(plan.YeastCount>0?1:0)+waterScore;
            plan.Valid=plan.HasCustom && !wrong && plan.Kind>0 && plan.IngredientCount==2 && plan.YeastCount<=1;
            return plan;
        }
        public static BrewPlan Plan(GameItem bottle) => Plan(GraphUtils.FindAllChildrenType<GameItem>(bottle),WineHelper.GetWaterQualityScore(bottle));
        public static bool ValidateStart(GameItem bottle,bool notify)
        {
            if(bottle.GetTagReadonly(WineHelper.WINE_CONTAINER_STATE).GetInt()!=0) return true;
            var plan=Plan(bottle);
            if(!plan.HasCustom) { ClearPlan(bottle); return true; }
            if(!plan.Valid)
            {
                if(notify) ModEntry.Message("recipe_error");
                return false;
            }
            StampPlan(bottle,plan);
            return true;
        }
        public static void StampPlan(GameItem bottle,BrewPlan plan)
        {
            Crops.SetInt(bottle,Rules.WineTag,plan.Kind);
            Crops.SetInt(bottle,Rules.SizeScoreTag,plan.SizeScore);
            // Native StartFermentation adds condition, paired-input, yeast and
            // water scores. Seed only the size bonus here, once per attempt.
            Crops.SetInt(bottle,WineHelper.WINE_SCORE_INT,plan.SizeScore);
        }
        public static void ClearPlan(GameItem bottle)
        {
            if(bottle.IsTag(Rules.WineTag)) Crops.SetInt(bottle,WineHelper.WINE_SCORE_INT,0);
            bottle.DisableTag(Rules.WineTag);bottle.DisableTag(Rules.SizeScoreTag);
        }
        public static void AfterStart(GameItem bottle)
        {
            int kind=bottle.GetTagReadonly(Rules.WineTag).GetInt();
            if(kind<1 || kind>=Rules.WineWork.Length || bottle.GetTagReadonly(WineHelper.WINE_CONTAINER_STATE).GetInt()!=1) return;
            Crops.SetInt(bottle,WineHelper.FERMENTATION_TIME_REMAINING_INT,Rules.WineWork[kind]);
            bottle.name=ModEntry.L("fermenting_name",ModEntry.L("wine_"+Rules.WineKeys[kind]));
            bottle.Validate();
        }
        public static void RefreshFinished(GameItem bottle,bool newlyFinished)
        {
            WineTransferProtection.Attach(bottle);
            int kind=bottle.GetTagReadonly(Rules.WineTag).GetInt();
            if(kind<1 || kind>=Rules.WinePrices.Length || !WineHelper.IsFinishedWine(bottle) || bottle.IsTag(WineHelper.WINE_COUNTERFEIT_TAG)) return;
            if(newlyFinished) bottle.unitValue=Rules.WinePrices[kind];
            bottle.name=ModEntry.L("wine_"+Rules.WineKeys[kind]);
            bottle.shortDescription=ModEntry.L("wine_desc_"+Rules.WineKeys[kind]);
            bottle.flavorText="";
            ApplyFinishedClassification(bottle,kind);
            BottleGeometry.ApplyFinished(bottle,kind);
            Barrel.RefreshQuality(bottle);
            bottle.Validate();
        }
        public static string FinishedName(GameItem bottle)
        {
            int kind=bottle.GetTagReadonly(Rules.WineTag).GetInt();
            return kind>0 && kind<Rules.WineKeys.Length && WineHelper.IsFinishedWine(bottle) && !bottle.IsTag(WineHelper.WINE_COUNTERFEIT_TAG)
                ?ModEntry.L("wine_"+Rules.WineKeys[kind]):null;
        }
        public static void ApplyFinishedClassification(GameItem bottle,int kind)
        {
            if(kind==6){bottle.SetGameItemType("ALCOHOL");bottle.RemoveGameItemType("SUBSTANCE");bottle.RemoveGameItemType("LUXURY_ITEM");return;}
            if(Rules.IsFruitWine(kind) && WineHelper.GetWineQualityTier(bottle)>0)
            {
                bottle.SetGameItemType("SUBSTANCE");
                bottle.SetGameItemType("LUXURY_ITEM");
                bottle.RemoveGameItemType("ALCOHOL");
            }
        }
        public static void AttachBottle(GameItem bottle)
        {
            WineTransferProtection.Attach(bottle);
            WineStorage.Expand(bottle);
            var nativeSprites=bottle.getItemSpriteModifierForItemFunc;
            bottle.getItemSpriteModifierForItemFunc=self=>{
                int kind=self.GetTagReadonly(Rules.WineTag).GetInt();
                if(kind>0 && kind<Rules.WineKeys.Length && WineHelper.IsFinishedWine(self) && !self.IsTag(WineHelper.WINE_COUNTERFEIT_TAG))
                    return new List<ItemSpriteModifier>(); // Labels are part of the five custom bottle sprites.
                return nativeSprites==null?new List<ItemSpriteModifier>():nativeSprites(self);
            };
            bottle.onLoaded+=self=>{
                WineTransferProtection.Attach(self);
                WineStorage.Expand(self);
                if(self.IsTag(BottleGeometry.CompactEmptyTag)) BottleGeometry.EnsureEmptyRoom(self,false);
                if(WineHelper.IsFinishedWine(self)) RefreshFinished(self,false);
                else if(self.GetTagReadonly(WineHelper.WINE_CONTAINER_STATE).GetInt()==1)
                {
                    int kind=self.GetTagReadonly(Rules.WineTag).GetInt();
                    if(kind>0 && kind<Rules.WineKeys.Length) self.name=ModEntry.L("fermenting_name",ModEntry.L("wine_"+Rules.WineKeys[kind]));
                }
                Barrel.RefreshQuality(self);
            };
        }
        public static void Tooltip(RichTextBuilder builder,GameItem item)
        {
            if(TooltipPolicy.HideModInfo(item)) return;
            if(BeverageStorage.CanChill(item)) builder.AddLine(ModEntry.L("beer_storage_hint"));
            else if(item.IsTag(Rules.WineTag) && Rules.IsFruitWine(item.GetTagReadonly(Rules.WineTag).GetInt())
                && WineHelper.IsFinishedWine(item) && !item.IsTag(WineHelper.WINE_COUNTERFEIT_TAG))
                builder.AddLine(ModEntry.L("fruit_wine_storage_hint"));
            if(item.IsTag(BottleGeometry.PendingBeerTag)) builder.AddLine(ModEntry.L("beer_space_hint"));
            if(item.IsTag(CropGeometry.PendingTag)) builder.AddLine(ModEntry.L("crop_space_hint"));
            if(item.IsTag(ProductGeometry.Pending)&&(item.identifier=="pelican_bait"||item.identifier=="pelican_premium_bait"))builder.AddLine(Expansion.L("bait_space_pending"));
            var seedCrop=Rules.BySeed(HydroponicHelper.GetPlantId(item));
            if(seedCrop!=null)
            {
                AddCareRequirements(builder,seedCrop,item);
                if(HydroponicHelper.IsGrowingSeed(item))
                {
                    builder.AddLine(ModEntry.L("care_current",CropCare.Score(item)));
                    if(Progress.Ready)
                    {
                        var machine=HydroponicHelper.GetAssociatedHydroponic(item);
                        if(machine!=null) CropCare.AddWaterDetails(builder,item,MachineHydroponic.GetWaterSlotItem(machine));
                    }
                }
            }
            int cropIndex=Crops.Index(item);
            if(cropIndex>=0 && cropIndex<Rules.Crops.Length)
            {
                var crop=Rules.Crops[cropIndex];
                int size=Rules.Size(crop,item.GetTagReadonly(Rules.SizeTag).GetInt());
                builder.AddLine(ModEntry.L("produce_footprint",ModEntry.L("size_"+size),Rules.CropWidth(crop,size)*Rules.CropHeight(crop,size)));
                if(item.IsTag(Rules.CareTag)) builder.AddLine(ModEntry.L("care_harvested",item.GetTagReadonly(Rules.CareTag).GetInt()));
                if(crop.WineKind>0)
                {
                    builder.AddLine(ModEntry.L("ingredient_hint",ModEntry.L("wine_"+Rules.WineKeys[crop.WineKind])));
                    int raw=item.GetTagReadonly(WineHelper.BERRY_CONDITION_INT).GetInt();
                    int score=Rules.IngredientScore(raw);
                    builder.AddLine(ModEntry.L("ingredient_score",ModEntry.L("condition_"+(score<0?"damaged":score==0?"poor":score==1?"good":"excellent")),Signed(score)));
                    if(crop.Sized) builder.AddLine(ModEntry.L("ingredient_size_score",Signed(Rules.SizeScore(crop,size))));
                }
            }
            if(item.IsTag(WineHelper.WINE_CONTAINER_TAG) && item.GetTagReadonly(WineHelper.WINE_CONTAINER_STATE).GetInt()==0)
            {
                var plan=Plan(item);
                if(plan.HasCustom)
                {
                    if(plan.Valid)
                    {
                        builder.AddLine(ModEntry.L("brew_preview",ModEntry.L("wine_"+Rules.WineKeys[plan.Kind]),Rules.Days(Rules.WineWork[plan.Kind],plan.YeastPower),ModEntry.L("tier_"+Rules.WineTier(plan.Score))));
                        int water=WineHelper.GetWaterQualityScore(item),yeast=plan.YeastCount>0?1:0;
                        builder.AddLine(ModEntry.L("brew_score_breakdown",Signed(plan.ConditionScore),plan.SizeScore,yeast,Signed(water),plan.Score));
                    }
                    else builder.AddLine(ModEntry.L("recipe_hint"));
                }
            }
            if(item.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG))
            {
                var seed=MachineHydroponic.GetInputSlot(item)?.childItem;
                var crop=seed==null?null:Rules.BySeed(HydroponicHelper.GetPlantId(seed));
                if(crop!=null)
                {
                    if(HydroponicHelper.IsGrowingSeed(seed)) builder.AddLine(ModEntry.L("care_machine",CropCare.Score(seed),ModuleHelper.GetAjustedQuality(item)));
                    AddCareRequirements(builder,crop,seed);
                    CropCare.AddWaterDetails(builder,seed,MachineHydroponic.GetWaterSlotItem(item));
                    if(crop.Sized) builder.AddLine(ModEntry.L("giant_progress",GiantHarvest.Misses(item,crop),Rules.GiantPityMisses));
                }
                int lastGiant=item.GetTagReadonly(Rules.LastGiantTag).GetInt()-1;
                if(lastGiant>=0 && lastGiant<Rules.Crops.Length) builder.AddLine(ModEntry.L("giant_last_harvest",ModEntry.L("crop_"+Rules.Crops[lastGiant].Key)));
                if(seed!=null && HydroponicHelper.GetPlantId(seed)==Rules.Crops[Rules.AncientIndex].SeedId)
                {
                    builder.AddLine(ModEntry.L(seed.GetTagReadonly(Rules.BreedTag).GetBool()?"harvest_breeding_hint":"harvest_normal_hint"));
                    int count=HydroponicHelper.GetHarvestCount(seed);
                    int guaranteed=(int)Math.Floor(2*PlantGeneticsHelper.GetStatMultiplier(seed,"SEED_YIELD"));
                    builder.AddLine(ModEntry.L("breeding_preview",Rules.HalfFruit(count),guaranteed));
                }
            }
        }
        private static string Signed(int score) => score>0?"+"+score:score.ToString();
        private static void AddCareRequirements(RichTextBuilder builder,CropDefinition crop,GameItem seed)
        {
            int level=HydroponicHelper.GetNutrimentLevel(seed),range=HydroponicHelper.GetNutrimentRange(seed);
            string low=(Math.Max(0,level-range)/100.0).ToString("0.##",CultureInfo.InvariantCulture);
            string high=((level+range)/100.0).ToString("0.##",CultureInfo.InvariantCulture);
            builder.AddLine(ModEntry.L("care_water_range",low,high));
            if(crop.Sized) builder.AddLine(ModEntry.L("giant_requirements",Rules.PremiumCare,Rules.PremiumQuality,Rules.GiantChancePercent));
        }
    }
}
