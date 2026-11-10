using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PelicanCultivation
{
    [HarmonyPatch(typeof(GameItem),nameof(GameItem.SetSpriteAndShape))]
    public static class FarmSpritePatch
    {
        public static bool Prefix(GameItem __instance,string spriteAtlasPath,string spritePath,ref GameItem __result)
        {
            if(!ModEntry.Enabled || spriteAtlasPath!=ModEntry.ModId || !FarmGeometry.Shape(spritePath,out int w,out int h)) return true;
            Crops.BindSprite(__instance,spritePath,w,h);__result=__instance;return false;
        }
    }
    [HarmonyPatch(typeof(AquacultureHelper),"GetSpriteShape")]
    public static class FarmShapePatch
    {
        public static bool Prefix(string atlas,string sprite,bool flipped,int orientation,ref GridShape __result)
        {
            if(!ModEntry.Enabled || atlas!=ModEntry.ModId || !FarmGeometry.Shape(sprite,out _,out _)) return true;
            __result=new GridShapeBuilder(FarmGeometry.Build(sprite).Build()).SetTransform(0,0,flipped,orientation).Build();return false;
        }
    }
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.TryChangeSprite))]
    public static class FarmAnimalSpritePatch
    {
        public static bool Prefix(GameItem gameItem,ref bool __result)
        {
            if(!ModEntry.Enabled || !FarmRules.Mammal(gameItem.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString())) return true;
            __result=FarmGeometry.Apply(gameItem,FarmSpecies.AnimalSprite(gameItem));return false;
        }
    }
    [HarmonyPatch(typeof(AnimalSpecies),nameof(AnimalSpecies.GetDisplayName))]
    public static class FarmAnimalNamePatch
    {
        public static bool Prefix(AnimalSpecies __instance,ref string __result)
        {
            if(!ModEntry.Enabled || !FarmRules.Mammal(__instance.speciesID)) return true;
            __result=ModEntry.L(__instance.speciesID==FarmRules.Cow?"farm_cow":"farm_pig");return false;
        }
    }
    [HarmonyPatch(typeof(FishSpecies),nameof(FishSpecies.GetDisplayName))]
    public static class FarmFishNamePatch
    {
        public static bool Prefix(FishSpecies __instance,ref string __result)
        {
            if(!ModEntry.Enabled || !FarmRules.Aquatic(__instance.speciesID)) return true;
            __result=ModEntry.L(__instance.speciesID==FarmRules.Sea?"farm_sea_cucumber":"farm_lobster");return false;
        }
    }
    [HarmonyPatch(typeof(FishSpecies),nameof(FishSpecies.GetRoeName))]
    public static class FarmRoeNamePatch
    {
        public static bool Prefix(FishSpecies __instance,ref string __result)
        {if(!ModEntry.Enabled || !FarmRules.Aquatic(__instance.speciesID)) return true;__result=ModEntry.L(__instance.speciesID==FarmRules.Sea?"farm_sea_cucumber_roe":"farm_lobster_roe");return false;}
    }
    [HarmonyPatch(typeof(FishSpecies),nameof(FishSpecies.GetFryName))]
    public static class FarmFryNamePatch
    {
        public static bool Prefix(FishSpecies __instance,ref string __result)
        {if(!ModEntry.Enabled || !FarmRules.Aquatic(__instance.speciesID)) return true;__result=ModEntry.L(__instance.speciesID==FarmRules.Sea?"farm_sea_cucumber_fry":"farm_lobster_fry");return false;}
    }
    [HarmonyPatch(typeof(AnimalCage),nameof(AnimalCage.CreateAnimalCage))]
    public static class FarmCagePatch
    { public static void Postfix(GameItem __result){if(ModEntry.Enabled && __result!=null) FarmHusbandry.AttachCage(__result);} }
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.HandleDisease))]
    public static class FarmImmunityTreatmentPatch
    { public static void Prefix(GameItem item){FarmTreatment.TryTreatAtNight(item);} }
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.CheckImmunityDisease))]
    public static class FarmImmunityPreventionPatch
    { public static void Prefix(GameItem item){FarmTreatment.TryTreatAtNight(item);} }
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.OnAnimalItemDayEnd))]
    public static class FarmProductionPatch
    {
        public static void Postfix(GameItem item,GameInventory inventory)
        {if(ModEntry.Enabled){FarmSpecies.RefreshAnimal(item);FarmHusbandry.Produce(item,inventory);}}
    }
    [HarmonyPatch(typeof(AquacultureHelper),nameof(AquacultureHelper.UpdateNameAndSprite))]
    public static class FarmFishRefreshPatch
    {public static void Postfix(GameItem item){if(ModEntry.Enabled) FarmSpecies.RefreshFish(item);} }
    [HarmonyPatch(typeof(AquacultureHelper),nameof(AquacultureHelper.TickTank))]
    public static class FarmSeaNightPatch
    {public static void Postfix(GameItem machine,GameGridInventory tankGrid,GameItem waterTank,bool isPowered){if(ModEntry.Enabled) FarmHusbandry.SeaNight(machine,tankGrid,waterTank,isPowered);} }
    [HarmonyPatch(typeof(AquacultureHelper),"HatchRoe")]
    public static class FarmHatchSafetyPatch
    {
        // Let observer prefixes (including Neon's BeforeCohort) capture their
        // per-call state before the safe replacement skips the native body.
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(GameItem roe,FishSpecies species,GameGridInventory tankGrid)
        {if(!ModEntry.Enabled) return true;FarmFishSafety.Convert(roe,species,tankGrid,true);return false;}
    }
    [HarmonyPatch(typeof(AquacultureHelper),"GrowFry")]
    public static class FarmFrySafetyPatch
    {
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(GameItem fry,FishSpecies species,GameGridInventory tankGrid,Dictionary<GameItem,float> gainedGrams,out GameItem[] __state)
        {
            __state=null;if(!ModEntry.Enabled) return true;
            SeaFounderPair.RecoverPurchasedBatch(fry,species);
            bool pair=SeaFounderPair.Guarantees(fry,species,Math.Max(1,fry.GetTagReadonly("FISH_GROUP_COUNT_TAG").GetInt()),false);
            var before=pair?new HashSet<GameItem>(tankGrid.childItems):null;
            FarmFishSafety.Convert(fry,species,tankGrid,false,gainedGrams);
            if(before!=null)
            {
                var born=new List<GameItem>();
                foreach(var child in tankGrid.childItems)
                    if(!before.Contains(child) && AquacultureHelper.IsFish(child) && child.GetTagReadonly("FISH_SPECIES_TAG").GetString()==FarmRules.Sea) born.Add(child);
                __state=born.ToArray();
            }
            return false;
        }
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(GameItem[] __state)
        {
            // Neon applies its inherited sexes in its own postfix. Retain the
            // already purchased first-pair guarantee after that processing,
            // while leaving its DNA, traits and later natural clutches intact.
            if(!ModEntry.Enabled || __state==null) return;
            for(int i=0;i<__state.Length;i++)
                if(__state[i]!=null && __state[i].unitCount>0) SeaFounderPair.PrepareIndividual(__state[i],i,true);
        }
    }
    [HarmonyPatch(typeof(AquacultureHelper),nameof(AquacultureHelper.GetMeatKcal))]
    public static class FarmSeaCaloriesPatch
    {
        public static bool Prefix(GameItem item,ref int __result)
        {
            if(!ModEntry.Enabled || item.GetTagReadonly("FISH_SPECIES_TAG").GetString()!=FarmRules.Sea) return true;
            __result=SeaCalories(item);return false;
        }
        public static int SeaCalories(GameItem item) => AquacultureHelper.IsDead(item)?Math.Max(0,item.GetTagReadonly("FISH_CARCASS_KCAL_TAG").GetInt()):FarmRules.SeaCalories(item.GetTagReadonly(FarmRules.FedNights).GetInt());
    }
    public static class FarmButchery
    {
        public static List<GameItem> MammalCuts(GameItem animal)
        {
            string id=animal.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();
            int kcal=Math.Max(0,(int)(animal.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()*0.9f));
            if(animal.IsTag("ANIMAL_DISEASED_TAG")) kcal=kcal*3/4;
            var result=Cuts(id,kcal,animal);
            if(id==FarmRules.Pig && animal.GetTagReadonly("ANIMAL_AGE_TAG").GetInt()>=FarmSettings.PigElder) result.Add(DirectoryMaster.Item("pelican_truffle"));
            return OwnCuts(result,animal);
        }
        public static List<GameItem> FishCuts(GameItem fish)
        {
            string id=fish.GetTagReadonly("FISH_SPECIES_TAG").GetString();
            if(id==FarmRules.Sea)
            {
                int remaining=FarmSeaCaloriesPatch.SeaCalories(fish);
                return OwnCuts(remaining<=0?new List<GameItem>():new List<GameItem>{FarmSpecies.SeaFood(fish.GetTagReadonly(FarmRules.FedNights).GetInt(),(int)AquacultureHelper.GetStage(fish),remaining)},fish);
            }
            int kcal=AquacultureHelper.IsDead(fish)?fish.GetTagReadonly("FISH_CARCASS_KCAL_TAG").GetInt():AquacultureHelper.GetMeatKcal(fish);
            return OwnCuts(Cuts(id,kcal,fish),fish);
        }
        public static List<GameItem> OwnCuts(List<GameItem> cuts,GameItem source)
        {
            // Directory factories return unowned catalog items. Unlike native
            // spawners, custom cuts must explicitly inherit the livestock owner.
            if(cuts!=null) foreach(var cut in cuts) GeneralHelper.SetItemOwned(cut,GeneralHelper.IsItemOwned(source));
            return cuts;
        }
        private static List<GameItem> Cuts(string species,int kcal,GameItem source)
        {
            var result=new List<GameItem>();
            foreach(int amount in FarmRules.SplitCalories(species,kcal))
            {
                var cut=FarmSpecies.Meat(species,amount);
                if(source.IsTag("ANIMAL_PRION_TAG")) cut.EnableTag("ANIMAL_PRION_TAG");
                if(source.IsTag("ANIMAL_DISEASED_TAG")) cut.EnableTag("ANIMAL_DISEASED_TAG");
                if(AquacultureHelper.IsPelletTainted(source)) GutterflowPelletHelper.ApplyTrashGrade(cut);
                result.Add(cut);
            }
            return result;
        }
    }
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.CreateButcherCuts))]
    public static class FarmMammalCutsPatch
    {
        public static bool Prefix(GameItem gameItem,ref List<GameItem> __result)
        {if(!ModEntry.Enabled || !FarmRules.Mammal(gameItem.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString())) return true;__result=FarmButchery.MammalCuts(gameItem);return false;}
    }
    [HarmonyPatch(typeof(AquacultureHelper),nameof(AquacultureHelper.CreateFilletPieces))]
    public static class FarmFishCutsPatch
    {
        public static bool Prefix(GameItem fish,ref List<GameItem> __result)
        {if(!ModEntry.Enabled || !FarmRules.Aquatic(fish.GetTagReadonly("FISH_SPECIES_TAG").GetString())) return true;__result=FarmButchery.FishCuts(fish);return false;}
        public static void Postfix(GameItem fish,List<GameItem> __result)
        {
            string id=fish.GetTagReadonly("FISH_SPECIES_TAG").GetString();
            if(ModEntry.Enabled && (FarmRules.Aquatic(id) || ExpansionStock.NewFish(id))) FarmButchery.OwnCuts(__result,fish);
        }
    }
    [HarmonyPatch(typeof(MachineAutoButcher),"IsFishMeat")]
    public static class FarmPremiumOutputPatch
    {
        public static bool Prefix(GameItem piece,ref bool __result)
        {if(!ModEntry.Enabled || (piece.identifier!="pelican_sea_cucumber_food" && !piece.identifier.StartsWith("pelican_lobster_meat_"))) return true;__result=false;return false;}
    }
    [HarmonyPatch(typeof(AquacultureItems),nameof(AquacultureItems.GetMeatKcal))]
    public static class FarmCutCaloriesLookupPatch
    {
        public static int DefaultCalories(string identifier)
        {
            return FarmPresentation.TryMeat(identifier,out _,out int amount)?amount:0;
        }
        public static bool Prefix(string identifier,ref int __result)
        {int amount=DefaultCalories(identifier);if(!ModEntry.Enabled || amount==0) return true;__result=amount;return false;}
    }
    [HarmonyPatch(typeof(AquacultureItems),nameof(AquacultureItems.GetMeatValue))]
    public static class FarmCutValueLookupPatch
    {
        public static bool Prefix(string identifier,ref int __result)
        {
            int amount=FarmCutCaloriesLookupPatch.DefaultCalories(identifier);if(!ModEntry.Enabled || amount==0) return true;
            string species=identifier.StartsWith("pelican_beef_")?FarmRules.Cow:identifier.StartsWith("pelican_pork_")?FarmRules.Pig:FarmRules.Lobster;
            __result=(int)FarmRules.MeatPrice(species,amount);return false;
        }
    }
    [HarmonyPatch(typeof(AquacultureHelper),"DescribeYield")]
    public static class FarmYieldTextPatch
    {
        public static bool Prefix(FishSpecies species,int kcal,ref string __result)
        {
            if(!ModEntry.Enabled || species==null || !FarmRules.Aquatic(species.speciesID)) return true;
            __result=species.speciesID==FarmRules.Sea?ModEntry.L("farm_sea_yield",kcal):ModEntry.L("farm_lobster_yield",FarmRules.SplitCalories(FarmRules.Lobster,kcal).Count,kcal);return false;
        }
    }
    [HarmonyPatch(typeof(AquacultureHelper),"AddRaiseOutlookLine")]
    public static class FarmOutlookPatch
    {
        // The native forecast assumes fixed fillet yields. The added species'
        // localized hints instead describe their actual harvest forms.
        public static bool Prefix(FishSpecies species) => !ModEntry.Enabled || species==null || !FarmRules.Aquatic(species.speciesID);
    }
}
