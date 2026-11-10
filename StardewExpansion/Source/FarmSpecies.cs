using System;
using System.Collections.Generic;
using UnityEngine;

namespace PelicanCultivation
{
    public static class FarmSpecies
    {
        public static AnimalSpecies Cattle,Pigs;
        public static FishSpecies Lobsters,SeaCucumbers;
        public static void Register()
        {
            if(Cattle==null)
            {
                Cattle=Animal(FarmRules.Cow,"cow",true);
                Pigs=Animal(FarmRules.Pig,"pig",false);
                Lobsters=Fish(FarmRules.Lobster,"lobster",false);
                SeaCucumbers=Fish(FarmRules.Sea,"sea_cucumber",true);
            }
            Cattle.growthStages=FarmRules.Stages(FarmSettings.CowAdult,FarmSettings.CowElder);
            Pigs.growthStages=FarmRules.Stages(FarmSettings.PigAdult,FarmSettings.PigElder);
            AnimalSpecies.All[FarmRules.Cow]=Cattle;AnimalSpecies.All[FarmRules.Pig]=Pigs;
            FishSpecies.All[FarmRules.Lobster]=Lobsters;FishSpecies.All[FarmRules.Sea]=SeaCucumbers;
        }
        private static AnimalSpecies Animal(string id,string prefix,bool cow)
        {
            var r=AnimalSpecies.Rat;
            var species=new AnimalSpecies {speciesID=id,displayNameKey="farm_"+prefix,spritePrefix=prefix,
                weightStages=cow?new[]{100,250,650,1000,2200,1800}:new[]{50,150,550,900,1600,1300},
                geneticTraitIDs=new List<string>(r.geneticTraitIDs),geneticBaseValues=new Dictionary<string,float>(r.geneticBaseValues),
                baseStats=new Dictionary<string,float>(r.baseStats),spriteByWeight=new Dictionary<int,string>{{900,"l"},{400,"m"},{0,"s"}},
                immunityThreshold=r.immunityThreshold,immunityRegenThreshold=r.immunityRegenThreshold};
            species.geneticBaseValues["ANIMAL_HUNGER_RATE_TAG"]=cow?110:150;
            species.geneticBaseValues["ANIMAL_LITTER_SIZE_TAG"]=cow?1:2;
            species.baseStats["ANIMAL_MAX_HUNGER_TAG"]=cow?1200:1000;
            species.baseStats["ANIMAL_HUNGER_TAG"]=cow?1000:850;
            species.baseStats["ANIMAL_GESTATION_PERIOD_TAG"]=cow?6:5;
            species.baseStats["ANIMAL_LITTER_SIZE_VARIANCE_TAG"]=0;
            return species;
        }
        private static FishSpecies Fish(string id,string key,bool sea)
        {
            return new FishSpecies {speciesID=id,displayName=key,flavorText="",fishIdentifier="pelican_"+key,
                roeIdentifier="pelican_"+key+"_roe",fryIdentifier="pelican_"+key+"_fry",groupCount=sea?2:1,
                stageStartDays=sea?new[]{0,1,2,4,8,60}:new[]{0,2,4,8,14,30},
                stageWeightGrams=sea?new[]{0,0,40,150,350,900}:new[]{0,0,100,400,850,1300},
                stageWaterNeedMl=sea?new[]{80,80,100,200,400,600}:new[]{100,100,150,400,800,1200},
                oldAgeDays=sea?3650:45,maintenanceKcalPer100gPerNight=sea?1.0f:2.4f,
                growthKcalPerGram=sea?2.0f:2.4f,conversionEfficiency=sea?0.85f:0.9f,
                maxHunger=sea?650:1000,maxHealth=100,wasteMlPer100gPerNight=sea?0.05f:0.5f,
                diet=sea?FishDiet.Omnivore:FishDiet.Carnivore,meatGrowthBoost=0,
                ignoresWaste=sea,pelletProof=sea,wasteSensitivity=sea?0.5f:1,
                remediationMlPer100gPerNight=0,breedChancePercent=sea?8:0,breedHungerRatio=0.65f,
                spriteAtlas=ModEntry.ModId,stageSprites=new[]{key+"_roe",key+"_fry",key+"_small",key+"_medium",key+"_large",key+"_large"},
                fertilizedRoeSprite=key+"_roe",meatItems=new[]{("pelican_lobster_meat_large",900),("pelican_lobster_meat_medium",400),("pelican_lobster_meat_small",200),("pelican_lobster_meat_tiny",100)}};
        }
        public static void RegisterItems(ModItemDirectory directory)
        {
            Register();
            directory.Add("pelican_cow",()=>CreateAnimal(true));directory.Add("pelican_pig",()=>CreateAnimal(false));
            foreach(string key in new[]{"lobster","sea_cucumber"})
            {
                string current=key;
                directory.Add("pelican_"+key,()=>CreateFish(current,0));
                directory.Add("pelican_"+key+"_roe",()=>CreateFish(current,1));
                directory.Add("pelican_"+key+"_fry",()=>CreateFish(current,2));
            }
            directory.Add("pelican_milk",()=>Product("milk",350,42,"BREVAGE"));
            directory.Add("pelican_truffle_small",()=>Product("truffle_small",125,FarmRules.TrufflePrice(true),"LUXURY_ITEM"));
            directory.Add("pelican_truffle",()=>Product("truffle",300,FarmRules.TrufflePrice(false),"LUXURY_ITEM"));
            directory.Add("pelican_sea_cucumber_food",()=>SeaFood(0,2));
            foreach(string species in new[]{FarmRules.Cow,FarmRules.Pig,FarmRules.Lobster})
                foreach(int kcal in FarmRules.CutCalories(species))
                {
                    string current=species;int amount=kcal;
                    directory.Add(MeatId(current,kcal),()=>Meat(current,amount));
                }
        }
        public static GameItem CreateAnimal(bool cow)
        {
            var item=ItemDirectory.CreateEmptyItem(cow?"pelican_cow":"pelican_pig");
            HusbandryHelper.InitAnimalItem(item,cow?Cattle:Pigs);
            item.SetGameItemType("FARMING_SUPPLIES");
            item.mayStackItemFunc=(a,b)=>false;
            item.onLoaded+=RefreshAnimal;
            RefreshAnimal(item);return item;
        }
        public static GameItem SpawnYoung(bool cow,string sex)
        {
            var item=DirectoryMaster.Item(cow?"pelican_cow":"pelican_pig");
            SetString(item,"ANIMAL_SEXE_TAG",sex);Crops.SetInt(item,"ANIMAL_AGE_TAG",1);
            SetFloat(item,"ANIMAL_WEIGHT_TAG",cow?250:150);RefreshAnimal(item);
            item.unitValue=cow?120:180;return item;
        }
        public static void RefreshAnimal(GameItem item)
        {
            string id=item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();if(!FarmRules.Mammal(id)) return;
            AnimalWeightGuard.Repair(item);
            bool cow=id==FarmRules.Cow;
            item.name=ModEntry.L(cow?"farm_cow":"farm_pig");
            item.SetGameItemType("FARMING_SUPPLIES");
            FarmPresentation.Text(item,cow?"cow":"pig");
            string key=AnimalSprite(item);
            FarmGeometry.Apply(item,key);
            // Native nightly animal handling zeros unitValue, so restore this
            // species' live-sale value after that handling, never cumulatively.
            int weight=Math.Max(0,(int)item.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat());
            item.unitValue=Math.Max(cow?120:180,FarmRules.MeatPrice(id,weight));
            if(HusbandryHelper.IsAnimalDead(item)) item.unitValue=FarmRules.MeatPrice(id,weight);
        }
        public static string AnimalSprite(GameItem item)
        {
            bool cow=item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()==FarmRules.Cow;
            int age=item.GetTagReadonly("ANIMAL_AGE_TAG").GetInt();int adult=cow?FarmSettings.CowAdult:FarmSettings.PigAdult;
            return (cow?"cow_":"pig_")+(age<2?"baby":age<adult?"young":item.GetTagReadonly("ANIMAL_SEXE_TAG").GetString()=="MALE"?"male":"female");
        }
        private static GameItem CreateFish(string key,int kind)
        {
            FishSpecies species=key=="lobster"?Lobsters:SeaCucumbers;
            var item=ItemDirectory.CreateEmptyItem("pelican_"+key+(kind==1?"_roe":kind==2?"_fry":""));
            item.mayStackItemFunc=(a,b)=>false;
            FarmGeometry.Apply(item,key+(kind==1?"_roe":kind==2?"_fry":"_small"));
            if(kind==1) AquacultureHelper.InitRoeItem(item,species);
            else if(kind==2) AquacultureHelper.InitFryItem(item,species);
            else AquacultureHelper.InitFishItem(item,species);
            item.unitValue=key=="lobster"?150:45;
            item.SetGameItemType("FARMING_SUPPLIES");
            item.onLoaded+=RefreshFish;RefreshFish(item);return item;
        }
        public static void RefreshFish(GameItem item)
        {
            var species=AquacultureHelper.GetSpecies(item);if(species==null || !FarmRules.Aquatic(species.speciesID)) return;
            string key=species.speciesID==FarmRules.Lobster?"lobster":"sea_cucumber";
            string stageKey=key+(item.IsTag("FISH_ROE_TAG")?"_roe":item.IsTag("FISH_FRY_TAG")?"_fry":"");
            item.name=ModEntry.L("farm_"+stageKey);
            FarmPresentation.Text(item,stageKey);
            string sprite=item.IsTag("FISH_ROE_TAG")?key+"_roe":item.IsTag("FISH_FRY_TAG")?key+"_fry":species.stageSprites[(int)AquacultureHelper.GetStage(item)];
            FarmGeometry.Apply(item,sprite);
        }
        public static GameItem Product(string key,int kcal,int price,string category)
        {
            var item=ItemDirectory.CreateEmptyItem("pelican_"+key);
            FarmGeometry.Apply(item,key);item.name=ModEntry.L("farm_"+key);
            FarmPresentation.Text(item,key);item.unitValue=price;
            item.SetGameItemType("FOOD");if(!string.IsNullOrEmpty(category)) item.SetGameItemType(category);
            FoodItemHelper.InitFoodItem(item,kcal,false);AudioHelper.InitProduce(item);
            item.mayStackItemFunc=(a,b)=>false;
            item.onLoaded+=FarmPresentation.RefreshFood;
            return item;
        }
        public static string MeatId(string species,int kcal)
        {
            string key=species==FarmRules.Cow?"beef":species==FarmRules.Pig?"pork":"lobster_meat";
            return "pelican_"+key+(kcal>=900?"_large":kcal>=400?"_medium":kcal>=200 && species==FarmRules.Lobster?"_small":"_tiny");
        }
        public static GameItem Meat(string species,int kcal)
        {
            string key=MeatId(species,kcal).Substring(8);
            var item=ItemDirectory.CreateEmptyItem("pelican_"+key);
            FarmGeometry.Apply(item,key);
            FoodItemHelper.InitFoodItem(item,kcal,false,true);SetString(item,"MEAT_TYPE_TAG",species);
            AudioHelper.InitMeaty(item);item.mayStackItemFunc=(a,b)=>false;
            FarmPresentation.RefreshFood(item);
            item.onLoaded+=FarmPresentation.RefreshFood;
            return item;
        }
        public static GameItem SeaFood(int nights,int stage,int calories=-1)
        {
            calories=calories<0?FarmRules.SeaCalories(nights):Math.Max(0,calories);
            var item=Product("sea_cucumber_food",calories,1,null);
            Crops.SetInt(item,FarmRules.FedNights,nights);Crops.SetInt(item,"CALORIE_VALUE_TAG",calories);
            Crops.SetInt(item,"PELICAN_SEA_HARVEST_STAGE",stage);
            Crops.SetInt(item,"PELICAN_SEA_HARVEST_KCAL",calories);
            item.EnableTag("MEAT_FOOD_TAG");SetString(item,"MEAT_TYPE_TAG",FarmRules.Sea);
            FarmGeometry.Apply(item,stage>=4?"sea_cucumber_large":stage>=3?"sea_cucumber_medium":"sea_cucumber_small");
            item.unitValue=Math.Max(1,calories/5);
            item.onLoaded+=self=>{int n=self.GetTagReadonly(FarmRules.FedNights).GetInt(),savedStage=self.GetTagReadonly("PELICAN_SEA_HARVEST_STAGE").GetInt();self.name=ModEntry.L("farm_sea_cucumber_food");Crops.SetInt(self,"CALORIE_VALUE_TAG",self.IsTag("PELICAN_SEA_HARVEST_KCAL")?self.GetTagReadonly("PELICAN_SEA_HARVEST_KCAL").GetInt():FarmRules.SeaCalories(n));FarmGeometry.Apply(self,savedStage>=4?"sea_cucumber_large":savedStage>=3?"sea_cucumber_medium":"sea_cucumber_small");};
            return item;
        }
        public static void SetString(GameItem item,string tag,string value) => item.ModifyTag(tag,s=>s.Enable().SetString(value));
        public static void SetFloat(GameItem item,string tag,float value) => item.ModifyTag(tag,s=>s.Enable().SetFloat(value));
    }

    public static class FarmGeometry
    {
        public static bool Shape(string key,out int width,out int height)
        {
            if(Expansion.Shape(key,out width,out height))return true;
            width=height=1;
            if(key=="cow_baby" || key=="pig_young" || key=="lobster_small" || key=="sea_cucumber_medium") {width=2;return true;}
            if(key=="cow_young" || key=="pig_female" || key=="lobster_medium" || key=="sea_cucumber_large" || key=="truffle") {width=height=2;return true;}
            if(key=="cow_male" || key=="cow_female" || key=="pig_male" || key=="lobster_large") {width=3;height=2;return true;}
            if(key=="milk") {height=2;return true;}
            if(key=="sea_cucumber_food") return true;
            if(key.StartsWith("beef_") || key.StartsWith("pork_") || key.StartsWith("lobster_meat_"))
            { if(key.EndsWith("_large")){width=3;height=2;}else if(key.EndsWith("_medium")){width=height=2;}else if(key.EndsWith("_small")){width=2;}return true; }
            return key=="pig_baby" || key=="truffle_small" || key=="sea_cucumber_small" || key=="lobster_roe" || key=="lobster_fry" || key=="sea_cucumber_roe" || key=="sea_cucumber_fry";
        }
        public static bool Apply(GameItem item,string key)
        {
            if(key=="sea_cucumber_food") key="sea_cucumber_small";
            if(!Shape(key,out int w,out int h)) return false;
            if(Assets.Sprite(key)==null) return false;
            bool fitted=CropGeometry.TryApplyShape(item,()=>Crops.BindSprite(item,key,w,h),PlaceWithin);
            item.SetEnableDisableTag("PELICAN_FARM_SPACE_PENDING_BOOL",!fitted);return fitted;
        }
        private static bool PlaceWithin(GameItem item)
        {
            var parent=item.parentInventory;
            if(parent==null || parent.CheckInventorySlotStillValid(item,item.modifiedShape,item.modifiedState)) return true;
            var slot=parent.TryFindOneValidInventorySlot(item);
            return slot!=null && slot.targetItem==null && slot.TryAcceptOnce()==1 && item.parentInventory==parent;
        }
        public static GridShapeBuilder Build(string key)
        {
            Shape(key,out int w,out int h);byte[] cells=new byte[w*h];for(int i=0;i<cells.Length;i++) cells[i]=1;
            return new GridShapeBuilder().SetData(cells,w);
        }
    }
}
