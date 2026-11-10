using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class ExpansionStock
    {
        public const string Chicken="PELICAN_CHICKEN",Goat="PELICAN_GOAT",Egg="PELICAN_CHICKEN_EGG",EggAge="PELICAN_EGG_AGE",Recovery="PELICAN_POSTPARTUM_UNTIL";
        // Retain the old saved tag name for compatibility; natural breeding
        // deliberately does not read it or create a management mode.
        public const string BreedMode="PELICAN_CAGE_BREED";
        public const string LastFertileEgg="PELICAN_LAST_FERTILE_EGG",PendingFertileEgg="PELICAN_FERTILE_EGG_PENDING";
        public static readonly string[] FishKeys={"midnight_carp","largemouth_bass","tuna","lava_eel"};
        public static readonly int[] EggCosts={80,100,160,160};
        // Native stages include a separate adolescent stage. Keep adulthood,
        // elderly age and mortality age at the approved distinct milestones.
        public static readonly int[][] FishStages={new[]{0,2,4,6,8,24},new[]{0,2,5,7,10,28},new[]{0,3,6,10,14,36},new[]{0,3,6,11,16,40}};
        public static readonly int[] FishOldAgeDays={36,40,50,54};
        public static readonly Dictionary<string,FishSpecies> Fish=new Dictionary<string,FishSpecies>();
        public static AnimalSpecies Chickens,Goats;
        public static bool NewAnimal(string id)=>id==Chicken||id==Goat;
        public static bool NewFish(string id)=>Fish.Values.Any(s=>s.speciesID==id);
        public static void Register()
        {
            if(Chickens==null){Chickens=Animal(Chicken,"chicken",4,18,60,new[]{15,50,150,300,1100,900});Goats=Animal(Goat,"goat",8,28,80,new[]{50,150,400,900,2100,1800});}
            AnimalSpecies.All[Chicken]=Chickens;AnimalSpecies.All[Goat]=Goats;
            for(int i=0;i<FishKeys.Length;i++)
            {
                string key=FishKeys[i];
                if(!Fish.TryGetValue(key,out var species))
                {
                    species=new FishSpecies{speciesID="PELICAN_"+key.ToUpperInvariant(),displayName=key,fishIdentifier="pelican_"+key,roeIdentifier="pelican_"+key+"_roe",fryIdentifier="pelican_"+key+"_fry",
                        groupCount=new[]{4,3,2,2}[i],stageStartDays=FishStages[i],oldAgeDays=FishOldAgeDays[i],
                        stageWeightGrams=i==0?new[]{0,0,60,300,900,1200}:i==1?new[]{0,0,90,600,1700,2200}:i==2?new[]{0,0,180,1400,4200,5500}:new[]{0,0,120,1000,3200,4200},
                        stageWaterNeedMl=new[]{70,100,180,500,1100,1500},maxHunger=i==0?700:i==1?1100:1800,maxHealth=100,
                        maintenanceKcalPer100gPerNight=2,growthKcalPerGram=2.4f,conversionEfficiency=0.9f,wasteMlPer100gPerNight=0.4f,
                        diet=i==0?FishDiet.Omnivore:FishDiet.Carnivore,breedChancePercent=new[]{12,10,8,6}[i],breedHungerRatio=0.65f,
                        meatItems=i==3?FishSpecies.Eel.meatItems:FishSpecies.Tilapia.meatItems,spriteAtlas=ModEntry.ModId,
                        stageSprites=new[]{key+"_roe",key+"_fry",key+"_small",key+"_medium",key+"_large",key+"_large"},fertilizedRoeSprite=key+"_roe"};
                    Fish.Add(key,species);
                }
                species.flavorText=Expansion.L(key+"_flavor");FishSpecies.All[species.speciesID]=species;
            }
        }
        private static AnimalSpecies Animal(string id,string prefix,int adult,int elder,int hunger,int[] weights)
        {
            var rat=AnimalSpecies.Rat;
            var s=new AnimalSpecies{speciesID=id,displayNameKey="v2_"+prefix,spritePrefix=prefix,growthStages=FarmRules.Stages(adult,elder),weightStages=weights,
                geneticTraitIDs=new List<string>(rat.geneticTraitIDs),geneticBaseValues=new Dictionary<string,float>(rat.geneticBaseValues),baseStats=new Dictionary<string,float>(rat.baseStats),
                spriteByWeight=new Dictionary<int,string>{{0,"s"}},immunityThreshold=rat.immunityThreshold,immunityRegenThreshold=rat.immunityRegenThreshold};
            s.geneticBaseValues["ANIMAL_HUNGER_RATE_TAG"]=hunger;s.geneticBaseValues["ANIMAL_LITTER_SIZE_TAG"]=1;
            s.baseStats["ANIMAL_MAX_HUNGER_TAG"]=id==Chicken?850:1200;s.baseStats["ANIMAL_HUNGER_TAG"]=id==Chicken?750:1000;
            s.baseStats["ANIMAL_GESTATION_PERIOD_TAG"]=6;s.baseStats["ANIMAL_LITTER_SIZE_VARIANCE_TAG"]=0;return s;
        }
        public static void RegisterItems(ModItemDirectory dir)
        {
            Register();dir.Add("pelican_chicken",()=>CreateAnimal(true));dir.Add("pelican_goat",()=>CreateAnimal(false));dir.Add("pelican_chicken_egg",CreateEgg);
            foreach(string key in FishKeys)for(int kind=0;kind<3;kind++){string k=key;int n=kind;dir.Add("pelican_"+key+(kind==1?"_roe":kind==2?"_fry":""),()=>CreateFish(k,n));}
            foreach(string king in Fishing.Kings){string key=king;dir.Add("pelican_"+king,()=>King(key));}
        }
        public static GameItem CreateAnimal(bool chicken)
        {
            var item=ItemDirectory.CreateEmptyItem(chicken?"pelican_chicken":"pelican_goat");
            HusbandryHelper.InitAnimalItem(item,chicken?Chickens:Goats);item.SetGameItemType("FARMING_SUPPLIES");item.mayStackItemFunc=(a,b)=>false;
            item.onLoaded+=Refresh;Refresh(item);return item;
        }
        public static string AnimalSprite(GameItem item)
        {
            bool chicken=item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()==Chicken;
            int age=item.GetTagReadonly("ANIMAL_AGE_TAG").GetInt();return (chicken?"chicken_":"goat_")+(age<1?"baby":age<(chicken?4:8)?"young":item.GetTagReadonly("ANIMAL_SEXE_TAG").GetString()=="MALE"?"male":"female");
        }
        public static GameItem SpawnYoung(string id,string sex)
        {
            var item=DirectoryMaster.Item(id);FarmSpecies.SetString(item,"ANIMAL_SEXE_TAG",sex);Crops.SetInt(item,"ANIMAL_AGE_TAG",1);
            FarmSpecies.SetFloat(item,"ANIMAL_WEIGHT_TAG",id=="pelican_chicken"?50:150);Refresh(item);return item;
        }
        public static GameItem CreateFish(string key,int kind)
        {
            var s=Fish[key];var item=ItemDirectory.CreateEmptyItem("pelican_"+key+(kind==1?"_roe":kind==2?"_fry":""));
            Crops.Sprite(item,key+(kind==1?"_roe":kind==2?"_fry":"_small"),kind==0?2:1,1);
            if(kind==1)AquacultureHelper.InitRoeItem(item,s);else if(kind==2)AquacultureHelper.InitFryItem(item,s);else AquacultureHelper.InitFishItem(item,s);
            item.mayStackItemFunc=(a,b)=>false;item.onLoaded+=Refresh;Refresh(item);return item;
        }
        public static GameItem King(string key)
        {
            var item=ItemDirectory.CreateEmptyItem("pelican_"+key);Crops.Sprite(item,key,4,3);item.name=Expansion.L(key);item.shortDescription=Expansion.L(key+"_desc");
            item.SetGameItemType("LUXURY_ITEM");item.unitValue=Fishing.KingPrices[Array.IndexOf(Fishing.Kings,key)];item.mayStackItemFunc=(a,b)=>false;item.onLoaded+=Expansion.Refresh;return item;
        }
        public static void Refresh(GameItem item)
        {
            string king=item.identifier!=null&&item.identifier.StartsWith("pelican_")?item.identifier.Substring(8):"";
            if(Array.IndexOf(Fishing.Kings,king)>=0){item.name=Expansion.L(king);item.shortDescription=Expansion.L(king+"_desc");item.flavorText="";ProductGeometry.Apply(item,king);return;}
            string species=item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();
            if(NewAnimal(species) && !item.IsTag(Egg))
            {
                AnimalWeightGuard.Repair(item);
                item.name=Expansion.L(species==Chicken?"chicken":"goat");item.shortDescription=Expansion.L(species==Chicken?"chicken_desc":"goat_desc");
                FarmGeometry.Apply(item,AnimalSprite(item));int weight=Math.Max(0,(int)item.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat());
                item.unitValue=Math.Max(species==Chicken?55:100,(long)Math.Round(weight*(species==Chicken?95.0:145.0)/1000));
            }
            var fish=AquacultureHelper.GetSpecies(item);
            if(fish!=null && NewFish(fish.speciesID))
            {
                string key=Fish.First(p=>p.Value==fish).Key;
                string stage=key+(item.IsTag("FISH_ROE_TAG")?"_roe":item.IsTag("FISH_FRY_TAG")?"_fry":"");
                item.name=Expansion.L(stage);item.shortDescription=Expansion.L(stage+"_desc");
                fish.flavorText=Expansion.L(key+"_flavor");item.flavorText=fish.flavorText;
                FarmGeometry.Apply(item,item.IsTag("FISH_ROE_TAG")?key+"_roe":item.IsTag("FISH_FRY_TAG")?key+"_fry":fish.stageSprites[(int)AquacultureHelper.GetStage(item)]);
            }
        }
        public static void AttachCage(GameItem cage,MachineryPanelRow row)
        {
            // The removed management panel contained only a button, not a
            // GraphNodeStorage. Keep native and product inventories in their
            // existing order so old cage contents retain their save paths.
            var native=cage.onCycleEndSlotItemFunc;
            cage.onCycleEndSlotItemFunc=(self,inventory,slot)=>{native?.Invoke(self,inventory,slot);if(ModEntry.Enabled)Hatch(self);};
        }
        public static bool HealthyAdult(GameItem item,int adult,int elder)
        {
            int age=item.GetTagReadonly("ANIMAL_AGE_TAG").GetInt();return age>=adult&&age<elder&&!HusbandryHelper.IsAnimalDead(item)&&!FarmRules.Sick(item)
                && item.GetTagReadonly("ANIMAL_HEALTH_TAG").GetFloat()>0&&item.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()>=HusbandryHelper.GetMaxHunger(item)*0.5f;
        }
        public static int FertileChance(GameItem mother,GameItem father)
        {
            Func<GameItem,float> health=i=>Math.Max(0,Math.Min(1,i.GetTagReadonly("ANIMAL_HEALTH_TAG").GetFloat()/Math.Max(1,GeneticHelper.GetCurrentStat(i,"ANIMAL_MAX_HEALTH_TAG"))));
            float fertility=Math.Min(GeneticHelper.GetCurrentStat(mother,"ANIMAL_FERTILITY_TAG"),GeneticHelper.GetCurrentStat(father,"ANIMAL_FERTILITY_TAG"));
            return (int)Math.Max(0,Math.Min(100,fertility*Math.Min(health(mother),health(father))));
        }
        public static void Produce(GameItem mother,GameInventory inventory)
        {
            if(!Progress.Ready)return;string species=mother.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();if(!NewAnimal(species))return;
            Refresh(mother);bool chicken=species==Chicken;
            if(mother.GetTagReadonly("ANIMAL_SEXE_TAG").GetString()!="FEMALE"||!HealthyAdult(mother,chicken?4:8,chicken?18:28))return;
            int today=Progress.Day;if(mother.GetTagReadonly("PELICAN_V2_PRODUCE_NIGHT").GetInt()==today)return;Crops.SetInt(mother,"PELICAN_V2_PRODUCE_NIGHT",today);
            var host=FarmHusbandry.Host(inventory);if(host==null||!GeneralHelper.IsItemOwned(host))return;
            if(chicken){ProduceChicken(mother,inventory,host,today);return;}
            const int interval=3,cost=400;int clock=Math.Min(interval,mother.GetTagReadonly(FarmRules.ProductClock).GetInt()+1);Crops.SetInt(mother,FarmRules.ProductClock,clock);if(clock<interval)return;
            float hunger=mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat();if(hunger-cost<HusbandryHelper.GetMaxHunger(mother)*0.25f)return;
            var product=DirectoryMaster.Item("pelican_goat_milk");GeneralHelper.SetItemOwned(product,true);GraphNodeStorage target=FarmHusbandry.Products(host);
            if(target==null || !GraphUtils.CanAccept(target,product) || GraphUtils.TryAcceptAll(target,product)!=0 || product.parentInventory!=target)
            {if(product.parentInventory==null)product.Destroy();FarmNightReport.RecordFullProducts(host);return;}
            Crops.SetInt(mother,FarmRules.ProductClock,0);FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",hunger-cost);
        }
        private static void ProduceChicken(GameItem mother,GameInventory inventory,GameItem host,int today)
        {
            const int cost=150;float hunger=mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat();float reserve=HusbandryHelper.GetMaxHunger(mother)*0.25f;
            Crops.SetInt(mother,FarmRules.ProductClock,1);
            if(hunger-cost>=reserve)
            {
                var egg=DirectoryMaster.Item("pelican_egg");
                if(TryPlaceProduct(egg,FarmHusbandry.Products(host)))
                {FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",hunger-cost);Crops.SetInt(mother,FarmRules.ProductClock,0);}
                else FarmNightReport.RecordFullProducts(host);
            }
            // Fertile eggs are a separate natural product, not a replacement
            // for the edible egg or dependent on the product tray's capacity.
            if(mother.IsTag("ANIMAL_NEUTERED_TAG")||!HealthyAdult(mother,4,18)||mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()-cost<reserve)return;
            if(mother.IsTag(LastFertileEgg)&&today-mother.GetTagReadonly(LastFertileEgg).GetInt()<3)return;
            var father=inventory.childItems.FirstOrDefault(a=>a!=mother&&!a.IsTag("ANIMAL_NEUTERED_TAG")&&GeneralHelper.IsItemOwned(a)&&a.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()==Chicken&&a.GetTagReadonly("ANIMAL_SEXE_TAG").GetString()=="MALE"&&HealthyAdult(a,4,18));
            if(father==null)return;
            // Preserve a successful conception while the cage is full. The
            // saved marker does not charge food or start cooldown until laid.
            if(!mother.IsTag(PendingFertileEgg))
            {
                if(!RNG.Roll(FertileChance(mother,father)))return;
                mother.ModifyTag(PendingFertileEgg,s=>s.Enable().SetBool(true));
            }
            var fertile=FertilizedEgg(mother,father);
            if(!TryPlaceProduct(fertile,inventory))return;
            FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()-cost);
            Crops.SetInt(mother,LastFertileEgg,today);mother.DisableTag(PendingFertileEgg);
        }
        private static bool TryPlaceProduct(GameItem product,GraphNodeStorage target)
        {
            GeneralHelper.SetItemOwned(product,true);
            if(target!=null&&GraphUtils.CanAccept(target,product)&&GraphUtils.TryAcceptAll(target,product)==0&&product.parentInventory==target)return true;
            if(product.parentInventory==null)product.Destroy();return false;
        }
        public static GameItem CreateEgg()
        {
            var item=ItemDirectory.CreateEmptyItem("pelican_chicken_egg");Crops.Sprite(item,"chicken_egg",1,1);item.name=Expansion.L("fertile_egg");item.shortDescription=Expansion.L("fertile_egg_desc");
            item.EnableTag(Egg);item.EnableTag("ALLOWED_IN_CAGE_TAG");item.SetGameItemType("FARMING_SUPPLIES");item.mayStackItemFunc=(a,b)=>false;return item;
        }
        public static void CopyState(GameItem source,GameItem target)
        {target.state=new TagSystem();foreach(var p in source.state.dict)target.state.dict[p.Key]=p.Value.Clone();target.modifiedState=target.state.Clone();}
        private static GameItem FertilizedEgg(GameItem mother,GameItem father)
        {
            var child=DirectoryMaster.Item("pelican_chicken");
            GeneticHelper.StorePartnerGenes(mother,father,Chickens);
            try{GeneticHelper.ApplyInheritedGenetics(child,mother,Chickens);}finally{GeneticHelper.ClearPartnerGenes(mother,Chickens);}
            var egg=DirectoryMaster.Item("pelican_chicken_egg");CopyState(child,egg);child.Destroy();
            egg.DisableTag("ANIMAL_ITEM_TAG");egg.DisableTag("CALORIE_VALUE_TAG");egg.EnableTag(Egg);egg.EnableTag("ALLOWED_IN_CAGE_TAG");
            Crops.SetInt(egg,EggAge,0);Crops.SetInt(egg,"PELICAN_EGG_BORN_NIGHT",Progress.Day);return egg;
        }
        public static void Hatch(GameItem cage)
        {
            if(!Progress.Ready)return;var grid=AnimalCage.GetCageInventory(cage);
            foreach(var egg in new List<GameItem>(grid.childItems).Where(e=>e.IsTag(Egg)))
            {
                int today=Progress.Day;if(egg.GetTagReadonly("PELICAN_EGG_BORN_NIGHT").GetInt()==today||egg.GetTagReadonly("PELICAN_EGG_LAST_NIGHT").GetInt()==today)continue;
                Crops.SetInt(egg,"PELICAN_EGG_LAST_NIGHT",today);int age=Math.Min(2,egg.GetTagReadonly(EggAge).GetInt()+1);Crops.SetInt(egg,EggAge,age);if(age<2)continue;
                var child=DirectoryMaster.Item("pelican_chicken");if(egg.IsTag("ANIMAL_SPECIES_TAG"))CopyState(egg,child);child.DisableTag(Egg);child.EnableTag("ANIMAL_ITEM_TAG");Crops.SetInt(child,"ANIMAL_AGE_TAG",0);GeneralHelper.SetItemOwned(child,true);
                Refresh(child);
                if(TryReplaceEgg(grid,egg,child))egg.Destroy();else if(child.parentInventory==null)child.Destroy();
            }
        }
        public static bool TryReplaceEgg(GameGridInventory grid,GameItem egg,GameItem child)
        {
            if(grid==null||egg==null||child==null||egg.parentInventory!=grid||child.parentInventory!=null||egg.unitCount!=1||child.unitCount!=1)return false;
            var oldShape=egg.modifiedShape;
            var placement=new GridShapeBuilder(child.modifiedShape).SetTransform(oldShape).Build();
            // Check the actual newborn geometry while ignoring only its egg.
            // No inventory is mutated when a larger child genuinely cannot fit.
            if(!grid.CheckShapePlacement(child,placement,new List<GameItem>{egg}))return false;
            bool placed=false;
            try
            {
                if(!grid.Expel(egg))return false;
                if(!grid.CheckInventorySlotStillValid(child,placement,child.modifiedState))return false;
                var slot=new SlotMarker{item=child,inventory=grid,itemGridShape=placement,numTransfer=1};
                placed=slot.TryAcceptOnce()==1&&child.parentInventory==grid;return placed;
            }
            finally
            {
                // The old egg is still the same protected object with its
                // inherited state and unique ID until replacement commits.
                if(!placed&&egg.parentInventory==null)
                {egg.modifiedShape=oldShape;grid.UncheckedAccept(egg);}
            }
        }
        public static List<GameItem> Cuts(GameItem animal)
        {
            bool chicken=animal.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()==Chicken;int remaining=Math.Max(0,(int)(animal.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()*0.9f));if(animal.IsTag("ANIMAL_DISEASED_TAG"))remaining=remaining*3/4;
            var result=new List<GameItem>();foreach(int tier in new[]{900,400,100})while(remaining>=tier){result.Add(Expansion.RawMeat(chicken?"chicken_meat":"goat_meat",tier));remaining-=tier;}
            if(remaining>0){var cut=Expansion.RawMeat(chicken?"chicken_meat":"goat_meat",100);Crops.SetInt(cut,"CALORIE_VALUE_TAG",remaining);Expansion.Refresh(cut);result.Add(cut);}
            foreach(var cut in result){if(animal.IsTag("ANIMAL_PRION_TAG"))cut.EnableTag("ANIMAL_PRION_TAG");if(animal.IsTag("ANIMAL_DISEASED_TAG"))cut.EnableTag("ANIMAL_DISEASED_TAG");}return FarmButchery.OwnCuts(result,animal);
        }
    }
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.TryChangeSprite))]
    public static class ExpansionAnimalSpritePatch
    {public static bool Prefix(GameItem gameItem,ref bool __result){if(!ModEntry.Enabled||!ExpansionStock.NewAnimal(gameItem.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()))return true;__result=FarmGeometry.Apply(gameItem,ExpansionStock.AnimalSprite(gameItem));return false;}}
    [HarmonyPatch(typeof(AnimalSpecies),nameof(AnimalSpecies.GetDisplayName))]
    public static class ExpansionAnimalNamePatch
    {public static bool Prefix(AnimalSpecies __instance,ref string __result){if(!ModEntry.Enabled||!ExpansionStock.NewAnimal(__instance.speciesID))return true;__result=Expansion.L(__instance.speciesID==ExpansionStock.Chicken?"chicken":"goat");return false;}}
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.OnAnimalItemDayEnd))]
    public static class ExpansionProducePatch
    {public static void Postfix(GameItem item,GameInventory inventory){if(ModEntry.Enabled)ExpansionStock.Produce(item,inventory);}}
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.CanMate))]
    public static class ExpansionMatingPatch
    {public static bool Prefix(GameItem item,ref bool __result){if(!ModEntry.Enabled)return true;string id=item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();if(id==ExpansionStock.Chicken||(id==ExpansionStock.Goat&&(item.GetTagReadonly(ExpansionStock.Recovery).GetInt()>Progress.Day||item.GetTagReadonly("ANIMAL_AGE_TAG").GetInt()>=28))){__result=false;return false;}return true;}}
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.GiveBirth))]
    public static class ExpansionGoatRecoveryPatch
    {public static void Postfix(GameItem mother){if(ModEntry.Enabled&&mother.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()==ExpansionStock.Goat)Crops.SetInt(mother,ExpansionStock.Recovery,Progress.Day+3);}}
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.CreateButcherCuts))]
    public static class ExpansionCutsPatch
    {public static bool Prefix(GameItem gameItem,ref List<GameItem> __result){if(!ModEntry.Enabled||!ExpansionStock.NewAnimal(gameItem.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()))return true;__result=ExpansionStock.Cuts(gameItem);return false;}}
    [HarmonyPatch(typeof(AquacultureHelper),nameof(AquacultureHelper.UpdateNameAndSprite))]
    public static class ExpansionFishRefreshPatch
    {public static void Postfix(GameItem item){if(ModEntry.Enabled)ExpansionStock.Refresh(item);}}
    [HarmonyPatch(typeof(FishSpecies),nameof(FishSpecies.GetDisplayName))]
    public static class ExpansionFishNamePatch
    {public static bool Prefix(FishSpecies __instance,ref string __result){if(!ModEntry.Enabled||!ExpansionStock.NewFish(__instance.speciesID))return true;__result=Expansion.L(ExpansionStock.Fish.First(p=>p.Value==__instance).Key);return false;}}
    [HarmonyPatch(typeof(FishSpecies),nameof(FishSpecies.GetRoeName))]
    public static class ExpansionRoeNamePatch
    {public static bool Prefix(FishSpecies __instance,ref string __result){if(!ModEntry.Enabled||!ExpansionStock.NewFish(__instance.speciesID))return true;__result=Expansion.L(ExpansionStock.Fish.First(p=>p.Value==__instance).Key+"_roe");return false;}}
    [HarmonyPatch(typeof(FishSpecies),nameof(FishSpecies.GetFryName))]
    public static class ExpansionFryNamePatch
    {public static bool Prefix(FishSpecies __instance,ref string __result){if(!ModEntry.Enabled||!ExpansionStock.NewFish(__instance.speciesID))return true;__result=Expansion.L(ExpansionStock.Fish.First(p=>p.Value==__instance).Key+"_fry");return false;}}
    [HarmonyPatch(typeof(AquacultureHelper),"IsFedAdult")]
    public static class ExpansionFishBreedingAgePatch
    {public static bool Prefix(GameItem stock,FishSpecies species,ref bool __result){if(!ModEntry.Enabled||!ExpansionStock.NewFish(species.speciesID))return true;int i=Array.FindIndex(ExpansionStock.FishKeys,k=>ExpansionStock.Fish[k]==species);if(ExpansionFishBreedingCostPatch.NativeBreeding||AquacultureHelper.GetStage(stock)>=FishStage.Elder||AquacultureHelper.GetHealth(stock)<=0||AquacultureHelper.GetHunger(stock)-ExpansionStock.EggCosts[i]<species.maxHunger*0.25f){__result=false;return false;}return true;}}
    [HarmonyPatch(typeof(AquacultureHelper),"TryBreed")]
    public static class ExpansionFishBreedingCostPatch
    {
        [ThreadStatic] public static bool NativeBreeding;
        public static void Prefix(){NativeBreeding=ModEntry.Enabled;}
        public static void Postfix(GameGridInventory tankGrid)
        {
            NativeBreeding=false;if(!ModEntry.Enabled)return;
            foreach(string key in ExpansionStock.FishKeys)
            {
                var species=ExpansionStock.Fish[key];int cost=ExpansionStock.EggCosts[Array.IndexOf(ExpansionStock.FishKeys,key)];
                Func<GameItem,bool> eligible=i=>AquacultureHelper.GetSpecies(i)==species&&AquacultureHelper.IsFish(i)&&!AquacultureHelper.IsDead(i)&&AquacultureHelper.GetHealth(i)>0&&AquacultureHelper.GetStage(i)>=FishStage.Adult&&AquacultureHelper.GetStage(i)<FishStage.Elder&&AquacultureHelper.GetHunger(i)>=species.maxHunger*species.breedHungerRatio&&AquacultureHelper.GetHunger(i)-cost>=species.maxHunger*0.25f;
                if(!tankGrid.childItems.Any(i=>eligible(i)&&i.GetTagReadonly("FISH_SEX_TAG").GetString()=="MALE"))continue;
                foreach(var mother in tankGrid.childItems.Where(i=>eligible(i)&&i.GetTagReadonly("FISH_SEX_TAG").GetString()=="FEMALE").ToList())
                {
                    if(!RNG.Roll(species.breedChancePercent))continue;
                    var egg=DirectoryMaster.Item(species.roeIdentifier);GeneralHelper.SetItemOwned(egg,GeneralHelper.IsItemOwned(mother));
                    if(!GraphUtils.CanAccept(tankGrid,egg)||GraphUtils.TryAcceptAll(tankGrid,egg)!=0||egg.parentInventory!=tankGrid){if(egg.parentInventory==null)egg.Destroy();continue;}
                    FarmSpecies.SetFloat(mother,"FISH_HUNGER_TAG",AquacultureHelper.GetHunger(mother)-cost);Crops.SetInt(egg,"PELICAN_ROE_MOTHER",mother.GetUniqueID());
                }
            }
        }
        public static Exception Finalizer(Exception __exception){NativeBreeding=false;return __exception;}
    }
}
