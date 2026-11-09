using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using PelicanCultivation;

public static class PelicanVerification
{
    private static int checks;
    private static void Check(bool condition,string name)
    { if(!condition) throw new Exception("FAIL: "+name); checks++; }
    private sealed class NativeItem : GameItem
    {
        public NativeItem(string id) : base(id) { modifiedState=state; }
        public override void Validate() { modifiedState=state; }
        public override void Destroy() { unitCount=0; parentInventory=null; }
        public void BindWindow(PixelWindow window) {contentWindow=window;}
    }
    private static NativeItem Ingredient(int crop,int raw=1,int size=1)
    {
        var item=new NativeItem("test_"+crop);
        Crops.SetInt(item,Rules.CropTag,crop+1);
        Crops.SetInt(item,WineHelper.BERRY_CONDITION_INT,raw);
        Crops.SetInt(item,Rules.SizeTag,size);
        item.EnableTag(WineHelper.WINE_INGREDIENT_TAG);
        return item;
    }
    private static NativeItem Yeast(int power,bool reusable=false)
    {
        var item=new NativeItem("yeast");
        WineHelper.InitYeast(item,power);
        if(reusable) item.EnableTag(WineHelper.WINE_YEAST_REUSABLE_TAG);
        return item;
    }
    private static Func<GameItem,GameInventory,bool> NativeHydroponicSeedPredicate()
    {
        // Invoke the actual input filter emitted from native SetupInputSlot,
        // without constructing Unity windows or replacing it with a copied rule.
        MethodInfo predicate=null;
        foreach(var nested in typeof(MachineHydroponic).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic))
            foreach(var method in nested.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static))
                if(method.Name.StartsWith("<SetupInputSlot>b__") && method.Name.EndsWith("_0") && method.ReturnType==typeof(bool))
                { if(predicate!=null) throw new Exception("Ambiguous native seed filter"); predicate=method; }
        if(predicate==null) throw new Exception("Native seed input filter not found; review the installed game build");
        object target=predicate.IsStatic?null:Activator.CreateInstance(predicate.DeclaringType,true);
        return (Func<GameItem,GameInventory,bool>)Delegate.CreateDelegate(typeof(Func<GameItem,GameInventory,bool>),target,predicate);
    }
    public static string Run()
    {
        checks=0;
        var storeField=typeof(PlayerStore).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        object beforeStore=storeField.GetValue(null);
        bool previousEnabled=ModEntry.Enabled;
        ModEntry.Enabled=true;
        ModEntry.GameplayReady=false;
        var pump=FormatterServices.GetUninitializedObject(typeof(RuntimePump));
        typeof(RuntimePump).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pump,null);
        Check(Object.ReferenceEquals(beforeStore,storeField.GetValue(null)),"main-menu update never initializes PlayerStore");
        Check(!Progress.Ready && Progress.Day==0 && Progress.Get("test",9)==9,"main-menu progress access returns safe defaults");
        AutoPlant.RefreshExistingMachines();
        AutoPlant.TryReplant(new NativeItem("hydroponic"));
        Check(Object.ReferenceEquals(beforeStore,storeField.GetValue(null)),"automatic replanting never initializes the store from the menu");
        Check(ModEntry.AutoReplantEnabled,"automatic replanting is enabled by default");
        var ownBottle=new NativeItem("wine_bottle");
        Crops.SetInt(ownBottle,Rules.WineTag,1);
        ownBottle.useSpriteFromMod=true;
        bool wasBeer;
        EmptyBottlePatch.Prefix(ownBottle,out wasBeer);
        Check(!ownBottle.useSpriteFromMod && !ownBottle.IsTag(Rules.WineTag),"emptying custom wine restores native sprite lookup");
        Check(wasBeer,"beer reset is tracked for safe empty-bottle placement");
        var otherBottle=new NativeItem("other_mod_bottle");otherBottle.useSpriteFromMod=true;
        EmptyBottlePatch.Prefix(otherBottle,out wasBeer);
        Check(otherBottle.useSpriteFromMod,"emptying unrelated bottles preserves their sprite lookup");
        ModEntry.Enabled=previousEnabled;
        foreach(var source in new[]{new[]{128f,160f},new[]{160f,384f},new[]{1024f,1024f}})
            foreach(var reference in new[]{new[]{48f,80f},new[]{64f,96f},new[]{128f,160f},new[]{288f,211f}})
            {
                float ppu=Rules.PortraitPixelsPerUnit(source[0],source[1],reference[0],reference[1],100);
                Check(source[0]/ppu<=reference[0]/100*0.8f+0.0001f,"portrait leaves a twenty-percent width margin");
                Check(source[1]/ppu<=reference[1]/100*0.8f+0.0001f,"portrait leaves a twenty-percent height margin");
            }
        Check(Math.Abs(Rules.TexturePixelsPerCell/(Rules.InventoryPixelsPerUnit/100)-16)<0.001,"high-resolution textures display at native cell size");
        var nativeSeedFilter=NativeHydroponicSeedPredicate();
        for(int kind=1;kind<Rules.WineKeys.Length;kind++)
        {
            Check(Rules.WineWidth(kind)==(kind<=2||kind==6?2:1) && Rules.WineHeight(kind)==(kind<=2||kind==6?3:4),"beer and spirit use six cells; fruit wine uses four cells");
            var finished=new NativeItem("wine_bottle");
            Crops.BindSprite(finished,"bottle_test",1,4);
            finished.modifiedShape=new GridShapeBuilder(finished.modifiedShape).SetTransform(5,3,false,0);
            Crops.BindSprite(finished,"bottle_test",Rules.WineWidth(kind),Rules.WineHeight(kind));
            Check(finished.modifiedShape.minX==5 && finished.modifiedShape.minY==3 && finished.modifiedShape.height==(kind<=2||kind==6?3:4),"bottle geometry preserves grid placement before capacity checks");
        }
        Check(Rules.PortraitDownwardFraction>0,"raising the portrait pivot lowers the rendered torso");
        foreach(int kind in new[]{3,4,5}) foreach(int score in new[]{1,2,3,5,6})
        {
            var fruitWine=new NativeItem("wine_bottle");
            Crops.SetInt(fruitWine,WineHelper.WINE_SCORE_INT,score);
            Crops.SetInt(fruitWine,"ALCOHOL_VALUE",7);
            fruitWine.SetGameItemType("SUBSTANCE");fruitWine.SetGameItemType("ALCOHOL");
            Brewing.ApplyFinishedClassification(fruitWine,kind);
            Check(fruitWine.IsGameItemType("SUBSTANCE") && fruitWine.IsGameItemType("LUXURY_ITEM"),"all three fruit wines are substance and luxury at every successful grade");
            Check(!fruitWine.IsGameItemType("ALCOHOL") && fruitWine.GetTagReadonly("ALCOHOL_VALUE").GetInt()==7,"display categories change without removing native alcohol amount");
        }
        Check(Rules.Crops.Length==12,"twelve species with the original eight stable indices");
        int[] expectedFirst={16,24,30,24,45,40,53,93,30,55,32,64};
        int[] expectedQuantity={3,8,8,6,7,6,4,6,4,6,4,4};
        int[] expectedWater={80,150,150,90,199,140,180,200,90,150,120,180};
        foreach(var crop in Rules.Crops)
        {
            PlantRegistry.Add(Crops.DefinePlant(crop,crop.Key));
            var seed=new NativeItem(crop.SeedId);
            HydroponicHelper.InitSeedItem(seed,crop.SeedId);
            GeneralHelper.SetItemOwned(seed,true);
            Check(!nativeSeedFilter(seed,null),"native filter reproduces the old missing-SEED bug "+crop.Key);
            Crops.EnsureSeedClassification(seed);
            Check(seed.IsGameItemType("SEED") && seed.IsTag("SEED") && seed.IsTag(HydroponicHelper.SEED_ITEM_TAG),"seed has both visible category and functional native tags "+crop.Key);
            Check(nativeSeedFilter(seed,null),"native hydroponic input accepts repaired owned seed "+crop.Key);
            Crops.BindSprite(seed,crop.Key+"_seed",1,1);
            Check(seed.modifiedShape.width==1 && seed.modifiedShape.height==1 && seed.useSpriteFromMod,"HD seed binding preserves one-cell geometry "+crop.Key);
            GeneralHelper.SetItemOwned(seed,false);
            Check(!nativeSeedFilter(seed,null),"unbought merchant seed cannot bypass ownership "+crop.Key);
            GeneralHelper.SetItemOwned(seed,true);seed.EnableTag("CONTAINER_TAG");
            Check(!nativeSeedFilter(seed,null),"container exclusion remains native "+crop.Key);
            seed.DisableTag("CONTAINER_TAG");
            Crops.SetInt(seed,HydroponicHelper.GROWING_PROGRESS_INT,11);
            Crops.SetInt(seed,PlantGeneticsHelper.GENETIC_HEAT_INT,19);
            seed.RemoveGameItemType("SEED"); // Old saved state overrides the factory.
            Crops.EnsureSeedClassification(seed);
            Check(nativeSeedFilter(seed,null) && seed.GetTagReadonly(HydroponicHelper.GROWING_PROGRESS_INT).GetInt()==11 && PlantGeneticsHelper.GetHeat(seed)==19,"old-save classification repair preserves progress and heat "+crop.Key);
            int index=Rules.Index(crop);
            Check(HydroponicHelper.GetRequiredProgress(seed)==expectedFirst[index],"native first work "+crop.Key);
            Check(HydroponicHelper.GetBaseHarvestQuantity(seed)==expectedQuantity[index],"native harvest count "+crop.Key);
            Check(HydroponicHelper.GetWaterUsePerCycle(seed)==expectedWater[index],"native water "+crop.Key);
            Check(HydroponicHelper.GetPlantId(seed)==crop.SeedId,"native plant id "+crop.Key);
            Check(PlantGeneticsHelper.GetTraitRank(seed,"edible")==0,"food produce no longer needs an innate edible trait "+crop.Key);
            Check(PlantRegistry.Get(crop.SeedId).returnsSeeds==(index==0 || index==7),"seed-return eligibility "+crop.Key);
            if(crop.Regrowth>0)
            {
                Crops.SetInt(seed,HydroponicHelper.PERENNIAL_ESTABLISHED_INT,1);
                Check(HydroponicHelper.GetRequiredProgress(seed)==new[]{0,0,15,0,15,20,0,27,15,20,0,0}[index],"native regrowth "+crop.Key);
            }
            if(index==1 || index==3 || index==6)
            {
                PlantGeneticsHelper.SetTraitRank(seed,"perennial",1);
                Check(!PlantRegistry.Get(crop.SeedId).returnsSeeds,"edited perennial does not enable seed return "+crop.Key);
                Check(HydroponicHelper.IsPerennial(seed),"edited perennial recognized "+crop.Key);
            }
        }
        VerifyCropCare();
        VerifyThreeSizesAndGiants();
        VerifySizeBrewing();
        VerifyBottleLayoutAndMigration();
        VerifyFinishedWineTransferProtection();
        VerifyBeverageStorage();
        var ancient=new NativeItem(Rules.Crops[7].SeedId);
        HydroponicHelper.InitSeedItem(ancient,Rules.Crops[7].SeedId);
        double mult=PlantGeneticsHelper.GetStatMultiplier(ancient,"SEED_YIELD");
        Check(Math.Abs(mult-0.5)<0.0001,"native perennial seed penalty");
        for(int i=0;i<100;i++) Check(Rules.BatchSeeds(mult,i/100.0)==1,"initial breeding reliably one seed");
        Check(Rules.HalfFruit(6)==3 && Rules.HalfFruit(7)==3 && Rules.HalfFruit(1)==0,"odd fruit rounds down");
        PlantGeneticsHelper.SetTraitRank(ancient,"slow_grower",0);
        PlantGeneticsHelper.SetTraitRank(ancient,"fertile",3);
        PlantGeneticsHelper.SetTraitRank(ancient,"bountiful",2);
        Crops.SetInt(ancient,PlantGeneticsHelper.GENETIC_HEAT_INT,20);
        var child=new NativeItem(Rules.Crops[7].SeedId);
        HydroponicHelper.InitSeedItem(child,Rules.Crops[7].SeedId);
        PlantGeneticsHelper.InheritTraits(ancient,child);
        Check(PlantGeneticsHelper.GetTraitRank(child,"slow_grower")==0,"deleted trait stays deleted");
        Check(PlantGeneticsHelper.GetTraitRank(child,"fertile")==3,"fertility rank inherited");
        Check(PlantGeneticsHelper.GetTraitRank(child,"bountiful")==2,"yield rank inherited");
        Check(PlantGeneticsHelper.GetHeat(child)==0,"editing heat not inherited");
        foreach(int count in new[]{0,1,3,6,11}) for(int health=0;health<=100;health++)
        {
            var split=PlantHarvest.SplitByHealth(count,health,false,false,0);
            Check(split.Total==count && split.huge==0,"native three-size split conserves crop count");
        }
        int[] cropKinds={3,4,5,6,7};
        int[] rawScores={-2,-1,1,2};
        foreach(int crop in cropKinds) foreach(int a in rawScores) foreach(int b in rawScores)
            foreach(int water in new[]{-3,-2,-1,0,1}) foreach(int power in new[]{0,2,4})
            {
                var first=Ingredient(crop,a);var second=Ingredient(crop,b);
                var items=new List<GameItem>{first,second};
                if(power>0) items.Add(Yeast(power));
                var plan=Brewing.Plan(items,water);
                var nativeBottle=new NativeItem("wine_bottle");
                Crops.SetInt(nativeBottle,WineHelper.WINE_SCORE_INT,Rules.IngredientScore(a)+Rules.IngredientScore(b)+1+(power>0?1:0)+water);
                Check(plan.Valid && plan.Kind==Rules.Crops[crop].WineKind,"five fixed recipe species");
                Check(Rules.WineTier(plan.Score)==WineHelper.GetWineQualityTier(nativeBottle),"native quality equivalence");
                Check(first.unitCount==1 && second.unitCount==1,"preview preserves ingredients");
            }
        Check(!Brewing.Plan(new List<GameItem>{Ingredient(3)}).Valid,"one crop rejected");
        Check(!Brewing.Plan(new List<GameItem>{Ingredient(3),Ingredient(3),Ingredient(3)}).Valid,"three crops rejected");
        Check(!Brewing.Plan(new List<GameItem>{Ingredient(3),Ingredient(4)}).Valid,"mixed species rejected");
        Check(!Brewing.Plan(new List<GameItem>{Ingredient(3),new NativeItem("wine_berry")}).Valid,"native berry mixed recipe rejected");
        Check(!Brewing.Plan(new List<GameItem>{Ingredient(3),Ingredient(3),Yeast(2),Yeast(4)}).Valid,"two yeasts rejected");
        Check(!Brewing.Plan(new List<GameItem>{Ingredient(0),Ingredient(0)}).Valid,"nonbrewing crop rejected");
        Check(Brewing.Plan(new List<GameItem>{Ingredient(3),Ingredient(3),Yeast(2,true)},1).Valid,"reusable yeast accepted");
        var stacked=Ingredient(3);stacked.unitCount=2;
        Check(!Brewing.Plan(new List<GameItem>{stacked,Ingredient(3)}).Valid,"unexpected stacked ingredient rejected safely");
        int[,] times={{4,2,1},{6,3,2},{6,3,2},{8,4,2},{10,5,3}};
        for(int kind=1;kind<=5;kind++) for(int p=0;p<3;p++) Check(Rules.Days(Rules.WineWork[kind],new[]{1,2,4}[p])==times[kind-1,p],"approved fermentation periods");
        Check(!Rules.EligibleForDiscovery(14) && Rules.EligibleForDiscovery(15),"day 15 inclusive discovery gate");
        Check(!Rules.EligibleForGift(2,false,false) && Rules.EligibleForGift(3,false,false),"third visit guarantee");
        Check(!Rules.EligibleForGift(3,true,false) && !Rules.EligibleForGift(3,false,true),"gift never repeats after acquisition or delivery");
        Check(!Rules.NewVisit(21,21) && Rules.NewVisit(28,21),"reloading same visit cannot increment count");
        VerifyMerchantSchedule();
        VerifyAutomaticReplanting();
        return "Passed "+checks+" checks: beer fridge acceptance and native Chilled +20% feature, fruit-wine-only aging, historical value preservation, finished-wine transfer protection, three crop sizes, wrapped-grid lookup, selective tooltips, giant guarantees, brewing and prior regressions.";
    }
    private static NativeItem SeedFor(PlantDefinition plant)
    {
        var seed=new NativeItem(plant.id);
        if(plant.id==PlantList.NUTRIFRUIT_SEED) seed.spritePath=PlantList.NUTRIFRUIT_SEED;
        HydroponicHelper.InitSeedItem(seed,plant.id);
        Crops.EnsureSeedClassification(seed);
        GeneralHelper.SetItemOwned(seed,true);
        return seed;
    }
    private static void VerifyMerchantSchedule()
    {
        Check(!FarmRules.VisitDay(-1,true,true,true) && !FarmRules.VisitDay(0,true,true,true),"merchant does not schedule a visit outside a running game");
        for(int day=1;day<=56;day++) foreach(bool nativeFarmer in new[]{false,true})
        {
            var manager=new StoreClientManager();
            var before=new StoreClient{identifier="before"};var after=new StoreClient{identifier="after"};
            manager.clientStack.Add(before);
            if(nativeFarmer) manager.clientStack.Add(new StoreClient{identifier="retired_farmer"});
            manager.clientStack.Add(after);
            var original=manager.clientStack.ToArray();int created=0;
            Func<StoreClient> create=()=>{created++;return new StoreClient{identifier=Merchant.Id};};
            bool expected=day%7==3 || day%7==5 || day%7==6;
            Merchant.ReconcileSchedule(manager,create,day);
            Check(created==(expected?1:0) && manager.clientStack.Count==original.Length+(expected?1:0),"eight-week schedule visits Wednesday, Friday and Saturday without changing native visitors");
            for(int index=0;index<original.Length;index++) Check(Object.ReferenceEquals(manager.clientStack[index],original[index]),"schedule preserves every native visitor and its order");
            Merchant.ReconcileSchedule(manager,create,day);
            Check(created==(expected?1:0),"repeated generation/load refresh cannot queue another visit");
            if(expected)
            {
                var queued=manager.clientStack[manager.clientStack.Count-1];
                manager.clientStack.Add(new StoreClient{identifier=Merchant.Id});
                Merchant.ReconcileSchedule(manager,create,day);
                Check(manager.clientStack.Count==original.Length+1 && Object.ReferenceEquals(queued,manager.clientStack[manager.clientStack.Count-1]),"saved duplicate visits collapse to the original queued visitor");
                Merchant.ReconcileSchedule(manager,create,day,isPresent:true);
                Check(manager.clientStack.Count==original.Length && created==1,"an ongoing trade removes queued duplicates without creating a replacement");
                Merchant.ReconcileSchedule(manager,create,day,lastVisitDay:day);
                Check(manager.clientStack.Count==original.Length && created==1,"a completed visit cannot be regenerated on the same day");
            }
            else
            {
                manager.clientStack.Insert(1,new StoreClient{identifier=Merchant.Id});
                Merchant.ReconcileSchedule(manager,create,day);
                Check(manager.clientStack.Count==original.Length && created==0,"legacy visits queued on Friday or another off-day are removed before arrival");
            }
        }
        var nullStack=new StoreClientManager();nullStack.clientStack=null;
        Merchant.ReconcileSchedule(null,null,3);Merchant.ReconcileSchedule(nullStack,null,6);
        Check(nullStack.clientStack==null,"schedule ignores unavailable client queues without constructing game state");
    }
    public static string RunScheduleIntegration()
    {
        int beforeChecks=checks;
        var stationField=typeof(StoreStation).GetField("instance",BindingFlags.NonPublic|BindingFlags.Static);
        object previous=stationField.GetValue(null);
        try
        {
            var station=(StoreStation)FormatterServices.GetUninitializedObject(typeof(StoreStation));
            stationField.SetValue(null,station);
            var calendar=(StoreCalendar)FormatterServices.GetUninitializedObject(typeof(StoreCalendar));
            int visits=0;
            for(int day=1;day<=28;day++)
            {
                station.dayCounter=day;
                string nativeWeekday=calendar.GetDayOfWeek();
                bool expected=nativeWeekday=="mech_calendar_wednesday" || nativeWeekday=="mech_calendar_friday" || nativeWeekday=="mech_calendar_saturday";
                Check(FarmRules.VisitDay(day,true,true,true)==expected,"schedule matches the installed game's actual calendar weekday");
                var manager=new StoreClientManager();
                Merchant.ReconcileSchedule(manager,()=>new StoreClient{identifier=Merchant.Id},day);
                Check(manager.clientStack.Count==(expected?1:0),"actual client queue follows the native Wednesday/Saturday calendar mapping");
                visits+=manager.clientStack.Count;
            }
            Check(visits==12,"four native calendar weeks contain exactly twelve scheduled visits");
        }
        finally {stationField.SetValue(null,previous);}
        return "Passed "+(checks-beforeChecks)+" merchant schedule integration checks against the installed native calendar (Wednesday/Friday/Saturday, three times per week).";
    }
    private static void VerifyCropCare()
    {
        foreach(var crop in Rules.Crops)
        {
            var seed=SeedFor(PlantRegistry.Get(crop.SeedId));
            Crops.SetInt(seed,HydroponicHelper.GROWING_PROGRESS_INT,7);
            Crops.SetInt(seed,PlantGeneticsHelper.GENETIC_HEAT_INT,19);
            PlantGeneticsHelper.SetTraitRank(seed,"edible",1); // Legacy v0.1.5 seed.
            PlantGeneticsHelper.SetTraitRank(seed,"bountiful",3);
            CropCare.RemoveRedundantTrait(seed);
            CropCare.RemoveRedundantTrait(seed);
            Check(PlantGeneticsHelper.GetTraitRank(seed,"edible")==0 && PlantGeneticsHelper.GetTraitRank(seed,"bountiful")==3,"legacy edible migration is selective and idempotent: "+crop.Key);
            Check(PlantGeneticsHelper.GetHeat(seed)==19 && seed.GetTagReadonly(HydroponicHelper.GROWING_PROGRESS_INT).GetInt()==7 && seed.IsGameItemType("SEED") && !seed.IsGameItemType("FOOD"),"legacy migration preserves seed progress, heat and classification: "+crop.Key);
            var produce=new NativeItem(crop.ProduceId);
            FoodItemHelper.InitFoodItem(produce,100,false);
            produce.EnableTag(InspectableHelper.INSPECTABLE_TAG);
            produce.unitValue=73;
            Crops.EnsureProduceClassification(produce);
            Check((Expansion.Flower(crop)?!produce.IsGameItemType("FOOD")&&!produce.IsTag("CALORIE_VALUE_TAG"):produce.IsGameItemType("FOOD")&&produce.IsTag("CALORIE_VALUE_TAG")) && !produce.IsTag(InspectableHelper.INSPECTABLE_TAG) && produce.unitValue==73,"produce keeps its food/flower classification without blank inspection: "+crop.Key);
        }
        var strawberry=SeedFor(PlantRegistry.Get(Rules.Crops[5].SeedId));
        Crops.SetInt(strawberry,HydroponicHelper.REQUIRED_PROGRESS_INT,20);
        Crops.SetInt(strawberry,HydroponicHelper.GROWING_PROGRESS_INT,0);
        int progress=88;
        CropCare.ClampGrowingProgress(strawberry,ref progress);
        Check(progress==20,"saved 88-work strawberry machine only charges its 20-work regrowth");
        Crops.SetInt(strawberry,HydroponicHelper.GROWING_PROGRESS_INT,17);
        progress=88;CropCare.ClampGrowingProgress(strawberry,ref progress);
        Check(progress==3,"only remaining work is charged at the end of a multi-cycle crop");
        Crops.SetInt(strawberry,HydroponicHelper.GROWING_PROGRESS_INT,20);
        progress=88;CropCare.ClampGrowingProgress(strawberry,ref progress);
        Check(progress==0,"finished crop cannot incur extra care loss");
        var native=SeedFor(PlantRegistry.Get("fillerweed_seed"));
        progress=88;CropCare.ClampGrowingProgress(native,ref progress);
        PlantGeneticsHelper.SetTraitRank(native,"edible",1);CropCare.RemoveRedundantTrait(native);
        Check(progress==88 && PlantGeneticsHelper.GetTraitRank(native,"edible")==1,"native progress and edible genetics are untouched");
        foreach(int work in new[]{16,20,24,27,40,53,93})
        {
            Check(Rules.CareScore(100,10000-100*work,work)==80,"equivalent full-cycle nutrient loss is comparable across crop periods");
            Check(Rules.CareScore(100,10000,work)==100,"well maintained crops keep full care score");
        }
        Check(Rules.CareScore(100,-18644,93)==38,"negative native raw health still yields a fair long-cycle care score");
        Check(Rules.CareScore(100,-1000000,93)==0 && Rules.CareScore(100,20000,20)==100,"care score is bounded");
        Check(Rules.CareScore(80,8000,20)==80,"weak starting health cannot bypass large-crop care requirements");
        foreach(int count in new[]{0,1,3,6,8,54}) foreach(int care in new[]{0,20,21,49,50,84,85,94,95,100}) foreach(int quality in new[]{0,19,20,49,50})
        {
            var split=Rules.SplitCrop(count,care,quality);
            Check(split.Total==count && split.small>=0 && split.normal>=0 && split.large==0 && split.huge==0,"ordinary split conserves produce and never creates the retired large size");
            Check(split.large==0,"large produce is never generated");
        }
        var standard=Rules.SplitCrop(6,60,0);
        Check(standard.normal==5 && standard.small==1 && standard.large==0,"ordinary six-crop harvest is five regular plus one small");
        var bulk=Rules.SplitCrop(54,60,0);
        Check(bulk.normal==52 && bulk.small==2 && bulk.large==0,"quantity modules retain mostly regular produce with at most two small crops");
        var large=Rules.SplitCrop(6,85,20);
        Check(large.normal==5 && large.small==1 && large.large==0,"former large threshold now produces normal crops");
        var premium=Rules.SplitCrop(6,95,50);
        Check(premium.large==0 && premium.normal==6 && premium.small==0,"premium care yields normal crops before the separate giant roll");
        int[] goodCounts=new int[4];
        int[] careBands={0,38,70,100};
        for(int band=0;band<careBands.Length;band++) for(int roll=0;roll<100;roll++)
        {
            int score=Rules.IngredientScore(Rules.RollCondition(careBands[band],roll));
            if(score>=1) goodCounts[band]++;
            Check(score>=-1 && score<=2,"condition scores remain compatible with native brewing");
        }
        Check(goodCounts[0]==15 && goodCounts[1]==85 && goodCounts[2]==100 && goodCounts[3]==100,"ordinary care mostly yields good ingredients while severe neglect retains consequences");
        foreach(int crop in new[]{3,4,5,6,7})
        {
            var plan=Brewing.Plan(new List<GameItem>{Ingredient(crop,1),Ingredient(crop,1),Yeast(2)},1);
            Check(plan.Score==5 && Rules.WineTier(plan.Score)==4,"two good ingredients, yeast and clean water produce reserve quality: "+crop);
        }
    }
    private static void VerifyThreeSizesAndGiants()
    {
        int variable=0,fixedCount=0;
        foreach(var crop in Rules.Crops)
        {
            if(crop.Sized) variable++;else fixedCount++;
            for(int size=0;size<4;size++)
            {
                int normalized=Rules.Size(crop,size);
                var item=new NativeItem(Rules.ProduceId(crop,size));
                Crops.BindSprite(item,"test_crop",Rules.CropWidth(crop,normalized),Rules.CropHeight(crop,normalized));
                Check(item.shape.width*item.shape.height==(crop.Sized?new[]{1,4,4,9}[size]:crop.Key=="wheat"||crop.Key=="poppy"||crop.Key=="fairy_rose"?4:2),"three sized footprints and requested fixed two/four-cell crops: "+crop.Key);
                Check(crop.Sized || (normalized==1 && Rules.SizeScore(crop,size)==0 && Rules.ProduceId(crop,size)==crop.ProduceId),"fixed crops have one normal product and no size brewing bonus");
            }
            if(!crop.Sized)
            {
                var fixedMachine=new NativeItem("hydroponic");
                var result=GiantHarvest.Resolve(fixedMachine,crop,8,100,100,0);
                Check(result.normal==8 && result.small==0 && result.large==0 && result.huge==0,"fixed species cannot become giant: "+crop.Key);
                continue;
            }
            var machine=new NativeItem("hydroponic");
            for(int attempt=1;attempt<=10;attempt++)
            {
                var result=GiantHarvest.Resolve(machine,crop,6,95,50,99);
                Check(result.huge==0 && result.Total==6 && GiantHarvest.Misses(machine,crop)==attempt,"qualified giant miss is saved once per batch: "+crop.Key);
            }
            GiantHarvest.Resolve(machine,crop,0,100,100,0);
            GiantHarvest.Resolve(machine,crop,6,94,100,0);
            GiantHarvest.Resolve(machine,crop,6,100,49,0);
            Check(GiantHarvest.Misses(machine,crop)==10,"empty or unqualified harvest neither spends nor resets pity");
            var guaranteed=GiantHarvest.Resolve(machine,crop,6,95,50,99);
            Check(guaranteed.huge==1 && guaranteed.Total==6 && guaranteed.large==0 && guaranteed.normal==5 && GiantHarvest.Misses(machine,crop)==0,"eleventh qualified attempt replaces one normal crop with a giant");
            int randomHits=0;
            for(int roll=0;roll<100;roll++)
            {
                var cleanMachine=new NativeItem("hydroponic");
                var result=GiantHarvest.Resolve(cleanMachine,crop,54,100,100,roll);
                if(result.huge==1) randomHits++;
                Check(result.huge<=1 && result.Total==54,"large module harvests never multiply giant count or total yield");
            }
            Check(randomHits==10,"ten percent is per batch, regardless of harvest quantity");
        }
        Check(variable==6 && fixedCount==6 && Rules.Crops[9].Sized && !Rules.Crops[0].Sized && !Rules.Crops[3].Sized && !Rules.Crops[4].Sized,"blueberry joins the original five sized crops; six crops remain fixed-size");
        var shared=new NativeItem("hydroponic");
        var potato=Rules.Crops[1];var strawberry=Rules.Crops[5];
        for(int i=0;i<10;i++) GiantHarvest.Resolve(shared,potato,8,100,50,99);
        GiantHarvest.Resolve(shared,strawberry,6,100,50,99);
        Check(GiantHarvest.Misses(shared,potato)==10 && GiantHarvest.Misses(shared,strawberry)==1,"pity is isolated by species on the same machine");
        var otherMachine=new NativeItem("hydroponic");
        Check(GiantHarvest.Misses(otherMachine,potato)==0,"another machine cannot consume this machine's guarantee");
        var saved=new NativeItem("hydroponic");
        foreach(var pair in shared.state.dict) saved.state.dict[pair.Key]=pair.Value.Clone();
        saved.modifiedState=saved.state;
        Check(GiantHarvest.Resolve(saved,potato,8,100,50,99).huge==1 && GiantHarvest.Misses(shared,potato)==10,"saved machine tags carry the guarantee independently of transient memory");
        Check(Rules.SizePrices[0]==0.5 && Rules.SizePrices[1]==1 && Rules.SizePrices[2]==1 && Rules.SizePrices[3]==3,"three sale multipliers with retired large mapped to normal");
        Check(CropCare.MineralState(49,150,100)==-1 && CropCare.MineralState(50,150,100)==0 && CropCare.MineralState(250,150,100)==0 && CropCare.MineralState(251,150,100)==1,"water advice respects native inclusive mineral boundaries");
        Check(!CropCare.HarmfulConcentration(10,1) && CropCare.HarmfulConcentration(11,1) && !CropCare.HarmfulConcentration(14,0.7),"pollution advice applies the native concentration threshold and strain sensitivity");
    }
    private static void VerifySizeBrewing()
    {
        foreach(int crop in new[]{3,4,5,6,7}) foreach(int a in new[]{0,1,2,3}) foreach(int b in new[]{0,1,2,3})
            foreach(var condition in new[]{new[]{-2,-2,-2},new[]{1,1,2},new[]{2,1,3}}) foreach(int water in new[]{-3,1}) foreach(bool yeast in new[]{false,true})
        {
            var items=new List<GameItem>{Ingredient(crop,condition[0],a),Ingredient(crop,condition[1],b)};
            if(yeast) items.Add(Yeast(2));
            var plan=Brewing.Plan(items,water);
            int sizeBonus=Rules.Crops[crop].Sized?(a==3?2:0)+(b==3?2:0):0;
            Check(plan.Valid && plan.ConditionScore==condition[2] && plan.SizeScore==sizeBonus,"preview separates raw condition and actual size scores");
            int nativeBase=condition[2]+1+(yeast?1:0)+water;
            Check(plan.Score==nativeBase+sizeBonus,"size changes score without changing native paired/yeast/water bonuses");
            var bottle=new NativeItem("wine_bottle");bottle.unitValue=197;
            Brewing.StampPlan(bottle,plan);Brewing.StampPlan(bottle,plan);
            Check(bottle.GetTagReadonly(WineHelper.WINE_SCORE_INT).GetInt()==sizeBonus && bottle.unitValue==197,"repeated start validation seeds size score once and never adds a sale multiplier");
            Crops.SetInt(bottle,WineHelper.WINE_SCORE_INT,bottle.GetTagReadonly(WineHelper.WINE_SCORE_INT).GetInt()+nativeBase);
            Check(WineHelper.GetWineQualityTier(bottle)==Rules.WineTier(plan.Score) && WineHelper.GetWineQualityTier(bottle)<=5,"native final score and preview agree, with no quality above the native top tier");
            Brewing.ClearPlan(bottle);
            Check(!bottle.IsTag(Rules.SizeScoreTag) && !bottle.IsTag(Rules.WineTag) && bottle.GetTagReadonly(WineHelper.WINE_SCORE_INT).GetInt()==0,"replacing custom ingredients with native berries clears a previous size bonus");
        }
        var mixed=Brewing.Plan(new List<GameItem>{Ingredient(5,1,2),Ingredient(5,1,3)},1);
        Check(mixed.Valid && mixed.SizeScore==2 && mixed.IngredientCount==2,"retired large acts as normal when paired with a giant");
        Check(!Brewing.Plan(new List<GameItem>{Ingredient(5,1,3),Ingredient(6,1,3)},1).Valid,"two giant crops of different species remain invalid");
        var nativeBottle=new NativeItem("wine_bottle");Crops.SetInt(nativeBottle,WineHelper.WINE_SCORE_INT,8);
        Brewing.ClearPlan(nativeBottle);
        Check(nativeBottle.GetTagReadonly(WineHelper.WINE_SCORE_INT).GetInt()==8,"native-only bottle scores are not reset");
    }
    private static void VerifyBottleLayoutAndMigration()
    {
        GridShape storage=new GridShapeBuilder().SetData(WineStorage.Shape,WineStorage.Width).SetDataOutside(1).Build();
        int free=0;
        for(int y=0;y<storage.height;y++) for(int x=0;x<storage.width;x++) if(storage.GetLocal(x,y)==0)free++;
        Check(storage.width==8 && storage.height==3 && free==22,"bottle grid has exactly 22 usable cells");
        for(int y=0;y<2;y++)for(int x=0;x<6;x++)Check(storage.GetLocal(x,y)==0,"all original twelve ingredient coordinates stay usable");
        GridShape first=new GridShapeBuilder(3,3).SetDataFill(1).SetPosition(0,0).Build();
        GridShape second=new GridShapeBuilder(3,3).SetDataFill(1).SetPosition(3,0).Build();
        GridShape yeast=new GridShapeBuilder(2,2).SetDataFill(1).SetPosition(6,0).Build();
        Check(first.FullyIntersects(storage,(value,cell)=>value!=1||cell==0) && second.FullyIntersects(storage,(value,cell)=>value!=1||cell==0) && yeast.FullyIntersects(storage,(value,cell)=>value!=1||cell==0),"two native nine-cell shapes and a four-cell yeast fit the expanded inventory");
        Check(!first.Intersects(second) && !first.Intersects(yeast) && !second.Intersects(yeast),"giant pair and yeast do not overlap");
        foreach(int oldSize in new[]{0,1,2})
        {
            var parsnip=new NativeItem("pelican_parsnip"+(oldSize==0?"_small":oldSize==2?"_large":""));
            Crops.BindSprite(parsnip,"legacy",oldSize==2?2:1,oldSize==0?1:2);
            Crops.SetInt(parsnip,Rules.SizeTag,oldSize);Crops.SetInt(parsnip,"CALORIE_VALUE_TAG",oldSize==0?50:oldSize==2?250:100);
            parsnip.unitValue=oldSize==0?4:oldSize==2?20:8;
            CropGeometry.PrepareLegacy(parsnip,Rules.Crops[0]);CropGeometry.PrepareLegacy(parsnip,Rules.Crops[0]);
            Check(parsnip.GetTagReadonly(Rules.SizeTag).GetInt()==1 && parsnip.unitValue==12 && parsnip.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==240,"all legacy parsnip IDs migrate once to normal price and calories");
        }
        var largeFruit=new NativeItem("pelican_starfruit_large");
        Crops.BindSprite(largeFruit,"legacy",2,2);Crops.SetInt(largeFruit,Rules.SizeTag,2);Crops.SetInt(largeFruit,"CALORIE_VALUE_TAG",250);largeFruit.unitValue=225;
        CropGeometry.PrepareLegacy(largeFruit,Rules.Crops[6]);CropGeometry.PrepareLegacy(largeFruit,Rules.Crops[6]);
        Check(largeFruit.unitValue==90 && largeFruit.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==240 && largeFruit.GetTagReadonly(Rules.SizeTag).GetInt()==1,"old large fruit migrates once to normal preserving its strain multiplier");
        largeFruit.modifiedShape=new GridShapeBuilder(largeFruit.modifiedShape).SetTransform(5,4,false,0);
        bool resized=CropGeometry.TryApplyShape(largeFruit,()=>Crops.BindSprite(largeFruit,"new_huge",3,3),item=>false);
        Check(!resized && largeFruit.shape.width==2 && largeFruit.shape.height==2 && largeFruit.modifiedShape.minX==5 && largeFruit.modifiedShape.minY==4 && largeFruit.unitValue==90,"full inventory rollback retains old cells, position and migrated value");
        resized=CropGeometry.TryApplyShape(largeFruit,()=>Crops.BindSprite(largeFruit,"new_huge",3,3),item=>true);
        Check(resized && largeFruit.shape.width==3 && largeFruit.shape.height==3,"free space permits the giant footprint");
        bool caught=false;
        try {CropGeometry.TryApplyShape(largeFruit,()=>{Crops.BindSprite(largeFruit,"new_small",1,1);throw new Exception("placement failure");},item=>true);}catch{caught=true;}
        Check(caught && largeFruit.shape.width==3 && largeFruit.shape.height==3,"failed sprite or placement callback restores the previous footprint");
        VerifyWrappedBottleGrid();
        bool enabledBefore=ModEntry.Enabled;ModEntry.Enabled=true;
        foreach(var crop in Rules.Crops)
        {
            var seed=new NativeItem(crop.SeedId);var produce=Ingredient(Rules.Index(crop));
            Check(!CropModInfoPatch.Prefix(seed)&&!CropModInfoPatch.Prefix(produce),"all crop seeds and produce hide late additional information");
        }
        Check(CropModInfoPatch.Prefix(new NativeItem("wine_bottle")) && !CropModInfoPatch.Prefix(new NativeItem("hydroponic")),"bottle instructions remain available while hydroponic appendices are hidden");
        var hydro=new NativeItem("peer_hydroponic");hydro.EnableTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG);
        Check(!CropModInfoPatch.Prefix(hydro) && CropModInfoPatch.Prefix(new NativeItem("water_purifier")),"hydroponic classification hides the appendix without hiding other machine hooks");
        ModEntry.Enabled=enabledBefore;
        foreach(int size in new[]{0,2,3})
        {
            var savedV17=Ingredient(6,1,size);Crops.BindSprite(savedV17,"saved_v17",size==0?1:size==3?3:2,size==0?2:3);
            Crops.SetInt(savedV17,Rules.CropFormatTag,17);Crops.SetInt(savedV17,"CALORIE_VALUE_TAG",size==0?50:size==2?200:300);
            savedV17.unitValue=size==0?45:size==2?180:270;
            CropGeometry.PrepareLegacy(savedV17,Rules.Crops[6]);CropGeometry.PrepareLegacy(savedV17,Rules.Crops[6]);
            Check(savedV17.unitValue==(size==0?45:size==2?90:270) && savedV17.GetTagReadonly(Rules.SizeTag).GetInt()==(size==2?1:size),"v0.1.7 small/large/giant migration preserves the intended values once");
        }
    }
    private static void VerifyWrappedBottleGrid()
    {
        // Use native container traversal through the actual intake wrapper,
        // without creating Unity controls. The former direct cast must fail.
        var grid=(GameGridInventory)FormatterServices.GetUninitializedObject(typeof(GameGridInventory));
        var intake=(MachineryIntake)FormatterServices.GetUninitializedObject(typeof(MachineryIntake));
        typeof(MachineryIntake).GetField("<content>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(intake,grid);
        var panel=(MachineryPanel)FormatterServices.GetUninitializedObject(typeof(MachineryPanel));
        typeof(MachineryPanel).GetField("<content>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,intake);
        panel.role="ingredients";
        var row=(MachineryPanelRow)FormatterServices.GetUninitializedObject(typeof(MachineryPanelRow));
        typeof(MachineryPanelRow).GetField("panels",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(row,new List<MachineryPanel>{panel});
        var window=(PixelWindow)FormatterServices.GetUninitializedObject(typeof(PixelWindow));
        typeof(PixelWindow).GetField("<childElement>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,row);
        var bottle=new NativeItem("wine_bottle");bottle.BindWindow(window);
        Check(!(row.GetPanel("ingredients").content is GameGridInventory),"native intake wrapper reproduces v0.1.7's failed direct cast");
        Check(Object.ReferenceEquals(WineStorage.IngredientGrid(bottle),grid),"native body-storage API reaches the actual wrapped bottle grid");
    }
    private static NativeItem TransferBottle(int kind,int state,int score)
    {
        var bottle=new NativeItem("wine_bottle");
        bottle.EnableTag(WineHelper.WINE_CONTAINER_TAG);
        Crops.SetInt(bottle,WineHelper.WINE_CONTAINER_STATE,state);
        Crops.SetInt(bottle,WineHelper.WINE_SCORE_INT,score);
        if(kind>0) Crops.SetInt(bottle,Rules.WineTag,kind);
        GeneralHelper.SetItemOwned(bottle,true);
        typeof(WineHelper).GetMethod("SetupBottleLiquidTransfer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{bottle});
        return bottle;
    }
    private static NativeItem TransferContainer(string id)
    {
        var target=new NativeItem(id);target.EnableTag("LIQUID_CONTAINER_TAG");
        Crops.SetInt(target,"LIQUID_CONTAINER_CAPACITY",3000000);
        Crops.SetInt(target,"CURRENT_PART_WATER",1500000);
        GeneralHelper.SetItemOwned(target,true);
        return target;
    }
    private static void VerifyFinishedWineTransferProtection()
    {
        bool beforeEnabled=ModEntry.Enabled;ModEntry.Enabled=true;
        try
        {
            foreach(int kind in new[]{1,2,3,4,5}) foreach(int score in new[]{0,1,2,3,5,6}) foreach(bool counterfeit in new[]{false,true})
                foreach(string id in new[]{"water_jug","water_bucket","water_tank","bottled_water","other_mod_liquid_container"})
            {
                var bottle=TransferBottle(kind,2,score);
                if(counterfeit) bottle.EnableTag(WineHelper.WINE_COUNTERFEIT_TAG);
                bottle.unitValue=Rules.WinePrices[kind];
                AgableHelper.InitAgable(bottle);
                for(int day=0;day<3;day++)AgableHelper.OnAge(bottle);
                long originalValue=bottle.unitValue;
                var target=TransferContainer(id);
                WineTransferProtection.Attach(bottle);
                Check(!bottle.mayThisTargetItemFunc(bottle,target) && !bottle.canThisTargetItemFunc(bottle,target),"all five custom finished wines reject liquid-container drag targets at every native tier");
                var may=bottle.mayThisTargetItemFunc;var can=bottle.canThisTargetItemFunc;var action=bottle.onThisTargetItemFunc;
                WineTransferProtection.Attach(bottle);
                Check(Object.ReferenceEquals(may,bottle.mayThisTargetItemFunc) && Object.ReferenceEquals(can,bottle.canThisTargetItemFunc) && Object.ReferenceEquals(action,bottle.onThisTargetItemFunc),"load/wake refresh does not accumulate interaction wrappers");
                // Invoke the guarded, actual native callback. If the guard fails,
                // native pouring/reset runs; the checks catch source or target loss.
                bottle.onThisTargetItemFunc(bottle,target);
                Check(WineHelper.IsFinishedWine(bottle) && bottle.GetTagReadonly(Rules.WineTag).GetInt()==kind && bottle.GetTagReadonly(WineHelper.WINE_SCORE_INT).GetInt()==score && bottle.unitCount==1,"rejected action keeps the wine bottle, kind and quality score intact");
                Check(bottle.unitValue==originalValue && AgableHelper.GetAge(bottle)==3,"rejected action keeps aged value and native aging metadata");
                Check(target.GetTagReadonly("CURRENT_PART_WATER").GetInt()==1500000 && !target.IsTag("CURRENT_PART_ALCOHOL") && !target.IsTag("CURRENT_PART_WINE_EXTRACT"),"rejected action leaves target water and composition intact");
                bool result=true;
                Check(!CustomWineTargetPatch.Prefix(bottle,target,ref result) && !result && !CustomWinePourPatch.Prefix(bottle,target) && !CustomWineLiquidTransferPatch.Prefix(bottle,target),"native targeting, direct wine pour and generic transfer entry points are protected");
            }
            var water=TransferContainer("water_jug");
            var empty=TransferBottle(0,0,0);empty.EnableTag("LIQUID_CONTAINER_TAG");
            var incomingMay=empty.mayItemTargetThisFunc;var incomingCan=empty.canItemTargetThisFunc;var incomingAction=empty.onItemTargetThisFunc;
            WineTransferProtection.Attach(empty);
            Check(empty.mayThisTargetItemFunc(empty,water) && empty.canThisTargetItemFunc(empty,water),"native empty-bottle liquid interaction remains available");
            Check(Object.ReferenceEquals(incomingMay,empty.mayItemTargetThisFunc) && Object.ReferenceEquals(incomingCan,empty.canItemTargetThisFunc) && Object.ReferenceEquals(incomingAction,empty.onItemTargetThisFunc),"incoming water and ration callbacks are not replaced");
            var fermenting=TransferBottle(5,1,5);WineTransferProtection.Attach(fermenting);
            Check(!WineTransferProtection.Blocks(fermenting,water) && !fermenting.canThisTargetItemFunc(fermenting,water),"fermenting bottles retain the native rejection rule");
            var finished=TransferBottle(5,2,6);WineTransferProtection.Attach(finished);
            var drain=new NativeItem("drain");drain.EnableTag("LIQUID_CONTAINER_TAG");
            Check(!WineTransferProtection.Blocks(finished,drain) && finished.canThisTargetItemFunc(finished,drain),"intentional native draining stays available");
            Check(!WineTransferProtection.Blocks(finished,new NativeItem("backpack")) && !WineTransferProtection.Blocks(null,water) && !WineTransferProtection.Blocks(finished,null),"ordinary storage targets and null inputs are not treated as liquid pours");
            var vanilla=TransferBottle(0,2,6);int forwarded=0;
            vanilla.mayThisTargetItemFunc=(s,t)=>true;vanilla.canThisTargetItemFunc=(s,t)=>true;vanilla.onThisTargetItemFunc=(s,t)=>forwarded++;
            WineTransferProtection.Attach(vanilla);
            vanilla.onThisTargetItemFunc(vanilla,water);
            Check(vanilla.canThisTargetItemFunc(vanilla,water) && forwarded==1 && CustomWinePourPatch.Prefix(vanilla,water),"native-only wine interactions are forwarded without custom-wine restrictions");
            var disabledTag=TransferBottle(5,2,6);disabledTag.DisableTag(Rules.WineTag);
            Check(!WineTransferProtection.Blocks(disabledTag,water),"disabled leftover kind values cannot misclassify a native bottle");
            Check(!WineTransferProtection.Blocks(TransferBottle(99,2,6),water),"unknown wine kinds from other mods are not blocked");
            int replacementCalls=0;
            finished.onThisTargetItemFunc=(s,t)=>replacementCalls++;
            WineTransferProtection.Attach(finished);finished.onThisTargetItemFunc(finished,water);
            Check(replacementCalls==0,"refresh also guards a replaced native or third-party callback");
            int drinkCalls=0;finished.onActivateSlotItemFunc=(s,inv,slot)=>drinkCalls++;
            var activation=finished.onActivateSlotItemFunc;
            WineTransferProtection.Attach(finished);
            Check(Object.ReferenceEquals(activation,finished.onActivateSlotItemFunc),"drink/empty activation is not changed by transfer protection");
            ModEntry.Enabled=false;
            Check(!WineTransferProtection.Blocks(finished,water) && CustomWinePourPatch.Prefix(finished,water),"disabled mod leaves native liquid behavior available");
        }
        finally {ModEntry.Enabled=beforeEnabled;}
    }
    private static bool FridgeAccepts(GameItem item)
    {
        bool result=false;
        if(BeerFridgeAcceptancePatch.Prefix(item,ref result)) result=MachineFridge.MayZoneAcceptItem(item);
        return result;
    }
    public static bool MechanicTextFixture(string key,ref string __result)
    { __result=key;return false; }
    public static string RunStorageIntegration()
    {
        int beforeChecks=checks;
        bool previousEnabled=ModEntry.Enabled;ModEntry.Enabled=true;
        try
        {
            var rackGrid=(GameGridInventory)FormatterServices.GetUninitializedObject(typeof(GameGridInventory));
            typeof(MachineWineRack).GetMethod("SetupWineSlot",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{rackGrid});
            foreach(int kind in new[]{1,2,3,4,5}) foreach(int score in new[]{0,1,2,3,5,6})
            {
                var bottle=TransferBottle(kind,2,score);bottle.uniqueId=1000+kind*10+score;AgableHelper.InitAgable(bottle);
                bottle.unitValue=81;Crops.SetInt(bottle,AgableHelper.AGE_INT,3);
                bool beer=kind<=2;
                Check(MachineFridge.MayZoneAcceptItem(bottle)==beer && MachineFridge.IsChillable(bottle)==beer,"actual Harmony-patched native fridge decisions route only the two beers");
                Check(MachineWineRack.CanAgeWine(bottle)==!beer && rackGrid.mayInventoryAddItemFunc(bottle,rackGrid)==!beer,"actual native ager inventory callback accepts fruit wines and rejects beer");
                Check(AgableHelper.IsAgable(bottle)==!beer,"actual native aging discovery excludes beer");
                WineHelper.OnAgeWine(bottle);
                Check(bottle.unitValue==(beer?81:84) && AgableHelper.GetAge(bottle)==(beer?3:4),"actual native wine aging preserves beer history and increases fruit-wine value");
                AgableHelper.OnAge(bottle);
                Check(bottle.unitValue==(beer?81:87) && AgableHelper.GetAge(bottle)==(beer?3:5),"actual native generic aging cannot bypass the beer guard");
                if(beer)
                {
                    MachineFridge.ChillItem(bottle);
                    Check(bottle.FindItemFeatureByID(MachineFridge.CHILLED_FEATURE_ID).GetCurrentValueMod()==20,"actual native fridge gives custom beer its +20 percent chilled feature");
                    GeneralHelper.SetItemOwned(bottle,false);
                    Check(!MachineFridge.MayZoneAcceptItem(bottle),"actual patched native fridge still rejects unowned beer");
                }
            }
            var beerBottle=TransferBottle(1,2,6);beerBottle.uniqueId=2000;beerBottle.unitValue=81;
            MachineFridge.ChillItem(beerBottle);
            var chilled=beerBottle.FindItemFeatureByID(MachineFridge.CHILLED_FEATURE_ID);
            Check(chilled!=null && chilled.GetCurrentValueMod()==20 && chilled.valueStage==ItemFeature.ValueStage.Innate,"actual native fridge feature is Chilled with an innate +20 percent value modifier");
            Check(beerBottle.IsTag(MachineFridge.CHILLED_KEPT_COLD_TAG) && beerBottle.unitValue==81,"native chilling uses its keep-cold marker and does not rewrite the beer's base or historical value");
            MachineFridge.ChillItem(beerBottle);
            int coldFeatures=0;foreach(var feature in beerBottle.itemFeatures) if(feature.identifier==MachineFridge.CHILLED_FEATURE_ID) coldFeatures++;
            Check(coldFeatures==1,"repeated daily chilling does not stack the +20 percent feature");
            bool wasBeer;EmptyBottlePatch.Prefix(beerBottle,out wasBeer);
            Check(wasBeer && beerBottle.FindItemFeatureByID(MachineFridge.CHILLED_FEATURE_ID)==null && !beerBottle.IsTag(MachineFridge.CHILLED_KEPT_COLD_TAG),"drinking or emptying beer clears the chilled feature before bottle reuse");
            Crops.SetInt(beerBottle,Rules.WineTag,3);
            Check(!Chillable(beerBottle) && !FridgeAccepts(beerBottle) && RackAccepts(beerBottle),"a bottle reused for fruit wine switches back to the ager without a residual beer bonus");
            var native=TransferBottle(0,2,6);AgableHelper.InitAgable(native);native.unitValue=100;
            Check(!MachineFridge.MayZoneAcceptItem(native) && !MachineFridge.IsChillable(native) && MachineWineRack.CanAgeWine(native),"actual native-only wine keeps its original routing");
            WineHelper.OnAgeWine(native);Check(native.unitValue==103,"native-only wine still gains its original daily aging value");
            var disabledBeer=TransferBottle(2,2,5);ModEntry.Enabled=false;
            Check(!MachineFridge.MayZoneAcceptItem(disabledBeer) && !MachineFridge.IsChillable(disabledBeer) && MachineWineRack.CanAgeWine(disabledBeer),"actual Harmony guards honor mod disable state");
        }
        finally {ModEntry.Enabled=previousEnabled;}
        return "Passed "+(checks-beforeChecks)+" native Harmony integration checks against installed game methods and the actual ager inventory callback (headless localization fixture only).";
    }
    private static bool Chillable(GameItem item)
    {
        bool result=MachineFridge.IsChillable(item);BeerChillablePatch.Postfix(item,ref result);return result;
    }
    private static bool RackAccepts(GameItem item)
    {
        bool result=MachineWineRack.CanAgeWine(item);BeerRackPatch.Postfix(item,ref result);return result;
    }
    private static bool Agable(GameItem item)
    {
        bool result=AgableHelper.IsAgable(item);BeerAgablePatch.Postfix(item,ref result);return result;
    }
    private static void VerifyBeverageStorage()
    {
        bool previousEnabled=ModEntry.Enabled;ModEntry.Enabled=true;
        try
        {
            foreach(int kind in new[]{0,1,2,3,4,5,99}) foreach(int state in new[]{0,1,2})
                foreach(int score in new[]{0,1,2,3,5,6}) foreach(bool counterfeit in new[]{false,true})
            {
                var bottle=TransferBottle(kind,state,score);
                if(counterfeit) bottle.EnableTag(WineHelper.WINE_COUNTERFEIT_TAG);
                AgableHelper.InitAgable(bottle);
                bottle.unitValue=149;Crops.SetInt(bottle,AgableHelper.AGE_INT,7);
                bool beer=(kind==1 || kind==2) && state==2;
                bool coldBeer=beer && !counterfeit;
                Check(!MachineFridge.MayZoneAcceptItem(bottle) && !MachineFridge.IsChillable(bottle),"native wine-bottle exclusion is reproduced before the storage hooks");
                Check(FridgeAccepts(bottle)==coldBeer && Chillable(bottle)==coldBeer,"only finished genuine custom beer enters the fridge and follows its chill cycle");
                Check(RackAccepts(bottle)==(state==2 && !counterfeit && !beer),"fruit wines and native wines retain the ager; custom beer is rejected at every score");
                Check(Agable(bottle)==!beer && BeerAgePatch.Prefix(bottle)==!beer,"both generic aging discovery and direct aging reject completed custom beer");
                Check(BeerWineTooltipPatch.Prefix(bottle)==!coldBeer,"finished beer stops advertising native aging while all other wine tooltips remain native");
                if(BeerAgePatch.Prefix(bottle)) WineHelper.OnAgeWine(bottle);
                bool aged=!beer && !counterfeit;
                Check(bottle.unitValue==(aged?152:149) && AgableHelper.GetAge(bottle)==(aged?8:7),"old beer retains historical value and age without further aging; native and fruit-wine gains still work");
                Check(bottle.GetTagReadonly(Rules.WineTag).GetInt()==kind && bottle.GetTagReadonly(WineHelper.WINE_SCORE_INT).GetInt()==score,"storage routing leaves wine identity and quality unchanged");
                if(coldBeer)
                {
                    GeneralHelper.SetItemOwned(bottle,false);
                    Check(!FridgeAccepts(bottle),"fridge ownership restriction still applies to beer");
                    GeneralHelper.SetItemOwned(bottle,true);
                    foreach(string type in new[]{"MACHINE","STORAGE"})
                    {
                        bottle.SetGameItemType(type);Check(!FridgeAccepts(bottle),"fridge rejects machine and storage objects even with a custom beer marker");bottle.RemoveGameItemType(type);
                    }
                }
            }
            var stale=TransferBottle(1,2,5);stale.DisableTag(Rules.WineTag);
            Check(!BeverageStorage.IsBeer(stale) && !Chillable(stale) && RackAccepts(stale),"disabled saved kind data cannot route an unrelated wine into the fridge");
            Check(!BeverageStorage.IsBeer(null) && !BeverageStorage.CanEnterFridge(null),"null storage probes are harmless");
            var nativeDrink=new NativeItem("native_soft_drink");nativeDrink.SetGameItemType("BREVAGE");GeneralHelper.SetItemOwned(nativeDrink,true);
            Check(FridgeAccepts(nativeDrink) && Chillable(nativeDrink),"native drinks retain their original fridge behavior");
            var disabledBeer=TransferBottle(2,2,5);AgableHelper.InitAgable(disabledBeer);
            ModEntry.Enabled=false;
            Check(!FridgeAccepts(disabledBeer) && !Chillable(disabledBeer) && RackAccepts(disabledBeer) && Agable(disabledBeer) && BeerAgePatch.Prefix(disabledBeer) && BeerWineTooltipPatch.Prefix(disabledBeer),"disabling the mod restores all six native storage and tooltip entry points");
        }
        finally {ModEntry.Enabled=previousEnabled;}
    }
    private static void VerifyAutomaticReplanting()
    {
        int nativeSpecies=0,customSpecies=0;
        // Nutrifruit's native genetics consult its story unlock flag. Supply a
        // data-only store for that lookup; no scene objects are constructed.
        var storeField=typeof(PlayerStore).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        object previousStore=storeField.GetValue(null);
        if(previousStore==null) storeField.SetValue(null,FormatterServices.GetUninitializedObject(typeof(PlayerStore)));
        try { foreach(var plant in PlantRegistry.All)
        {
            if(Rules.BySeed(plant.id)==null) nativeSpecies++; else customSpecies++;
            var first=SeedFor(plant);var second=SeedFor(plant);var third=SeedFor(plant);
            var output=new List<GameItem>{new NativeItem("harvested_crop"),first,second,third};
            int moved=0;GameItem planted=null;
            Func<GameItem,bool> accept=seed=>{moved++;planted=seed;return true;};
            Check(AutoPlant.TrySelectAndMove(null,output,plant.id,true,true,accept) && moved==1 && Object.ReferenceEquals(planted,first),"one actual matching seed selected: "+plant.id);
            Check(!AutoPlant.TrySelectAndMove(planted,output,plant.id,true,true,accept) && moved==1,"occupied/perennial input never consumes another seed: "+plant.id);
            Check(!AutoPlant.TrySelectAndMove(null,output,plant.id,false,true,accept) && moved==1,"setting off stops every species: "+plant.id);
            Check(!AutoPlant.TrySelectAndMove(null,output,plant.id,true,false,accept) && moved==1,"unowned machine is excluded: "+plant.id);
            Check(!AutoPlant.TrySelectAndMove(null,output,"unrelated_species",true,true,accept) && moved==1,"different species never substituted: "+plant.id);
            Check(AutoPlant.TrySelectAndMove(null,output,"",true,true,accept) && Object.ReferenceEquals(planted,first),"legacy idle output can seed an unrecorded machine: "+plant.id);
            var main=SeedFor(plant);var stored=SeedFor(plant);
            Check(AutoPlant.TryFromSources(null,output,new[]{main},new[]{stored},plant.id,true,true,accept) && Object.ReferenceEquals(planted,first),"machine output has priority over reserves: "+plant.id);
            Check(AutoPlant.TryFromSources(null,new GameItem[0],new[]{main},new[]{stored},plant.id,true,true,accept) && Object.ReferenceEquals(planted,main),"main inventory has priority over warehouse: "+plant.id);
            Check(AutoPlant.TryFromSources(null,new GameItem[0],new GameItem[0],new[]{stored},plant.id,true,true,accept) && Object.ReferenceEquals(planted,stored),"warehouse supplies a matching seed when needed: "+plant.id);
            Check(!AutoPlant.TryFromSources(null,new GameItem[0],new[]{main},new[]{stored},"",true,true,accept),"unknown machine never chooses a random reserve species: "+plant.id);
            Check(!AutoPlant.TryFromSources(null,new GameItem[0],new[]{main},new[]{stored},plant.id,false,true,accept),"disabled setting stops reserve consumption too: "+plant.id);
            Check(AutoPlant.InferSpecies(new[]{main})==plant.id,"legacy machine species can be inferred from its output seed: "+plant.id);
            int rejected=0;
            Check(!AutoPlant.TrySelectAndMove(null,output,plant.id,true,true,seed=>{rejected++;return false;}) && rejected==3 && first.unitCount==1 && second.unitCount==1,"failed placement consumes no seed: "+plant.id);
            first.EnableTag(HydroponicHelper.GROWING_TAG);
            GeneralHelper.SetItemOwned(second,false);third.EnableTag("CONTAINER_TAG");
            Check(!AutoPlant.TrySelectAndMove(null,output,plant.id,true,true,accept),"growing/unowned/container output is excluded: "+plant.id);
            first.DisableTag(HydroponicHelper.GROWING_TAG);
            first.unitCount=4;
            PlantGeneticsHelper.SetTraitRank(first,"bountiful",3);
            PlantGeneticsHelper.SetTraitRank(first,"slow_grower",0);
            Crops.SetInt(first,PlantGeneticsHelper.GENETIC_HEAT_INT,17);
            Crops.SetInt(first,"OTHER_MOD_SEED_DATA",991);
            first.uniqueId=1234;first.unitValue=765;first.name="edited strain";
            var one=SeedFor(plant);one.uniqueId=5678;
            AutoPlant.CopySeedInstance(first,one);
            Check(one.unitCount==1 && first.unitCount==4,"preparing one stacked seed does not consume the source: "+plant.id);
            Check(one.GetTagReadonly("OTHER_MOD_SEED_DATA").GetInt()==991 && one.uniqueId==5678,"seed metadata copied without duplicating identity: "+plant.id);
            Check(PlantGeneticsHelper.GetTraitRank(one,"bountiful")==3 && PlantGeneticsHelper.GetTraitRank(one,"slow_grower")==0 && PlantGeneticsHelper.GetHeat(one)==17,"edited ranks, deletions and heat survive a stack split: "+plant.id);
            Check(one.name=="edited strain" && one.unitValue==765 && GeneralHelper.IsItemOwned(one),"strain name/value/ownership preserved: "+plant.id);
            PlantGeneticsHelper.SetTraitRank(one,"bountiful",1);
            Check(PlantGeneticsHelper.GetTraitRank(first,"bountiful")==3,"split seed state is independent: "+plant.id);
        } } finally { storeField.SetValue(null,previousStore); }
        Check(nativeSpecies>0 && customSpecies==12,"automatic replanting covers native species and all twelve additions");
        var potato=Ingredient(1);
        Check(AutoPlant.InferSpecies(new[]{potato})==Rules.Crops[1].SeedId,"non-returning custom crops identify the last species for purchased reserves");
        Check(AutoPlant.InferSpecies(new[]{Ingredient(1),Ingredient(0)})=="","mixed legacy output cannot choose an arbitrary reserve species");
        Check(!AutoPlant.TrySelectAndMove(null,new List<GameItem>{new NativeItem("food")},"",true,true,seed=>{throw new Exception("Food must never be planted");}),"crop-only output cannot produce free seeds");
    }
}
