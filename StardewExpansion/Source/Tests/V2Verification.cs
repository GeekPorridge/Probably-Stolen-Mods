using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.IO;
using HarmonyLib;
using PelicanCultivation;

// Run real factories, resource plans, state transitions and native genetics.
// Unity rendering, scene singleton lookup, RNG and placement are boundaries.
public static class V2Verification
{
    private static int checks,day=70,nextId=90000,cash=1000,capacity=100;
    private static readonly Dictionary<string,int> saved=new Dictionary<string,int>();
    private static readonly Dictionary<GameInventory,GameItem> hosts=new Dictionary<GameInventory,GameItem>();
    private static readonly Dictionary<GameItem,GameGridInventory> cages=new Dictionary<GameItem,GameGridInventory>();
    private static PlayerStore store;
    private static EmporiumEntry emporium;
    private static StoreClient client;
    private static IEnumerable<GameItem> dispensers=new GameItem[0];
    private static readonly Dictionary<string,string> locale=new Dictionary<string,string>();
    private static bool dna;
    private static GameGridInventory products;
    private static bool room=true;
    private static int powerCalls;
    private static GameGridInventory watched;
    private static readonly List<string> placements=new List<string>();
    private static UnityEngine.Sprite uiSprite;
    private static readonly List<UnityEngine.Texture2D> sceneTextures=new List<UnityEngine.Texture2D>();
    private static GameItem valueChild,bodyHost;
    private static GameGridInventory bodyGrid;
    private sealed class Item:GameItem
    {
        public Item(string id):base(id){modifiedState=state;uniqueId=++nextId;SetShape(new GridShapeBuilder().SetData(new byte[]{1},1));}
        public override void Validate(){modifiedState=state;}
        public void BindWindow(PixelWindow window){contentWindow=window;}
        public List<GraphNodeStorage> StorageEdges;
        public override List<GraphNodeStorage> children=>StorageEdges??base.children;
        public override void Destroy(){Detach(this);unitCount=0;}
    }
    private sealed class Grid:GameGridInventory{public Grid():base(1,1){}public override void Validate(){}}
    private sealed class Slot:GameSlotInventory{public override void Validate(){}}
    private static GameSlotInventory NewSlot(){var slot=(Slot)FormatterServices.GetUninitializedObject(typeof(Slot));Field(typeof(TagSystem),"<dict>k__BackingField").SetValue(slot,new Dictionary<string,TagState>());return slot;}
    private static void Detach(GameItem item){if(item.parentInventory is GameGridInventory grid)((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(grid)).Remove(item);else if(item.parentInventory is GameSlotInventory slot)Field(typeof(GameSlotInventory),"currentItem").SetValue(slot,null);item.parentInventory=null;}
    private static FieldInfo Field(Type t,string name)=>AccessTools.Field(t,name);
    private static void Check(bool value,string why){if(!value)throw new Exception("V2 FAIL: "+why);checks++;}
    private static void Patch(Harmony h,MethodBase target,string method){if(target==null)throw new Exception("Fixture missing: "+method);h.Patch(target,new HarmonyMethod(typeof(V2Verification).GetMethod(method)));}
    private static GameGridInventory NewGrid(params GameItem[] items)
    {
        var grid=(Grid)FormatterServices.GetUninitializedObject(typeof(Grid));Field(typeof(GameGridInventory),"items").SetValue(grid,new List<GameItem>(items));
        Field(typeof(TagSystem),"<dict>k__BackingField").SetValue(grid,new Dictionary<string,TagState>());
        foreach(var item in items)item.parentInventory=grid;return grid;
    }
    private static GameItem Owned(GameItem item){GeneralHelper.SetItemOwned(item,true);return item;}
    private static GameItem Food(string id,int kcal,long price=100,string meat=null)
    {var item=new Item(id){unitValue=price};FoodItemHelper.InitFoodItem(item,kcal,false,meat!=null);if(meat!=null)FarmSpecies.SetString(item,"MEAT_TYPE_TAG",meat);return Owned(item);}
    private static GameItem Bottle(int kind,int score=2)
    {
        var item=new Item("empty_bottle"){unitValue=kind==7?150:100};item.EnableTag(WineHelper.WINE_CONTAINER_TAG);Crops.SetInt(item,WineHelper.WINE_CONTAINER_STATE,2);Crops.SetInt(item,WineHelper.WINE_SCORE_INT,score);Crops.SetInt(item,Rules.WineTag,kind);
        item.AddItemFeature(ItemFeatureList.CreateWineQualityFeature(score));UseCountHelper.InitUseCountItem(item,4,false,false);return Owned(item);
    }
    private static long FeatureValue(GameItem item)
    {
        object[] stages={0L,0L,1.0,false};AccessTools.Method(typeof(GameItem),"AccumulateFeatureStages").Invoke(item,stages);
        return (long)AccessTools.Method(typeof(GameItem),"ComposeStagedValue").Invoke(null,new object[]{item.unitValue,stages[0],stages[1],stages[2],0,0});
    }
    public static bool Ready(ref bool __result){__result=true;return false;}
    public static bool Day(ref int __result){__result=day;return false;}
    public static bool Get(string key,int fallback,ref int __result){__result=saved.TryGetValue(key,out int value)?value:fallback;return false;}
    public static bool Set(string key,int value){saved[key]=value;return false;}
    public static bool Store(ref PlayerStore __result){__result=store;return false;}
    public static bool Emporium(ref EmporiumEntry __result){__result=emporium;return false;}
    public static bool Client(ref StoreClient __result){__result=client;return false;}
    public static bool Cash(ref int __result){__result=cash;return false;}
    public static bool Credit(int __0){cash+=__0;return false;}
    public static bool Location(ref string __result){__result=Fishing.LocationId;return false;}
    public static bool Reachable(ref IEnumerable<GameItem> __result){__result=dispensers;return false;}
    public static bool Host(GameInventory __instance,ref GameItem __result){__result=hosts.TryGetValue(__instance,out var h)?h:null;return false;}
    public static bool Cage(GameItem __0,ref GameGridInventory __result){__result=cages[__0];return false;}
    public static bool Empty(string identifier,ref GameItem __result){__result=new Item(identifier);return false;}
    public static bool Sprite(GameItem __instance,ref GameItem __result){__result=__instance;return false;}
    public static bool CropSprite(GameItem item,string key,int width,int height){Crops.BindSprite(item,key,width,height);return false;}
    public static bool Factory(string identifier,ref GameItem __result)
    {
        string key=identifier.StartsWith("pelican_")?identifier.Substring(8):identifier;
        int drink=Array.IndexOf(Rules.WineKeys,key);if(identifier.StartsWith("pelican_")&&drink>0){__result=Brewing.DebugFinished(drink);__result.identifier=identifier;return false;}
        if(identifier=="wine_bottle"){__result=new Item(identifier);WineHelper.InitWineBottle(__result);return false;}
        if(Array.IndexOf(ArtisanMachines.Keys,key)>=0){__result=new Item(identifier);__result.onLoaded+=MachineTrade.Refresh;MachineTrade.Refresh(__result);return false;}
        var seed=Rules.BySeed(identifier);if(seed!=null){__result=Crops.CreateSeed(seed);return false;}
        int product=Array.IndexOf(Expansion.ProductKeys,key);if(product>=0){__result=Expansion.Product(product);return false;}
        if(Array.IndexOf(SeafoodRecipes.Keys,key)>=0){__result=SeafoodRecipes.Create(key,900);return false;}
        if(key=="chicken"||key=="goat"){__result=ExpansionStock.CreateAnimal(key=="chicken");return false;}
        if(key=="chicken_egg"){__result=ExpansionStock.CreateEgg();return false;}
        foreach(string fish in ExpansionStock.FishKeys)if(key==fish||key==fish+"_roe"||key==fish+"_fry"){__result=ExpansionStock.CreateFish(fish,key.EndsWith("_roe")?1:key.EndsWith("_fry")?2:0);return false;}
        if(Array.IndexOf(Fishing.Kings,key)>=0){__result=ExpansionStock.King(key);return false;}
        var crop=Rules.Crops.FirstOrDefault(c=>c.ProduceId==identifier);if(crop!=null){__result=Crops.CreateProduce(crop,1);return false;}
        __result=new Item(identifier);if(identifier=="empty_bottle"){__result.SetShape(new GridShapeBuilder().SetData(new byte[]{1,1,1,1},1));__result.EnableTag(WineHelper.WINE_CONTAINER_TAG);Crops.SetInt(__result,WineHelper.WINE_CONTAINER_STATE,0);}return false;
    }
    public static bool Accept(GraphNodeStorage __0,GameItem __1,ref int __result)
    {
        if(__1.parentInventory is GameInventory parent&&parent.mayInventoryRemoveItemFunc!=null&&!parent.mayInventoryRemoveItemFunc.InvokeAllReduce(__1,parent,InvokeFuncExtensions.BoolReduceOR)){__result=1;return false;}
        if(__0 is GameSlotInventory slot){if(slot.childItem!=null||(slot.mayInventoryAddItemFunc!=null&&!slot.mayInventoryAddItemFunc.InvokeAllReduce(__1,slot,InvokeFuncExtensions.BoolReduceOR))){__result=1;return false;}Detach(__1);Field(typeof(GameSlotInventory),"currentItem").SetValue(slot,__1);__1.parentInventory=slot;__result=0;return false;}
        var grid=(GameGridInventory)__0;
        if(grid.mayInventoryAddItemFunc!=null&&!grid.mayInventoryAddItemFunc.InvokeAllReduce(__1,grid,InvokeFuncExtensions.BoolReduceOR)){__result=1;return false;}
        foreach(var other in grid.childItems.ToArray())if(other.MayStack(__1))
        {other.unitCount+=__1.unitCount;__1.Destroy();__result=0;return false;}
        if(grid.childItems.Count>=capacity){__result=1;return false;}
        Detach(__1);((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(grid)).Add(__1);__1.parentInventory=grid;if(grid==watched)placements.Add(__1.identifier);__result=0;return false;
    }
    public static bool CanAccept(GraphNodeStorage __0,GameItem __1,ref bool __result){if(__0 is GameSlotInventory slot){__result=slot.childItem==null&&(slot.mayInventoryAddItemFunc==null||slot.mayInventoryAddItemFunc.InvokeAllReduce(__1,slot,InvokeFuncExtensions.BoolReduceOR));return false;}var grid=(GameGridInventory)__0;__result=grid.childItems.Count<capacity&&(grid.mayInventoryAddItemFunc==null||grid.mayInventoryAddItemFunc.InvokeAllReduce(__1,grid,InvokeFuncExtensions.BoolReduceOR));return false;}
    public static bool Water(GameItem __0,ref int __result){__result=__0.GetTagReadonly("TEST_WATER").GetInt();return false;}
    public static bool Purity(GameItem __0,string __1,ref int __result){__result=__1=="water"?10000:0;return false;}
    public static bool Drain(GameItem __0,int __1){Crops.SetInt(__0,"TEST_WATER",__0.GetTagReadonly("TEST_WATER").GetInt()-__1);return false;}
    public static bool NoOp()=>false;
    public static bool NoKingActivity(ref double __result){__result=1;return false;}
    public static bool Room(ref bool __result){__result=room;return false;}
    public static bool Powered(ref bool __result){__result=true;return false;}
    public static bool SpendPower(ref bool __result){powerCalls++;__result=true;return false;}
    public static bool SceneSprite(UnityEngine.Texture2D texture,ref UnityEngine.Sprite __result){sceneTextures.Add(texture);__result=uiSprite;return false;}
    public static bool Dna(ref bool __result){__result=dna;return false;}
    public static bool AlwaysRoll(ref bool __result){__result=true;return false;}
    public static bool NeverRoll(ref bool __result){__result=false;return false;}
    public static bool Products(ref GameGridInventory __result){__result=products;return false;}
    public static bool FarmHost(GraphNodeStorage __0,ref GameItem __result){__result=__0 is GameInventory inventory&&hosts.TryGetValue(inventory,out var host)?host:null;return false;}
    public static bool Locale(string key,object[] args,ref string __result){__result=locale.TryGetValue(key,out string text)?string.Format(text,args):key;return false;}
    public static bool Text(string key,ref string __result){__result=key;return false;}
    public static bool English(string englishFallback,ref string __result){__result=englishFallback;return false;}
    public static bool SavedText(string text,ref string __result){__result=text;return false;}
    private static void Crafting()
    {
        foreach(int recipe in new[]{0,1,2})
        {
            string id=recipe==0?"pelican_egg":recipe==1?"pelican_milk":"pelican_goat_milk";int kcal=recipe==0?150:recipe==1?350:400;
            var input=Enumerable.Range(0,5).Select(n=>Food(id,kcal)).ToList();var partial=Food(id,kcal-1);input.Insert(0,partial);
            var p=ArtisanMachines.Plan(0,recipe,input);Check(p.Valid&&p.Products.Count==4&&p.Units.Count==4&&!p.Units.ContainsKey(partial)&&p.Nights==(recipe==0?1:2),"dairy takes four full ingredients and rejects partials: "+recipe);
            var output=NewGrid();capacity=2;Check(!ArtisanMachines.Commit(p,output)&&output.childItems.Count==0&&input.All(i=>i.unitCount==1),"partial output staging rolls back without consuming ingredients: "+recipe);
            p=ArtisanMachines.Plan(0,recipe,input);capacity=100;Check(ArtisanMachines.Commit(p,output)&&output.childItems.Count==4&&input.Count(i=>i.unitCount==0)==4,"dairy commits exact ingredient count: "+recipe);
        }
        var oilInput=new List<GameItem>{Food("pelican_truffle",300),Food("pelican_truffle_small",125),Food("pelican_truffle_small",125),Food("pelican_truffle",299)};
        var oil=ArtisanMachines.Plan(1,0,oilInput);Check(oil.Valid&&oil.Products.Count==2&&oil.Units.Count==3&&oil.Nights==2,"oil combines one full truffle and two small ones, capped at two bottles");ArtisanMachines.DestroyPreview(oil);
        foreach(int recipe in new[]{1,2})
        {
            var input=new List<GameItem>{Bottle(6)};for(int n=0;n<4;n++){input.Add(Crops.CreateProduce(Rules.Crops[recipe==1?10:11],1));input.Add(DirectoryMaster.Item("empty_bottle"));}
            var p=ArtisanMachines.Plan(1,recipe,input);Check(p.Valid&&p.Products.Count==5&&p.Units.Count==9&&p.Nights==(recipe==1?2:4),"perfume has four flowers/four bottles and returns the spirit bottle");
            var output=NewGrid();Check(ArtisanMachines.Commit(p,output)&&output.childItems.Count(i=>i.identifier=="wine_bottle")==1&&input.All(i=>i.unitCount==0),"perfume material accounting preserves one returned empty wine bottle");
            input=new List<GameItem>{Bottle(6)};Crops.SetInt(input[0],UseCountHelper.CURRENT_USE_COUNT_INT,3);Check(!ArtisanMachines.Plan(1,recipe,input).Valid,"partial spirit cannot make perfume");
        }
        var raw=Food("raw",2000,200,ExpansionStock.Chicken);var plan=ArtisanMachines.Plan(3,0,new[]{raw});
        Check(plan.Valid&&plan.Products.Sum(i=>i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt())==1800&&plan.Products.Sum(i=>i.unitValue)==240,"plain grill caps meat at 1800 kcal and prices two chicken portions");
        var tray=NewGrid();Check(ArtisanMachines.Commit(plan,tray)&&raw.unitCount==1&&raw.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==200&&raw.unitValue==20,"grill preserves remaining calories and proportional price");
        Check(ArtisanMachines.MeatKind(tray.childItems.First())==null,"cooked meat cannot be processed again");
        foreach(int recipe in new[]{1,2,3})
        {
            var input=new List<GameItem>{Food("raw",1800,200,recipe==3?FishSpecies.Tilapia.speciesID:ExpansionStock.Goat)};
            string extra=recipe==1?"pelican_chili":recipe==2?"pelican_truffle_oil":"pelican_mayonnaise";
            int count=recipe==1?2:1;for(int n=0;n<count;n++)input.Add(Food(extra,recipe==1?120:recipe==2?200:180));
            plan=ArtisanMachines.Plan(3,recipe,input);Check(plan.Valid&&plan.Products.Count==2&&plan.Products.Sum(i=>i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt())==1800+input.Skip(1).Sum(i=>i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()),"seasoned grill includes seasoning calories: "+recipe);
            Check(plan.Products.Sum(i=>i.unitValue)==(recipe==1?430:recipe==2?540:300),"seasoned sale bonus applies per portion: "+recipe);ArtisanMachines.DestroyPreview(plan);
            Crops.SetInt(input[1],"CALORIE_VALUE_TAG",1);Check(!ArtisanMachines.Plan(3,recipe,input).Valid,"partial seasoning cannot obtain a full batch bonus: "+recipe);
        }
        var water=new Item("water");water.EnableTag("LIQUID_CONTAINER_TAG");Crops.SetInt(water,"TEST_WATER",250);
        var wheat=Enumerable.Range(0,5).Select(n=>Food("pelican_wheat",Rules.CropNutrition[3])).ToList();wheat.Add(water);
        plan=ArtisanMachines.Plan(3,4,wheat);Check(plan.Valid&&plan.Products.Sum(i=>i.unitCount)==24&&plan.Water[water]==200&&plan.Units.Count==4,"bait makes at most four groups and takes 50 ml each");
        tray=NewGrid();capacity=3;Check(!ArtisanMachines.Commit(plan,tray)&&water.GetTagReadonly("TEST_WATER").GetInt()==250&&wheat.All(i=>i.unitCount==1),"bait output-full rollback preserves water and wheat");
        plan=ArtisanMachines.Plan(3,4,wheat);capacity=100;Check(ArtisanMachines.Commit(plan,tray)&&water.GetTagReadonly("TEST_WATER").GetInt()==50&&tray.childItems.Sum(i=>i.unitCount)==24,"bait commit consumes exactly 200 ml and creates 24 units");
        var bait=Owned(Expansion.Product(8));bait.SetUnitCount(20);plan=ArtisanMachines.Plan(3,5,new[]{bait,Food("pelican_mayonnaise",180),Food("pelican_mayonnaise",180)});
        Check(plan.Valid&&plan.Units[bait]==12&&plan.Products.Sum(i=>i.unitCount)==12,"premium bait uses exact units from a stack");Check(ArtisanMachines.Commit(plan,NewGrid())&&bait.unitCount==8,"premium bait leaves eight ordinary bait in the original stack");
    }
    private static void BarrelChecks()
    {
        foreach(int kind in new[]{0,7,3})foreach(int score in new[]{0,2,5,6})
        {
            var bottle=Bottle(kind,score);if(kind==0){bottle.DisableTag(Rules.WineTag);bottle.identifier="wine_bottle";}
            Check(Barrel.Accepts(bottle),"native and custom fruit wine are both accepted, including undrinkable original quality");
            int tier=WineHelper.GetWineQualityTier(bottle),baseNights=tier>=5?0:tier>=4?8:14,target=baseNights+32;
            Check(Barrel.Completion(tier)==target,"iridium duration includes original quality and three successive star stages");
            for(int n=1;n<=target;n++)
            {
                Barrel.TickBottle(bottle,day+n);int progress=bottle.GetTagReadonly(Barrel.Nights).GetInt();Barrel.TickBottle(bottle,day+n);
                Check(bottle.GetTagReadonly(Barrel.Nights).GetInt()==progress,"barrel cannot count the same night twice");
                int expectedStar=n>=baseNights+32?3:n>=baseNights+17?2:n>=baseNights+7?1:0;
                Check(bottle.GetTagReadonly(Barrel.Star).GetInt()==expectedStar,"silver/gold/iridium require cumulative 7/17/32 nights after native maximum");
                if(n==6&&tier<4)Check(WineHelper.GetWineQualityTier(bottle)==4&&!bottle.IsTag(Barrel.Bonus),"entry wine reaches reserve after six nights");
                if(n==baseNights)Check(WineHelper.GetWineQualityTier(bottle)==5&&bottle.GetTagReadonly(Barrel.Star).GetInt()==0,"original maximum comes before any star tier");
                if(n==baseNights+7||n==baseNights+17||n==target)
                {
                    int star=bottle.GetTagReadonly(Barrel.Star).GetInt();double nativeMax=bottle.unitValue*1.75;
                    Check(FeatureValue(bottle)==(long)Math.Round(nativeMax*(star==1?1.25:star==2?1.35:1.50)),"each star applies only its own 25/35/50 percent final modifier");
                    Check(bottle.itemFeatures.Count(f=>f.identifier=="pelican_barrel_bonus")==1,"star replacement never duplicates value features");
                }
            }
            Check(WineHelper.GetWineQualityTier(bottle)==5&&bottle.GetTagReadonly(Barrel.Star).GetInt()==3,"barrel finishes iridium for every native/custom entry tier");
            Barrel.TickBottle(bottle,day+100);Check(bottle.itemFeatures.Count(f=>f.identifier=="pelican_barrel_bonus")==1&&bottle.GetTagReadonly(Barrel.Nights).GetInt()==target,"completed barrel reward cannot compound");
            Barrel.RefreshQuality(bottle);Barrel.RefreshQuality(bottle);Check(bottle.itemFeatures.Count(f=>f.identifier=="pelican_barrel_bonus")==1,"refreshing completed wine cannot compound value");
        }
        var legacy=Bottle(7,6);Crops.SetInt(legacy,Barrel.Nights,14);Crops.SetInt(legacy,Barrel.OriginTier,2);legacy.EnableTag(Barrel.Bonus);
        legacy.AddItemFeature(new ItemFeature(Barrel.Bonus){identifier="pelican_barrel_bonus",valueStage=ItemFeature.ValueStage.Final}.SetValueModifier(10));
        for(int n=1;n<=7;n++)Barrel.TickBottle(legacy,day+200+n);
        Check(legacy.GetTagReadonly(Barrel.Star).GetInt()==1&&legacy.itemFeatures.Count(f=>f.identifier=="pelican_barrel_bonus")==1&&FeatureValue(legacy)==328,"r4 completed bottle can continue to silver, replacing its old ten-percent reward");
        legacy.identifier="wine_bottle";var loaded=RoundTrip(legacy);Check(loaded.GetTagReadonly(Barrel.Star).GetInt()==1&&loaded.GetTagReadonly(Barrel.Nights).GetInt()==21&&FeatureValue(loaded)==328,"star quality and exact aging progress survive native save/load; actual star/nights/value="+loaded.GetTagReadonly(Barrel.Star).GetInt()+"/"+loaded.GetTagReadonly(Barrel.Nights).GetInt()+"/"+FeatureValue(loaded));
        WineHelper.ResetToEmptyBottle(loaded);Check(!loaded.IsTag(Barrel.Star)&&!loaded.IsTag(Barrel.Nights)&&!loaded.itemFeatures.Any(f=>f.identifier=="pelican_barrel_bonus"),"emptying removes all aging and star value for the next batch");
        Check(!Barrel.Accepts(Bottle(6)),"spirit cannot enter barrel");var partial=Bottle(7);UseCountHelper.Use(partial);Check(!Barrel.Accepts(partial),"partly drunk wine cannot enter barrel");
        var fake=Bottle(7);fake.EnableTag(WineHelper.WINE_COUNTERFEIT_TAG);Check(!Barrel.Accepts(fake),"counterfeit wine cannot enter barrel");
    }
    private static void DietChecks()
    {
        foreach(var crop in Rules.Crops)Check(PlantDiet.PlantFood(Crops.CreateProduce(crop,1))==!Expansion.Flower(crop),"plant whitelist excludes flowers: "+crop.Key);
        Check(!PlantDiet.PlantFood(Expansion.RawMeat("goat_meat",900))&&!PlantDiet.PlantFood(Expansion.Product(0)),"herbivores reject meat and eggs");
        var dispenser=new Item("dispenser");Crops.SetInt(dispenser,MachineFeedDispenser.STORED_KCAL_TAG,1000);Crops.SetInt(dispenser,MachineFeedDispenser.STORED_OTHER_KEY,800);Crops.SetInt(dispenser,PlantDiet.PlantKcal,500);
        Check(PlantDiet.Draw(dispenser,200)==200&&PlantDiet.Available(dispenser)==300&&dispenser.GetTagReadonly(MachineFeedDispenser.STORED_KCAL_TAG).GetInt()==800&&dispenser.GetTagReadonly(MachineFeedDispenser.STORED_OTHER_KEY).GetInt()==600,"plant drawing decrements total, plant and other counters exactly once");
        Check(PlantDiet.Draw(dispenser,900)==300&&PlantDiet.Available(dispenser)==0&&dispenser.GetTagReadonly(MachineFeedDispenser.STORED_KCAL_TAG).GetInt()==500,"plant draw cannot consume unproven non-plant food");
        Crops.SetInt(dispenser,PlantDiet.PlantKcal,200);PlantDrawProvenancePatch.Prefix(dispenser,out var before);Crops.SetInt(dispenser,MachineFeedDispenser.STORED_OTHER_KEY,150);PlantDrawProvenancePatch.Postfix(dispenser,before);Check(PlantDiet.Available(dispenser)==100,"omnivore mixed-food draws reduce plant provenance proportionally");
        foreach(string species in new[]{FarmRules.Cow,ExpansionStock.Chicken,ExpansionStock.Goat})
        {
            var animal=new Item("animal");FarmSpecies.SetString(animal,"ANIMAL_SPECIES_TAG",species);FarmSpecies.SetFloat(animal,"ANIMAL_MAX_HUNGER_TAG",1000);FarmSpecies.SetFloat(animal,"ANIMAL_HUNGER_TAG",200);
            var meat=Food("meat",900,100,"RAT");var egg=Expansion.Product(0);var corn=Crops.CreateProduce(Rules.Crops[2],1);Owned(corn);Crops.SetInt(corn,"CALORIE_VALUE_TAG",900);corn.unitValue=90;
            var grid=NewGrid(animal,meat,egg,corn);PlantDiet.Feed(animal,grid);
            Check(meat.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==900&&egg.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==150&&corn.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==100&&corn.unitValue==10,"herbivore eats only plant calories and preserves partial portions: "+species);
        }
    }
    private static void GeneticsChecks()
    {
        ChickenCageStorageChecks();
        var mother=Owned(ExpansionStock.CreateAnimal(true));var father=Owned(ExpansionStock.CreateAnimal(true));
        foreach(string gene in ExpansionStock.Chickens.geneticTraitIDs){GeneticHelper.SetTrait(mother,ExpansionStock.Chickens,gene,1);GeneticHelper.SetTrait(father,ExpansionStock.Chickens,gene,2);}
        var egg=(GameItem)AccessTools.Method(typeof(ExpansionStock),"FertilizedEgg").Invoke(null,new object[]{mother,father});Owned(egg);
        Check(egg.IsTag(ExpansionStock.Egg)&&!egg.IsTag("ANIMAL_ITEM_TAG")&&!egg.IsTag("CALORIE_VALUE_TAG")&&egg.mayInventoryRemoveItemFunc==null,"fertile egg is movable, non-food and not a live animal");
        foreach(string gene in ExpansionStock.Chickens.geneticTraitIDs)
        {
            float a=egg.GetTagReadonly(gene+"_ALLELE_A").GetFloat(),b=egg.GetTagReadonly(gene+"_ALLELE_B").GetFloat();
            float maternal=mother.GetTagReadonly(gene+"_ALLELE_A").GetFloat(),paternal=father.GetTagReadonly(gene+"_ALLELE_A").GetFloat();
            Check(a>=maternal*0.7f&&a<=maternal*1.3f&&b>=paternal*0.7f&&b<=paternal*1.3f,"fertile egg retains each parent's allele within native mutation limits: "+gene+"; got "+a+","+b);
        }
        var cage=Owned(new Item("cage"));var grid=NewGrid(egg);grid.SetShape(1,1);cages[cage]=grid;
        cage.ModifyTag(ExpansionStock.BreedMode,s=>s.Enable().SetBool(false));
        ExpansionStock.Hatch(cage);Check(grid.childItems.Single()==egg&&egg.GetTagReadonly(ExpansionStock.EggAge).GetInt()==0,"egg does not incubate on its laying night");
        day++;ExpansionStock.Hatch(cage);ExpansionStock.Hatch(cage);Check(egg.GetTagReadonly(ExpansionStock.EggAge).GetInt()==1,"legacy fertile egg incubates once per night without parents or an enabled mode");
        var hatchFixture=new Harmony("pelican_cultivation.hatch_placement_fixture");GameItem child=null;
        int eggId=egg.uniqueId;Crops.SetInt(egg,"TEST_INHERITED_MARKER",487);var oldShape=egg.modifiedShape.Clone();
        Func<GridShape,GridShape,bool> sameShape=(a,b)=>a.minX==b.minX&&a.minY==b.minY&&a.width==b.width&&a.height==b.height&&a.orientation==b.orientation&&a.flipped==b.flipped&&a.GetOutsideBounds()==b.GetOutsideBounds()
            &&Enumerable.Range(0,a.width*a.height).All(cell=>a.GetLocal(cell%a.width,cell/a.width)==b.GetLocal(cell%b.width,cell/b.width));
        float motherHunger=mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat(),fatherHunger=father.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat();
        watched=grid;
        try
        {
            // Keep the native geometry checks and SlotMarker placement. Replace
            // only scene/render side effects of Expel/UncheckedAccept for this grid.
            Patch(hatchFixture,AccessTools.Method(typeof(GameGridInventory),"Expel"),"HatchExpelFixture");
            Patch(hatchFixture,AccessTools.Method(typeof(GameGridInventory),"UncheckedAccept"),"HatchAcceptFixture");
            hatchExpels=hatchRestores=0;day++;capacity=0;ExpansionStock.Hatch(cage);
            Check(grid.childItems.Single()==egg&&egg.parentInventory==grid&&egg.unitCount==1&&egg.uniqueId==eggId&&sameShape(egg.modifiedShape,oldShape)&&egg.GetTagReadonly(ExpansionStock.EggAge).GetInt()==2&&egg.GetTagReadonly("TEST_INHERITED_MARKER").GetInt()==487&&hatchExpels==1&&hatchRestores==1,
                "failed native placement restores the exact original ready egg, ID, slot and inherited custom state; count="+grid.childItems.Count+",sameEgg="+(grid.childItems.FirstOrDefault()==egg)+",parent="+(egg.parentInventory==grid)+",units="+egg.unitCount+",id="+egg.uniqueId+"/"+eggId+",shape="+sameShape(egg.modifiedShape,oldShape)+",age="+egg.GetTagReadonly(ExpansionStock.EggAge).GetInt()+",marker="+egg.GetTagReadonly("TEST_INHERITED_MARKER").GetInt()+",expel/restore="+hatchExpels+"/"+hatchRestores);
            var oversized=Owned(new Item("oversized_newborn"));oversized.SetShape(new GridShapeBuilder().SetData(new byte[]{1,1},2));
            Check(!ExpansionStock.TryReplaceEgg(grid,egg,oversized)&&grid.childItems.Single()==egg&&egg.parentInventory==grid&&oversized.parentInventory==null,"actual native shape preflight rejects a two-cell newborn in a one-cell cage without removing the egg");oversized.Destroy();
            capacity=1;day++;ExpansionStock.Hatch(cage);child=grid.childItems.Single();
            Check(child!=egg&&child.parentInventory==grid&&child.IsTag("ANIMAL_ITEM_TAG")&&!child.IsTag(ExpansionStock.Egg)&&child.GetTagReadonly("ANIMAL_AGE_TAG").GetInt()==0&&child.GetItemSize()==1&&child.modifiedShape.minX==oldShape.minX&&child.modifiedShape.minY==oldShape.minY&&egg.unitCount==0,"a completely full one-cell cage hatches its one-cell egg directly into the same slot without requiring an extra cell");
            Check(child.GetTagReadonly("TEST_INHERITED_MARKER").GetInt()==487&&mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==motherHunger&&father.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==fatherHunger,"hatching preserves unique inherited state and charges neither parent's nutrition");
        }
        finally{hatchFixture.UnpatchAll(hatchFixture.Id);watched=null;}
        foreach(string gene in ExpansionStock.Chickens.geneticTraitIDs)Check(child.GetTagReadonly(gene+"_ALLELE_A").GetFloat()==egg.GetTagReadonly(gene+"_ALLELE_A").GetFloat()&&child.GetTagReadonly(gene+"_ALLELE_B").GetFloat()==egg.GetTagReadonly(gene+"_ALLELE_B").GetFloat(),"hatch preserves inherited alleles: "+gene);
        var deep=new Item("copy");ExpansionStock.CopyState(child,deep);FarmSpecies.SetFloat(deep,"ANIMAL_HEALTH_TAG",17);Check(child.GetTagReadonly("ANIMAL_HEALTH_TAG").GetFloat()!=17,"inherited tag state is deep copied");capacity=100;
        for(int i=0;i<4;i++)
        {
            var s=ExpansionStock.Fish[ExpansionStock.FishKeys[i]];var fish=ExpansionStock.CreateFish(ExpansionStock.FishKeys[i],0);Crops.SetInt(fish,"FISH_AGE_TAG",s.stageStartDays[4]);FarmSpecies.SetFloat(fish,"FISH_HUNGER_TAG",s.maxHunger);
            var target=AccessTools.Method(typeof(AquacultureHelper),"IsFedAdult");Check((bool)target.Invoke(null,new object[]{fish,s}),"healthy adult new fish can breed: "+i+" stage="+AquacultureHelper.GetStage(fish)+" health="+AquacultureHelper.GetHealth(fish)+" hunger="+AquacultureHelper.GetHunger(fish)+" species="+fish.GetTagReadonly("FISH_SPECIES_TAG").GetString()+" age="+fish.GetTagReadonly("FISH_AGE_TAG").GetInt()+" dead="+AquacultureHelper.IsDead(fish));
            Crops.SetInt(fish,"FISH_AGE_TAG",s.stageStartDays[5]);Check(!(bool)target.Invoke(null,new object[]{fish,s}),"elder new fish cannot breed: "+i);
        }
    }
    private static int hatchExpels,hatchRestores;
    public static bool HatchExpelFixture(GameGridInventory __instance,GameItem item,ref bool __result)
    {
        if(__instance!=watched)return true;
        __result=item?.parentInventory==__instance;if(__result){hatchExpels++;Detach(item);}return false;
    }
    public static bool HatchAcceptFixture(GameGridInventory __instance,GameItem item,ref bool __result)
    {
        if(__instance!=watched)return true;
        if(item==null||item.parentInventory!=null||__instance.childItems.Contains(item)||(!item.IsTag(ExpansionStock.Egg)&&__instance.childItems.Count>=capacity)){__result=false;return false;}
        ((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(__instance)).Add(item);item.parentInventory=__instance;if(item.IsTag(ExpansionStock.Egg))hatchRestores++;__result=true;return false;
    }
    private static void ChickenCageStorageChecks()
    {
        var row=(MachineryPanelRow)FormatterServices.GetUninitializedObject(typeof(MachineryPanelRow));
        var cageGrid=NewGrid(Owned(new Item("saved_hen")));var dispenser=NewSlot();var butcher=NewSlot();var productGrid=NewGrid(Food("pelican_egg",150));
        var panels=new List<MachineryPanel>();
        foreach(var pair in new[]{Tuple.Create("cage",(PixelElement)cageGrid),Tuple.Create("dispenser",(PixelElement)dispenser),Tuple.Create("butcher",(PixelElement)butcher),Tuple.Create("pelican_products",(PixelElement)productGrid),Tuple.Create("pelican_chicken_mode",(PixelElement)FormatterServices.GetUninitializedObject(typeof(MachineryButton)))})
        {
            var panel=(MachineryPanel)FormatterServices.GetUninitializedObject(typeof(MachineryPanel));panel.role=pair.Item1;Field(typeof(MachineryPanel),"<content>k__BackingField").SetValue(panel,pair.Item2);panels.Add(panel);
        }
        Field(typeof(MachineryPanelRow),"panels").SetValue(row,panels);
        var oldStorage=row.children.ToArray();var oldContents=oldStorage.OfType<GameInventory>().SelectMany(inventory=>inventory.childItems).ToArray();
        Check(oldStorage.Length==4&&oldStorage[0]==cageGrid&&oldStorage[1]==dispenser&&oldStorage[2]==butcher&&oldStorage[3]==productGrid,"native storage collection excludes the legacy chicken management button");
        panels.RemoveAt(4);var newStorage=row.children.ToArray();
        Check(oldStorage.SequenceEqual(newStorage)&&oldContents.SequenceEqual(newStorage.OfType<GameInventory>().SelectMany(inventory=>inventory.childItems)),"removing the storage-free management panel preserves native saved inventory indices and every item");
        var cage=(Item)Owned(new Item("layout_cage"));var window=(PixelWindow)FormatterServices.GetUninitializedObject(typeof(PixelWindow));Field(typeof(PixelWindow),"<childElement>k__BackingField").SetValue(window,row);cage.BindWindow(window);
        Check(MachineHelper.GetBodyStorage(cage,0)==cageGrid&&MachineHelper.GetBodyStorage(cage,1)==dispenser&&MachineHelper.GetBodyStorage(cage,2)==butcher&&MachineHelper.GetBodyStorage(cage,3)==productGrid,"native cage and equipment storage access keeps its original indices after panel removal");
        int nativeCalls=0;cage.onCycleEndSlotItemFunc=(self,inventory,slot)=>nativeCalls++;cages[cage]=cageGrid;
        ExpansionStock.AttachCage(cage,row);Check(row.GetPanel("pelican_chicken_mode")==null&&row.Panels.Count==4,"attaching natural chicken behavior adds no management panel or storage");
        cage.onCycleEndSlotItemFunc(cage,null,null);Check(nativeCalls==1,"natural hatch callback preserves the original cage night callback");
    }
    private static void ScentChecks()
    {
        var poppy=Owned(Expansion.Product(6));var fairy=Owned(Expansion.Product(7));
        Check(poppy.unitValue==100&&fairy.unitValue==160&&Scent.Strength(1)==15&&Scent.Strength(2)==25,"perfume maps native strengths and preserves full bottle prices");
        store.currentClientInstance=null;Check(!Scent.CanUse(poppy,20),"perfume cannot be wasted without a customer");
        store.currentClientInstance=(StoreClientInstance)FormatterServices.GetUninitializedObject(typeof(StoreClientInstance));client=new StoreClient{perfumeInfluence=2,clientIntent=StoreClient.ClientIntent.SELLNBUY};
        Check(!Scent.CanUse(poppy,20)&&Scent.CanUse(fairy,30),"native 25% perfume blocks weaker 20% but allows 30%");
        fairy.onActivateSlotItemFunc(fairy,null,null);Check(client.perfumeInfluence==30&&fairy.GetTagReadonly(UseCountHelper.CURRENT_USE_COUNT_INT).GetInt()==2&&fairy.unitValue==107,"strong scent consumes one charge and partial bottle gets native proportional value");
        fairy.onActivateSlotItemFunc(fairy,null,null);Check(fairy.GetTagReadonly(UseCountHelper.CURRENT_USE_COUNT_INT).GetInt()==2,"equal scent cannot waste a charge");
        var native=new Item("native");int used=0;native.mayActivateSlotItemFunc=(s,i,m)=>true;native.canActivateSlotItemFunc=(s,i,m)=>true;native.onActivateSlotItemFunc=(s,i,m)=>used++;Scent.GuardNative(native,25);native.onActivateSlotItemFunc(native,null,null);Check(used==0&&!native.mayActivateSlotItemFunc(native,null,null),"native 25% scent cannot downgrade active 30%");
        var sold=Owned(new Item("sale"));var bought=new Item("purchase");Scent.Apply(sold);Scent.Apply(sold);Scent.Apply(bought);
        Check(sold.itemFeatures.Count(f=>f.identifier=="PELICAN_SCENT_SELL")==1&&sold.itemFeatures.Single(f=>f.identifier=="PELICAN_SCENT_SELL").GetCurrentValueMod()==30&&bought.itemFeatures.Single(f=>f.identifier=="PELICAN_SCENT_BUY").GetCurrentValueMod()==-30,"highest scent gives one sell increase and one buy discount");
        store.currentClientInstance=null;Check(Scent.Current==0,"scent does not persist after the customer leaves");
    }
    private static void ProductionChecks(Harmony h)
    {
        Patch(h,AccessTools.Method(typeof(FarmHusbandry),"Host"),"FarmHost");Patch(h,AccessTools.Method(typeof(FarmHusbandry),"Products"),"Products");
        var cage=Owned(new Item("cage"));products=NewGrid();var mother=Owned(ExpansionStock.CreateAnimal(true));var father=Owned(ExpansionStock.CreateAnimal(true));
        foreach(var a in new[]{mother,father}){Crops.SetInt(a,"ANIMAL_AGE_TAG",4);FarmSpecies.SetFloat(a,"ANIMAL_HEALTH_TAG",100);FarmSpecies.SetFloat(a,"ANIMAL_HUNGER_TAG",800);GeneticHelper.SetTrait(a,ExpansionStock.Chickens,"ANIMAL_MAX_HEALTH_TAG",1);GeneticHelper.SetTrait(a,ExpansionStock.Chickens,"ANIMAL_FERTILITY_TAG",1);}
        FarmSpecies.SetString(mother,"ANIMAL_SEXE_TAG","FEMALE");FarmSpecies.SetString(father,"ANIMAL_SEXE_TAG","MALE");var grid=NewGrid(mother);hosts[grid]=cage;
        Check(ExpansionStock.FertileChance(mother,father)==50,"default healthy pair has 50 percent fertile-egg chance");FarmSpecies.SetFloat(father,"ANIMAL_HEALTH_TAG",50);Check(ExpansionStock.FertileChance(mother,father)==25,"weaker parent health reduces breeding chance");FarmSpecies.SetFloat(father,"ANIMAL_HEALTH_TAG",100);
        capacity=0;day++;ExpansionStock.Produce(mother,grid);Check(products.childItems.Count==0&&mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==800,"full product tray does not charge a hen's calories");
        capacity=100;day++;ExpansionStock.Produce(mother,grid);ExpansionStock.Produce(mother,grid);Check(products.childItems.Count==1&&products.childItems[0].identifier=="pelican_egg"&&mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==650,"laying hen produces once per night at 150 kcal");
        cage.ModifyTag(ExpansionStock.BreedMode,s=>s.Enable().SetBool(false));GraphUtils.TryAcceptAll(grid,father);Patch(h,AccessTools.Method(typeof(RNG),"Roll",new[]{typeof(int)}),"AlwaysRoll");
        day++;FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",800);ExpansionStock.Produce(mother,grid);ExpansionStock.Produce(mother,grid);Check(grid.childItems.Count(i=>i.IsTag(ExpansionStock.Egg))==1&&products.childItems.Count==2&&mother.GetTagReadonly(ExpansionStock.LastFertileEgg).GetInt()==day&&mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==500,"adult mixed-sex chickens naturally produce an extra fertile egg without mode gating, charging exactly 300 kcal once");
        day++;FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",800);ExpansionStock.Produce(mother,grid);Check(grid.childItems.Count(i=>i.IsTag(ExpansionStock.Egg))==1&&products.childItems.Count==3&&mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==650,"fertile-egg cooldown preserves ordinary daily egg production");
        int edible=products.childItems.Count,fertile=grid.childItems.Count(i=>i.IsTag(ExpansionStock.Egg));day+=3;products.mayInventoryAddItemFunc=(item,inventory)=>false;FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",800);ExpansionStock.Produce(mother,grid);
        Check(products.childItems.Count==edible&&grid.childItems.Count(i=>i.IsTag(ExpansionStock.Egg))==fertile+1&&mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==650,"a full edible-product tray does not block or charge for the independent fertile egg");products.mayInventoryAddItemFunc=null;
        day+=3;grid.mayInventoryAddItemFunc=(item,inventory)=>!item.IsTag(ExpansionStock.Egg);FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",800);int lastFertile=mother.GetTagReadonly(ExpansionStock.LastFertileEgg).GetInt();ExpansionStock.Produce(mother,grid);
        Check(products.childItems.Count==edible+1&&grid.childItems.Count(i=>i.IsTag(ExpansionStock.Egg))==fertile+1&&mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==650&&mother.GetTagReadonly(ExpansionStock.LastFertileEgg).GetInt()==lastFertile&&mother.GetTagReadonly(ExpansionStock.PendingFertileEgg).GetBool(),"a full cage retains a successful fertile-egg attempt without charging it or blocking the ordinary egg");
        var roll=AccessTools.Method(typeof(RNG),"Roll",new[]{typeof(int)});h.Unpatch(roll,typeof(V2Verification).GetMethod("AlwaysRoll"));Patch(h,roll,"NeverRoll");grid.mayInventoryAddItemFunc=null;
        day++;FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",800);ExpansionStock.Produce(mother,grid);
        Check(products.childItems.Count==edible+2&&grid.childItems.Count(i=>i.IsTag(ExpansionStock.Egg))==fertile+2&&!mother.IsTag(ExpansionStock.PendingFertileEgg)&&mother.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==500,"waiting fertile egg is laid once space returns without another chance roll");h.Unpatch(roll,typeof(V2Verification).GetMethod("NeverRoll"));Patch(h,roll,"AlwaysRoll");
        ChickenEligibilityChecks();
        var goat=Owned(ExpansionStock.CreateAnimal(false));Crops.SetInt(goat,"ANIMAL_AGE_TAG",8);FarmSpecies.SetString(goat,"ANIMAL_SEXE_TAG","FEMALE");FarmSpecies.SetFloat(goat,"ANIMAL_HEALTH_TAG",100);var goats=NewGrid(goat);hosts[goats]=cage;
        for(int n=0;n<3;n++){day++;FarmSpecies.SetFloat(goat,"ANIMAL_HUNGER_TAG",1100);ExpansionStock.Produce(goat,goats);}Check(products.childItems.Count(i=>i.identifier=="pelican_goat_milk")==1&&goat.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==700,"adult goat produces milk every three nights for 400 kcal without a prior birth");
        Crops.SetInt(goat,"ANIMAL_AGE_TAG",28);for(int n=0;n<3;n++){day++;ExpansionStock.Produce(goat,goats);}Check(products.childItems.Count(i=>i.identifier=="pelican_goat_milk")==1,"elder goat stops producing milk");
        foreach(string key in ExpansionStock.FishKeys)
        {
            var s=ExpansionStock.Fish[key];var female=Owned(ExpansionStock.CreateFish(key,0));var male=Owned(ExpansionStock.CreateFish(key,0));
            foreach(var f in new[]{female,male}){Crops.SetInt(f,"FISH_AGE_TAG",s.stageStartDays[4]);FarmSpecies.SetFloat(f,"FISH_HUNGER_TAG",s.maxHunger);FarmSpecies.SetFloat(f,"FISH_HEALTH_TAG",100);}FarmSpecies.SetString(female,"FISH_SEX_TAG","FEMALE");FarmSpecies.SetString(male,"FISH_SEX_TAG","MALE");
            var tank=NewGrid(female);ExpansionFishBreedingCostPatch.Postfix(tank);Check(tank.childItems.Count==1,"female alone cannot generate new fish eggs: "+key);
            tank=NewGrid(female,male);capacity=2;ExpansionFishBreedingCostPatch.Postfix(tank);Check(tank.childItems.Count==2&&AquacultureHelper.GetHunger(female)==s.maxHunger,"full tank preserves mother's nutrients: "+key);
            capacity=100;float body=female.GetTagReadonly("FISH_KCAL_GROWN_TAG").GetFloat();ExpansionFishBreedingCostPatch.Postfix(tank);var roe=tank.childItems.Single(i=>i.IsTag("FISH_ROE_TAG"));
            Check(AquacultureHelper.GetHunger(female)==s.maxHunger-ExpansionStock.EggCosts[Array.IndexOf(ExpansionStock.FishKeys,key)]&&roe.GetTagReadonly("PELICAN_ROE_MOTHER").GetInt()==female.GetUniqueID()&&female.GetTagReadonly("FISH_KCAL_GROWN_TAG").GetFloat()==body,"actual egg mother pays exact calories without adding body mass: "+key);
            Check(!roe.IsTag("FISH_FERTILIZED_TAG")&&roe.GetTagReadonly("FISH_GROUP_COUNT_TAG").GetInt()==s.groupCount,"roe retains native male-fertilization and clutch count: "+key);
        }
    }
    private static void ChickenEligibilityChecks()
    {
        for(int scenario=0;scenario<13;scenario++)
        {
            var hen=Owned(ExpansionStock.CreateAnimal(true));var rooster=Owned(ExpansionStock.CreateAnimal(true));
            foreach(var bird in new[]{hen,rooster}){Crops.SetInt(bird,"ANIMAL_AGE_TAG",4);FarmSpecies.SetFloat(bird,"ANIMAL_HEALTH_TAG",100);FarmSpecies.SetFloat(bird,"ANIMAL_HUNGER_TAG",800);}
            FarmSpecies.SetString(hen,"ANIMAL_SEXE_TAG","FEMALE");FarmSpecies.SetString(rooster,"ANIMAL_SEXE_TAG","MALE");
            if(scenario==0)Crops.SetInt(rooster,"ANIMAL_AGE_TAG",3);
            if(scenario==1)Crops.SetInt(rooster,"ANIMAL_AGE_TAG",18);
            if(scenario==2)Crops.SetInt(hen,"ANIMAL_AGE_TAG",3);
            if(scenario==3)Crops.SetInt(hen,"ANIMAL_AGE_TAG",18);
            if(scenario==4)rooster.EnableTag("ANIMAL_IS_DEAD_TAG");
            if(scenario==5)rooster.EnableTag("ANIMAL_DISEASED_TAG");
            if(scenario==6)FarmSpecies.SetFloat(rooster,"ANIMAL_HUNGER_TAG",1);
            if(scenario==7)hen.EnableTag("ANIMAL_DISEASED_TAG");
            if(scenario==8)FarmSpecies.SetString(rooster,"ANIMAL_SEXE_TAG","FEMALE");
            if(scenario==9)GeneralHelper.SetItemOwned(rooster,false);
            if(scenario==10)hen.EnableTag("ANIMAL_IS_DEAD_TAG");
            if(scenario==11)hen.EnableTag("ANIMAL_NEUTERED_TAG");
            if(scenario==12)rooster.EnableTag("ANIMAL_NEUTERED_TAG");
            var cage=Owned(new Item("cage"));cage.ModifyTag(ExpansionStock.BreedMode,s=>s.Enable().SetBool(true));var grid=NewGrid(hen,rooster);hosts[grid]=cage;products=NewGrid();day++;
            ExpansionStock.Produce(hen,grid);Check(!grid.childItems.Any(item=>item.IsTag(ExpansionStock.Egg))&&!hen.GetTagReadonly(ExpansionStock.PendingFertileEgg).GetBool(),"natural chicken breeding requires an owned healthy fed adult mixed-sex pair despite a legacy enabled mode: "+scenario);
            if(scenario>=11)Check(products.childItems.Count(item=>item.identifier=="pelican_egg")==1&&hen.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==650,"neutered hen or rooster prevents fertile eggs while ordinary laying still costs only 150 kcal: "+scenario);
        }
        var native=Owned(new Item("vanilla_rat"));FarmSpecies.SetString(native,"ANIMAL_SPECIES_TAG","RAT");var nativeGrid=NewGrid(native);var nativeCage=Owned(new Item("cage"));hosts[nativeGrid]=nativeCage;products=NewGrid();day++;ExpansionStock.Produce(native,nativeGrid);
        Check(nativeGrid.childItems.Count==1&&products.childItems.Count==0&&!native.IsTag("PELICAN_V2_PRODUCE_NIGHT"),"natural chicken production does not intercept vanilla animal production");
    }
    private static void BookChecks(Harmony h,string localePath,string artifactPath)
    {
        Patch(h,AccessTools.Method(typeof(PaperAssets),"Resolve"),"NoOp");Patch(h,AccessTools.Method(typeof(PaperAssets),"DisplaySizeOf"),"NoOp");Patch(h,AccessTools.Method(typeof(NetworkUpgrade),"IsPlantDnaUnlocked"),"Dna");
        h.Unpatch(AccessTools.Method(typeof(ModEntry),"L"),typeof(FarmVerification).GetMethod("TextFixture"));Patch(h,AccessTools.Method(typeof(ModEntry),"L"),"Locale");
        var directory=new ModItemDirectory();Field(typeof(ModItemDirectory),"<factoryDictionary>k__BackingField").SetValue(directory,new Dictionary<string,Func<GameItem>>());Expansion.RegisterItems(directory);ExpansionStock.RegisterItems(directory);
        var catalog=(Dictionary<string,Func<GameItem>>)Field(typeof(ModItemDirectory),"<factoryDictionary>k__BackingField").GetValue(directory);Check(catalog.Count==57&&catalog.ContainsKey("pelican_grill")&&catalog.ContainsKey("pelican_chicken_egg")&&SeafoodRecipes.Keys.All(key=>catalog.ContainsKey("pelican_"+key)),"native mod directory contains all 57 non-crop factories including the three seafood dishes");
        for(int kind=1;kind<Rules.WineKeys.Length;kind++)Check(catalog.ContainsKey("pelican_"+Rules.WineKeys[kind]),"cheat catalog has every finished drink ID");
        Check(!FarmStock.Entries.Any(e=>ExpansionStock.FishKeys.Any(k=>e.Id=="pelican_"+k||e.Id=="pelican_"+k+"_roe")),"Townsman never sells reservoir fish or their eggs");
        foreach(string language in new[]{"zh","en"})
        {
            locale.Clear();foreach(string line in File.ReadAllLines(Path.Combine(localePath,language+".csv"))){int c=line.IndexOf(',');if(c<=0)continue;string v=line.Substring(c+1).Trim();if(v.StartsWith("\"")&&v.EndsWith("\""))v=v.Substring(1,v.Length-2).Replace("\"\"","\"");locale[line.Substring(0,c)]=v.Replace("\\n","\n");}
            foreach(string key in new[]{"plant_manual","animal_manual","artisan_manual"})
            {
                dna=false;var paper=Books.Build(key,text=>text.Length<=120);int expected=key=="plant_manual"?13:key=="animal_manual"?12:11;Check(paper.pageCount>=expected,"native handbook paginates all foundation chapters: "+language+"/"+key);
                Check(Books.BodyFontSize==24&&paper.subtitle.Length==paper.pageCount&&paper.body.Length==paper.pageCount,"native book arrays and night-report font baseline agree");
                Check(paper.images.Length==paper.pageCount&&paper.images.All(image=>!string.IsNullOrEmpty(image)),"every handbook page retains an illustration key");
                foreach(string image in paper.images)Check(File.Exists(Path.Combine(Path.GetDirectoryName(localePath),"Textures","Items",image+".png")),"every illustrated handbook image exists: "+image);
                foreach(var body in paper.body){Check(body.Length<=120,"handbook respects supplied runtime text measurements");Check(!body.Contains("v2_")&&!body.Contains("{0}")&&!body.Contains("\\n"),"handbook resolves dynamic bilingual text: "+key);}
                File.WriteAllText(Path.Combine(artifactPath,"v2-book-"+language+"-"+key+".json"),Newtonsoft.Json.JsonConvert.SerializeObject(paper,Newtonsoft.Json.Formatting.Indented));
                dna=true;var unlocked=Books.Build(key,text=>text.Length<=120);Check(unlocked.pageCount>paper.pageCount||key=="artisan_manual","gene chapters only appear after gene editing unlock: "+key);
                foreach(string suffix in new[]{"legend","crimsonfish","glacierfish","mutant_carp","poppy_perfume","fairy_perfume"})Check(!Expansion.L(suffix+"_desc").Contains("v2_"),"distinct narrative resolved: "+suffix);
                Check(!Expansion.L("fairy_perfume_desc").Contains("3")&&!Expansion.L("poppy_perfume_desc").Contains("3"),"perfume prose does not repeat charge counts");
            }
        }
        dna=false;
    }
    private static void RevisionChecks(Harmony h)
    {
        Patch(h,AccessTools.Method(typeof(LocHelper),"RelocalizeIfMissing"),"SavedText");
        foreach(int kind in new[]{0,1,3})
        {
            var battery=Owned(new Item("battery100"));battery.EnableTag("power_source_item");var module=Owned(new Item("module"));module.EnableTag("MODULE_TAG");module.EnableTag("MODULE_TYPE_UNIVERSAL");
            Check(!ArtisanMachines.AcceptsInput(kind,battery)&&!ArtisanMachines.AcceptsInput(kind,module),"equipment can never enter artisan raw input: "+kind);
        }
        var lobster=Food("pelican_lobster_meat_large",900,180,FarmRules.Lobster);var sea=Food("pelican_sea_cucumber_food",500,100,FarmRules.Sea);
        Check(ArtisanMachines.MeatKind(lobster)==null&&ArtisanMachines.MeatKind(sea)==null&&!ArtisanMachines.Plan(3,0,new[]{lobster,sea}).Valid,"lobster and sea-cucumber meat never become grilled finfish");
        var fish=Food("tilapia_meat_large",1800,200,"native_fish");var mayo=Expansion.Product(2);var p=ArtisanMachines.AutoPlan(3,new[]{fish,mayo});Check(p.Valid&&p.Recipe==3&&p.Products.Count==2,"grill automatically recognizes a complete fish/mayo recipe");ArtisanMachines.DestroyPreview(p);
        var milk=Food("pelican_milk",350);p=ArtisanMachines.AutoPlan(0,new[]{milk});Check(p.Valid&&p.Recipe==1&&p.Nights==2,"cheese press detects milk without a selected recipe");ArtisanMachines.DestroyPreview(p);
        var goat=Expansion.Product(1);p=ArtisanMachines.AutoPlan(0,new[]{goat});Check(p.Valid&&p.Recipe==2,"cheese press automatically distinguishes goat milk");ArtisanMachines.DestroyPreview(p);
        var truffle=Food("pelican_truffle",300);p=ArtisanMachines.AutoPlan(1,new[]{truffle});Check(p.Valid&&p.Recipe==0&&p.Nights==2,"oil press detects truffles without controls");ArtisanMachines.DestroyPreview(p);
        var perfumeInput=new List<GameItem>{Bottle(6)};for(int n=0;n<4;n++){perfumeInput.Add(Crops.CreateProduce(Rules.Crops[10],1));perfumeInput.Add(DirectoryMaster.Item("empty_bottle"));}
        p=ArtisanMachines.AutoPlan(1,perfumeInput);Check(p.Valid&&p.Recipe==1,"oil press detects complete poppy perfume ingredients automatically");
        watched=NewGrid();placements.Clear();Check(ArtisanMachines.Commit(p,watched)&&placements[0]=="wine_bottle","tall returned bottle stages before wider perfume, avoiding a false-full output layout");watched=null;
        var wet=DirectoryMaster.Item("empty_bottle");Crops.SetInt(wet,"TEST_WATER",500);Check(!ArtisanMachines.EmptyBottle(wet),"wine bottle containing water is not consumed as an empty bottle");
        var machine=Owned(new Item("pelican_cheese_press"));var input=NewGrid();var slot=NewSlot();slot.mayInventoryAddItemFunc=(i,inv)=>i.IsTag("power_source_item");var modules=NewGrid();modules.mayInventoryAddItemFunc=(i,inv)=>i.IsTag("MODULE_TYPE_UNIVERSAL");
        var oldBattery=Owned(new Item("old_battery"));oldBattery.EnableTag("power_source_item");var oldModule=Owned(new Item("old_module"));oldModule.EnableTag("MODULE_TAG");oldModule.EnableTag("MODULE_TYPE_UNIVERSAL");
        input=NewGrid(oldBattery,oldModule);input.mayInventoryRemoveItemFunc=(i,inv)=>false;ArtisanMachines.RouteEquipment(machine,input,slot,modules);
        Check(input.childItems.Count==0&&slot.childItem==oldBattery&&modules.childItems.Single()==oldModule&&oldBattery.unitCount==1&&oldModule.unitCount==1,"old misplaced equipment moves to its correct bay without loss");Check(!input.mayInventoryRemoveItemFunc(oldBattery,input),"equipment migration restores the working input lock");
        foreach(var method in typeof(MachineHelper).GetMethods().Where(m=>m.Name=="CanPower"))Patch(h,method,"Powered");Patch(h,AccessTools.Method(typeof(MachineHelper),"TryDrawCyclePower"),"SpendPower");Patch(h,AccessTools.Method(typeof(MachineHelper),"OnMachineActioned"),"NoOp");Patch(h,AccessTools.Method(typeof(ModuleHelper),"GetAjustedQuality"),"NoOp");
        input=NewGrid(Food("pelican_milk",350));var output=NewGrid();Crops.SetInt(machine,MachineProgressHelper.CURRENT_PROCESSING_SPEED_TAG,8);powerCalls=0;day++;
        ArtisanMachines.Tick(machine,0,input,output,slot,modules);Check(machine.GetTagReadonly(ArtisanMachines.WorkTag).GetInt()==8&&input.childItems.Count==1&&output.childItems.Count==0&&powerCalls==1,"automatic milk batch retains the approved two-night timing");
        ArtisanMachines.Tick(machine,0,input,output,slot,modules);Check(powerCalls==1&&machine.GetTagReadonly(ArtisanMachines.WorkTag).GetInt()==8,"repeated callback cannot advance an automatic batch twice in one night");
        capacity=0;day++;ArtisanMachines.Tick(machine,0,input,output,slot,modules);Check(input.childItems.Count==1&&output.childItems.Count==0&&powerCalls==1,"blocked automatic output neither consumes milk nor draws completion power");
        capacity=100;day++;ArtisanMachines.Tick(machine,0,input,output,slot,modules);Check(input.childItems.Count==0&&output.childItems.Single().identifier=="pelican_cheese"&&powerCalls==2,"automatic batch completes exactly once after output room is restored");
        foreach(string key in new[]{"poppy_perfume","fairy_perfume","cheese","goat_cheese","truffle_oil"})
        {
            var item=Expansion.Product(Array.IndexOf(Expansion.ProductKeys,key));Check(item.shape.width==2&&item.shape.height==2,"requested artisan product occupies four cells: "+key);
        }
        foreach(string key in new[]{"wheat","poppy","fairy_rose"}){var crop=Rules.Crops.Single(c=>c.Key==key);var item=Crops.CreateProduce(crop,1);Check(item.shape.width==2&&item.shape.height==2,"requested fixed crop occupies four cells: "+key);}
        var blueberry=Rules.Crops[9];foreach(int size in new[]{0,1,3}){var item=Crops.CreateProduce(blueberry,size);Check(item.GetItemSize()==(size==0?1:size==1?4:9)&&item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==(size==0?80:size==1?160:480),"blueberry size conserves intended cells and nutrition: "+size);}
        foreach(var crop in Rules.Crops){Check(TooltipPolicy.HideModInfo(Crops.CreateSeed(crop))&&TooltipPolicy.HideModInfo(Crops.CreateProduce(crop,1)),"all crop seeds and produce hide appended info: "+crop.Key);}
        Check(TooltipPolicy.HideModInfo(new Item("nutrifruit")),"registered native crops also suppress the appended information section");
        Patch(h,AccessTools.Method(typeof(BottleGeometry),"PlaceExpanded"),"Room");var saved=Owned(Expansion.Product(6));saved.SetShape(new GridShapeBuilder().SetData(new byte[]{1,1},1));UseCountHelper.Use(saved);long price=saved.unitValue;int charges=saved.GetTagReadonly(UseCountHelper.CURRENT_USE_COUNT_INT).GetInt();var oldGrid=NewGrid(saved);
        room=false;ProductGeometry.Apply(saved,"poppy_perfume");Check(saved.GetItemSize()==2&&saved.IsTag(ProductGeometry.Pending)&&saved.parentInventory==oldGrid&&saved.unitValue==price&&saved.GetTagReadonly(UseCountHelper.CURRENT_USE_COUNT_INT).GetInt()==charges,"full legacy inventory preserves old perfume cells, price and remaining uses while waiting");
        room=true;ProductGeometry.Apply(saved,"poppy_perfume");Check(saved.GetItemSize()==4&&!saved.IsTag(ProductGeometry.Pending)&&saved.unitValue==price&&saved.GetTagReadonly(UseCountHelper.CURRENT_USE_COUNT_INT).GetInt()==charges,"legacy perfume expands safely without replenishing price or charges");
        foreach(string key in new[]{"wheat","blueberry"})
        {
            var crop=Rules.Crops.Single(c=>c.Key==key);var old=Food(crop.ProduceId,key=="wheat"?150:80,key=="wheat"?6:10);old.SetShape(new GridShapeBuilder().SetData(new byte[]{1,1},1));Crops.SetInt(old,Rules.CropTag,Rules.Index(crop)+1);Crops.SetInt(old,Rules.SizeTag,1);Crops.SetInt(old,Rules.CropFormatTag,18);Crops.SetInt(old,CropEconomy.FormatTag,CropEconomy.Format);
            var loaded=RoundTrip(old);Check(loaded.GetItemSize()==4&&loaded.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==(key=="wheat"?150:80)&&loaded.unitValue==(key=="wheat"?6:10),"actual saved two-cell crop expands without restoring eaten calories or full value: "+key);
        }
        var oldPerfume=Owned(Expansion.Product(7));UseCountHelper.Use(oldPerfume);oldPerfume.SetShape(new GridShapeBuilder().SetData(new byte[]{1,1},1));var loadedPerfume=RoundTrip(oldPerfume);Check(loadedPerfume.GetItemSize()==4&&loadedPerfume.unitValue==107&&loadedPerfume.GetTagReadonly(UseCountHelper.CURRENT_USE_COUNT_INT).GetInt()==2,"actual saved perfume reloads at four cells with the same remaining charges/value");
    }
    private static GameItem RoundTrip(GameItem item)
    {
        var node=new SaveItemNode{identifier=item.identifier,unitCount=item.unitCount,uuid=++nextId,uniqueId=item.uniqueId,itemType=typeof(GameItem).FullName,itemState=item.state.Clone(),itemShape=new GridShapeBuilder(item.shape),itemModifiedShape=new GridShapeBuilder(item.modifiedShape),unitValue=item.unitValue,itemTypes=new List<string>(item.itemTypes),name=item.name,spriteAtlasPath=ModEntry.ModId,spritePath=item.spritePath};
        // Native EncodeNodes saves ItemFeatures separately in PlayerStore's
        // savedItemFeatureList. Restore that list here as the scene loader does.
        var features=item.itemFeatures.ToArray();
        node.OnBeforeSerialize();node.OnAfterDeserialize();var loaded=SaveManager.DecodeNodes(new List<SaveItemNode>{node});
        loaded.itemFeatures.Clear();foreach(var feature in features)loaded.AddItemFeature(feature);
        loaded.onLoaded?.Invoke(loaded);return loaded;
    }
    private static void SeafoodChecks()
    {
        var lobster=Food("pelican_lobster_meat_large",900,180,FarmRules.Lobster);
        var sea=Food("pelican_sea_cucumber_food",500,100,FarmRules.Sea);
        Check(ArtisanMachines.AcceptsInput(3,lobster)&&ArtisanMachines.AcceptsInput(3,sea),"grill accepts lobster cuts and harvested sea cucumber for their dedicated recipes");
        foreach(var raw in new[]{lobster,sea})
        {
            string key=raw==lobster?"baked_lobster":"scallion_sea_cucumber";
            var plan=ArtisanMachines.AutoPlan(3,new[]{raw});var dish=plan.Products.Single();int calories=raw.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();
            Check(plan.Valid&&plan.Nights==1&&plan.Units.Count==1&&plan.Units[raw]==1&&plan.Calories.Count==0&&dish.identifier=="pelican_"+key,"dedicated seafood recipe consumes one source portion and creates the named dish");
            int cookedCalories=raw==lobster?1080:600;long fixedPrice=raw==lobster?350:200;
            Check(dish.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==cookedCalories&&dish.unitValue==fixedPrice&&dish.IsGameItemType("FOOD")&&dish.IsGameItemType("LUXURY_ITEM")== (raw==lobster),"seafood dish uses requested fixed price, source-dependent increased energy and exact food/luxury classifications");
            Check(!ArtisanMachines.AcceptsInput(3,dish)&&ArtisanMachines.MeatKind(dish)==null&&!ArtisanMachines.AutoPlan(3,new[]{dish}).Valid,"finished seafood cannot be processed again");
            var tray=NewGrid();capacity=0;Check(!ArtisanMachines.Commit(plan,tray)&&raw.unitCount==1&&raw.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==calories&&tray.childItems.Count==0,"blocked seafood output preserves all original ingredients");
            capacity=100;plan=ArtisanMachines.AutoPlan(3,new[]{raw});Check(ArtisanMachines.Commit(plan,tray)&&raw.unitCount==0&&tray.childItems.Count==1,"dedicated seafood recipe commits exactly one input portion");
            dish=tray.childItems.Single();var loaded=RoundTrip(dish);
            Check(loaded.identifier=="pelican_"+key&&loaded.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==cookedCalories&&loaded.unitValue==fixedPrice&&loaded.IsGameItemType("LUXURY_ITEM")== (raw==lobster),"actual seafood save/load preserves cooked nutrition, fixed base value and luxury classification");
        }
        var many=Enumerable.Range(0,5).Select(n=>Food("pelican_lobster_meat_tiny",100,20,FarmRules.Lobster)).ToArray();var batch=ArtisanMachines.AutoPlan(3,many);
        Check(batch.Valid&&batch.Products.Count==4&&batch.Units.Count==4&&batch.Products.All(item=>item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==120&&item.unitValue==350),"lobster batch caps at four, each tiny cut gains only the configured energy percentage and uses the fixed dish base price");ArtisanMachines.DestroyPreview(batch);
        var fish=new List<GameItem>();foreach(string key in new[]{"tuna","lava_eel","midnight_carp","largemouth_bass"})
        {
            var item=Owned(ExpansionStock.CreateFish(key,0));Crops.SetInt(item,"FISH_AGE_TAG",ExpansionStock.Fish[key].stageStartDays[4]);FarmSpecies.SetFloat(item,"FISH_KCAL_EATEN_TAG",1000);item.unitValue=100;fish.Add(item);
            Check(ArtisanMachines.AcceptsInput(3,item),"whole named fish enters the sashimi recipe: "+key);
            var roe=ExpansionStock.CreateFish(key,1);var fry=ExpansionStock.CreateFish(key,2);Check(!ArtisanMachines.AcceptsInput(3,roe)&&!ArtisanMachines.AcceptsInput(3,fry),"sashimi rejects eggs and fry: "+key);
        }
        var platter=ArtisanMachines.AutoPlan(3,fish);Check(platter.Valid&&platter.Recipe==8&&platter.Units.Count==4&&platter.Units.Values.All(n=>n==1)&&platter.Products.Single().identifier=="pelican_sashimi_platter","complete four-species recipe takes priority and consumes each whole fish once");
        var result=platter.Products.Single();Check(result.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==(int)Math.Round(fish.Sum(SeafoodRecipes.FishCalories)*1.2)&&result.unitValue==600&&result.IsGameItemType("FOOD")&&result.IsGameItemType("LUXURY_ITEM"),"sashimi gains twenty percent over actual four-fish harvestable calories and has requested price and classifications");
        capacity=0;var output=NewGrid();Check(!ArtisanMachines.Commit(platter,output)&&fish.All(item=>item.unitCount==1),"full output rolls back sashimi without losing any of its four fish");
        for(int missing=0;missing<4;missing++)Check(!ArtisanMachines.AutoPlan(3,fish.Where((item,index)=>index!=missing).ToArray()).Valid,"sashimi cannot substitute duplicate or missing fish species");
        capacity=100;platter=ArtisanMachines.AutoPlan(3,fish);Check(ArtisanMachines.Commit(platter,output)&&fish.All(item=>item.unitCount==0)&&output.childItems.Single().identifier=="pelican_sashimi_platter","successful sashimi commits all four fish and one platter atomically");
        var wholeSea=Owned(new Item("pelican_sea_cucumber"));wholeSea.EnableTag("FISH_ITEM_TAG");FarmSpecies.SetString(wholeSea,"FISH_SPECIES_TAG",FarmRules.Sea);Crops.SetInt(wholeSea,FarmRules.FedNights,5);var seaPlan=ArtisanMachines.AutoPlan(3,new[]{wholeSea});
        Check(ArtisanMachines.AcceptsInput(3,wholeSea)&&seaPlan.Valid&&seaPlan.Products.Single().identifier=="pelican_scallion_sea_cucumber"&&seaPlan.Products.Single().GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==(int)Math.Round(FarmRules.SeaCalories(5)*1.2),"whole sea cucumber uses its real harvestable nutrition plus the cooking bonus");ArtisanMachines.DestroyPreview(seaPlan);
        foreach(string key in SeafoodRecipes.Keys)
        {
            long price=key=="baked_lobster"?350:key=="scallion_sea_cucumber"?200:600;
            var old=SeafoodRecipes.Create(key,900);old.DisableTag(SeafoodRecipes.BalanceTag);
            Crops.SetInt(old,Expansion.CaloriesTag,1000);Crops.SetInt(old,"CALORIE_VALUE_TAG",1000);Crops.SetInt(old,Expansion.PriceTag,125);old.unitValue=125;
            old.EnableTag("ANIMAL_PRION_TAG");var restored=RoundTrip(old);
            Check(restored.GetTagReadonly(Expansion.CaloriesTag).GetInt()==1200&&restored.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==1200&&restored.unitValue==price,"r5 dishes migrate actual saved energy once and adopt the requested fixed base price");
            Check(restored.IsTag("ANIMAL_PRION_TAG")&&restored.GetTagReadonly(SeafoodRecipes.BalanceTag).GetInt()==6,"dish rebalance preserves disease and stamps migration version");
            SeafoodRecipes.Refresh(restored);var again=RoundTrip(restored);
            Check(again.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==1200&&again.unitValue==price,"repeated refresh/load cannot multiply cooking energy or change the fixed base price");
        }
    }
    public static bool BottleUiFixture(GameItem item)
    {
        item.EnableTag(WineHelper.WINE_CONTAINER_TAG);Crops.SetInt(item,WineHelper.WINE_CONTAINER_STATE,0);item.SetGameItemType("HOUSEHOLD_GOOD");item.unitValue=15;
        item.SetShape(new GridShapeBuilder().SetData(new byte[]{1,1,1,1},1));return false;
    }
    private static void RevisionThreeChecks(Harmony h)
    {
        foreach(var crop in Rules.Crops.Skip(8))
        {
            var seed=Crops.CreateSeed(crop);Check(PlantGeneticsHelper.GetTraits(seed).Count>=3,"new crops carry distinct native trait sets: "+crop.Key);
            foreach(var trait in PlantTraitRegistry.All)PlantGeneticsHelper.SetTraitRank(seed,trait.id,0);
            string old=crop.Key=="chili"||crop.Key=="blueberry"?"perennial":crop.Key=="poppy"?"hardy":"delicious";PlantGeneticsHelper.SetTraitRank(seed,old,1);
            seed.EnableTag("GROWING_TAG");Crops.SetInt(seed,"GROWING_PROGRESS",9);Crops.SetInt(seed,"REQUIRED_PROGRESS",91);CropTraitRevision.Migrate(seed,crop);
            Check(crop.Traits.All(p=>PlantGeneticsHelper.GetTraitRank(seed,p.Key)==p.Value)&&seed.GetTagReadonly("GROWING_PROGRESS").GetInt()==9&&seed.GetTagReadonly("REQUIRED_PROGRESS").GetInt()==91,"unedited r2 strain upgrades without resetting active crop progress");
            PlantGeneticsHelper.SetTraitRank(seed,"hardy",3);CropTraitRevision.Migrate(seed,crop);Check(PlantGeneticsHelper.GetTraitRank(seed,"hardy")==3,"trait upgrade runs only once");
            seed=Crops.CreateSeed(crop);foreach(var trait in PlantTraitRegistry.All)PlantGeneticsHelper.SetTraitRank(seed,trait.id,0);PlantGeneticsHelper.SetTraitRank(seed,old,1);PlantGeneticsHelper.SetTraitRank(seed,"nutritious",2);Crops.SetInt(seed,"GENETIC_HEAT_INT",8);
            CropTraitRevision.Migrate(seed,crop);Check(PlantGeneticsHelper.GetTraits(seed).Count==2&&PlantGeneticsHelper.GetTraitRank(seed,"nutritious")==2&&PlantGeneticsHelper.GetHeat(seed)==8,"edited saved genes and heat remain intact");
        }
        foreach(string king in Fishing.Kings)
        {
            var item=Owned(ExpansionStock.King(king));Check(item.GetItemSize()==12,"all four kings occupy twelve cells");
            item.SetShape(new GridShapeBuilder().SetData(new byte[]{1,1,1,1,1,1,1,1},4));item.unitValue=391;var loaded=RoundTrip(item);
            Check(loaded.GetItemSize()==12&&loaded.unitValue==391,"native save decoder migrates eight-cell king without resetting price");
            var parent=NewGrid(item);room=false;ProductGeometry.Apply(item,king);Check(item.GetItemSize()==8&&item.IsTag(ProductGeometry.Pending)&&item.parentInventory==parent,"full inventory delays king expansion without overlap");
            room=true;ProductGeometry.Apply(item,king);Check(item.GetItemSize()==12&&!item.IsTag(ProductGeometry.Pending)&&item.unitValue==391,"king expansion retries with price intact");
        }
        var manual=Books.Item("plant_manual");Check(!manual.IsTag("BOOK_TAG")&&!manual.IsTag("BOOK_ID"),"illustrated manuals never register with the shared native book manager");
        Crops.SetInt(manual,ManualWindow.PageTag,1);Check(manual.GetTagReadonly("CURRENT_BOOK_PAGE").GetInt()==0,"manual page state is independent of vanilla page tags");
        var vanilla=new Item("water_book");Crops.SetInt(vanilla,"CURRENT_BOOK_PAGE",0);Check(BookHelper.CanFlipLeft(vanilla),"unrelated vanilla books retain native index navigation");
        var directory=new ModItemDirectory();Field(typeof(ModItemDirectory),"<factoryDictionary>k__BackingField").SetValue(directory,new Dictionary<string,Func<GameItem>>());Expansion.RegisterItems(directory);
        for(int kind=1;kind<Rules.WineKeys.Length;kind++)
        {
            string id="pelican_"+Rules.WineKeys[kind];var bottle=directory.Create(id);
            Check(bottle.identifier==id&&WineHelper.IsFinishedWine(bottle)&&WineHelper.GetWineQualityTier(bottle)>0&&bottle.unitValue==Rules.WinePrices[kind],"native debug catalog creates usable graded finished drink: "+id);
            Check(bottle.GetItemSize()==(kind<=2||kind==6?6:4)&&bottle.onActivateSlotItemFunc!=null,"debug drink preserves native activation and requested bottle size");
            bottle.unitValue=213;var loaded=RoundTrip(bottle);Check(loaded.identifier==id&&loaded.GetTagReadonly(Rules.WineTag).GetInt()==kind&&WineHelper.IsFinishedWine(loaded)&&loaded.unitValue==213,"custom debug drink survives real native save decoding");
            if(kind==6)Check(loaded.IsGameItemType("ALCOHOL")&&!loaded.IsGameItemType("SUBSTANCE")&&!loaded.IsGameItemType("LUXURY_ITEM")&&!BeverageStorage.CanChill(loaded),"spirit is alcohol and does not inherit fruit-wine or cold-beer categories");
            WineHelper.ResetToEmptyBottle(loaded);Check(loaded.identifier=="wine_bottle"&&!WineHelper.IsFinishedWine(loaded)&&!loaded.IsTag(Rules.WineTag),"empty debug drink becomes native empty bottle");
            var empty=RoundTrip(loaded);Check(empty.identifier=="wine_bottle"&&!WineHelper.IsFinishedWine(empty),"emptied debug drink cannot become full again on reload");
        }
    }
    private static void ReservoirChecks(Harmony h)
    {
        var mod=(LoadedMod)FormatterServices.GetUninitializedObject(typeof(LoadedMod));Field(typeof(LoadedMod),"Manifest").SetValue(mod,new ModManifest{ID=ModEntry.ModId});
        Field(typeof(LoadedMod),"Textures").SetValue(mod,new Dictionary<string,UnityEngine.Texture2D>());Field(typeof(LoadedMod),"Sprites").SetValue(mod,new Dictionary<string,UnityEngine.Sprite>());Field(typeof(LoadedMod),"PaperSprites").SetValue(mod,new Dictionary<string,UnityEngine.Sprite>());
        var card=(UnityEngine.Texture2D)FormatterServices.GetUninitializedObject(typeof(UnityEngine.Texture2D));var scene=(UnityEngine.Texture2D)FormatterServices.GetUninitializedObject(typeof(UnityEngine.Texture2D));uiSprite=(UnityEngine.Sprite)FormatterServices.GetUninitializedObject(typeof(UnityEngine.Sprite));
        var pointer=Field(typeof(UnityEngine.Object),"m_CachedPtr");foreach(var obj in new UnityEngine.Object[]{card,scene,uiSprite})pointer.SetValue(obj,new IntPtr(1));
        // Current ModLoader.Mods returns a defensive copy. Modify only the
        // isolated harness registry, not the copy returned by its public API.
        var registry=(List<LoadedMod>)Field(typeof(ModLoader),"mods").GetValue(null);
        mod.Textures["reservoir_card"]=card;mod.Textures["reservoir_scene"]=scene;registry.Add(mod);
        var manager=(CustomUIManager)FormatterServices.GetUninitializedObject(typeof(CustomUIManager));
        try
        {
            Check(ReferenceEquals(manager.GetModSprite(ModEntry.ModId,"reservoir_card",true),null)&&ReferenceEquals(manager.GetModSprite(ModEntry.ModId,"reservoir_scene",true),null),"native UI resolver reproduces both missing images when they are only textures");
            Patch(h,AccessTools.Method(typeof(UnityEngine.Sprite),"Create",new[]{typeof(UnityEngine.Texture2D),typeof(UnityEngine.Rect),typeof(UnityEngine.Vector2),typeof(float)}),"SceneSprite");sceneTextures.Clear();Assets.RegisterReservoirSprites(mod);
            Check(ReferenceEquals(manager.GetModSprite(ModEntry.ModId,"reservoir_card",true),uiSprite)&&ReferenceEquals(manager.GetModSprite(ModEntry.ModId,"reservoir_scene",true),uiSprite),"actual native UI resolver now finds both registered reservoir sprite aliases");
            Check(sceneTextures.Count==2&&ReferenceEquals(sceneTextures[0],card)&&ReferenceEquals(sceneTextures[1],scene)&&mod.Sprites.Count==0,"reservoir registration uses both source textures without entering inventory sprite scaling");
            Check(Math.Abs(ReservoirStage.CoverScale(9.6f,5.4f,19.2f,10.8f)-2)<0.0001f&&Math.Abs(ReservoirStage.CoverScale(19.2f,10.8f,25.6f,10.8f)-4f/3)<0.0001f,"reservoir background covers the camera for normal and ultrawide aspect ratios");
        }
        finally{registry.Remove(mod);foreach(var obj in new UnityEngine.Object[]{card,scene,uiSprite})pointer.SetValue(obj,IntPtr.Zero);uiSprite=null;sceneTextures.Clear();}
    }
    public static bool ChildValue(GameItem __instance,ref long __result){if(__instance!=valueChild)return true;__result=213;return false;}
    public static bool DialogueText(Dialogue __instance,ref Dialogue __result){__result=__instance;return false;}
    public static bool BodyGrid(GameItem machine,ref GraphNodeStorage __result){if(machine!=bodyHost)return true;__result=bodyGrid;return false;}
    private static void RevisionFourChecks(Harmony h)
    {
        Patch(h,AccessTools.PropertyGetter(typeof(DebugPanel),"Instance"),"NoOp");
        foreach(int kind in Enumerable.Range(0,4))
        {
            var machine=Owned(new Item("pelican_"+ArtisanMachines.Keys[kind]));Check(machine.unitValue==new[]{72,96,72,80}[kind],"owned machines use the approved 16 percent chassis price");
            machine.AddItemFeature(new ItemFeature("test"){identifier="client_bonus",valueStage=ItemFeature.ValueStage.Market,isFeatureMatch=true}.SetValueModifier(500));
            Check(machine.GetNegociatedValue()<=MachineTrade.Resale(kind),"actual native negotiated value cannot apply a reseller markup above the used-machine ceiling");
            long inflated=50000;ArtisanCurrentPricePatch.Postfix(machine,true,ref inflated);Check(inflated==MachineTrade.Resale(kind),"current estimate follows the same resale ceiling");
            machine.unitValue=ArtisanMachines.Prices[kind];var loaded=RoundTrip(machine);Check(loaded.unitValue==MachineTrade.Resale(kind),"native saved old retail price migrates to owned resale price");
            GeneralHelper.SetItemOwned(machine,false);Check(machine.unitValue==ArtisanMachines.Prices[kind],"supplier-owned machine uses retail price");
        }
        Patch(h,AccessTools.Method(typeof(GameItem),"GetCurrentValue"),"ChildValue");
        var host=(Item)Owned(new Item("pelican_grill"));valueChild=new Item("valuable_contents");var contents=NewGrid(valueChild);host.StorageEdges=new List<GraphNodeStorage>{contents};
        host.AddItemFeature(new ItemFeature("test"){identifier="bonus",valueStage=ItemFeature.ValueStage.Market,isFeatureMatch=true}.SetValueModifier(500));
        Check(host.GetNegociatedValue()==293,"machine resale cap preserves the independent value of player-supplied contents");valueChild=null;
        var barrel=Owned(new Item("pelican_barrel"));var bottles=Enumerable.Range(0,9).Select(n=>Bottle(7)).ToArray();var grid=NewGrid(bottles);grid.SetShape(4,4);
        Check(Barrel.CanAdd(barrel,grid,Bottle(7)),"barrel accepts the tenth whole bottle");
        ((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(grid)).Add(Bottle(7));Check(!Barrel.CanAdd(barrel,grid,Bottle(7)),"barrel rejects the eleventh bottle regardless of free cells");
        var stack=Bottle(7);stack.SetUnitCount(2);Check(!Barrel.CanAdd(barrel,NewGrid(),stack),"stacked wine cannot bypass the ten-bottle limit");
        bodyHost=barrel;bodyGrid=grid;Patch(h,AccessTools.Method(typeof(MachineHelper),"GetBodyStorage"),"BodyGrid");Crops.SetInt(bottles[0],Barrel.Nights,5);Barrel.Resize(barrel);
        Check(grid.inventoryShape.width==10&&grid.inventoryShape.height==4&&grid.childItems.Count==10&&bottles[0].GetTagReadonly(Barrel.Nights).GetInt()==5,"old barrel grid expands without deleting bottles or resetting barrel aging");bodyHost=null;bodyGrid=null;
        for(int kind=1;kind<Rules.WineKeys.Length;kind++){var bottle=Brewing.DebugFinished(kind);Check(TooltipPolicy.HideModInfo(bottle)&&!CropModInfoPatch.Prefix(bottle),"all seven wines suppress appended information");}
        var nativeAlcohol=new Item("native_alcohol");nativeAlcohol.SetGameItemType("ALCOHOL");Check(TooltipPolicy.HideModInfo(nativeAlcohol),"native alcohol uses the same appendix suppression");
        var spirit=Brewing.DebugFinished(6);spirit.identifier="pelican_chili_spirit";var input=new List<GameItem>{spirit};
        for(int n=0;n<4;n++){input.Add(Crops.CreateProduce(Rules.Crops[10],1));input.Add(DirectoryMaster.Item("wine_bottle"));}
        var plan=ArtisanMachines.Plan(1,1,input);Check(plan.Valid&&plan.Products.Count(p=>p.identifier=="wine_bottle"&&!WineHelper.IsFinishedWine(p))==1&&!plan.Products.Any(WineHelper.IsFinishedWine),"cheat-ID spirit returns an empty native bottle, never a fresh full spirit");ArtisanMachines.DestroyPreview(plan);
        var usable=new GridShapeBuilder(ArtisanMachines.InputWidth(1),4).SetDataFill(0).SetDataOutside(1).Build();var old=new GridShapeBuilder(8,4).SetDataFill(0).SetDataOutside(1).Build();
        var placed=new List<GridShape>();int bottleX=0,flower=0;
        foreach(var raw in input)
        {
            int x,y;if(raw==spirit){x=8;y=0;}else if(raw.IsTag(WineHelper.WINE_CONTAINER_TAG)){x=bottleX++;y=0;}else{x=4+flower%2*2;y=flower/2*2;flower++;}
            var shape=new GridShapeBuilder(raw.shape).SetTransform(x,y,false,0).Build();
            Check(shape.FullyIntersects(usable,(a,b)=>a!=1||b==0)&&placed.All(p=>!shape.Intersects(p)),"complete perfume ingredients have a non-overlapping placement in the actual new input dimensions");placed.Add(shape);
        }
        Check(input.Sum(p=>p.GetItemSize())==38&&!placed[0].FullyIntersects(old,(a,b)=>a!=1||b==0),"38-cell perfume recipe proves the previous 32-cell input was insufficient");
        var friday=FarmStock.Quantities(5,true,true,true,true,true,true);Check(friday.Values.Sum()==5&&ArtisanMachines.Keys.All(k=>friday["v2_"+k]==1)&&friday["v2_artisan_manual"]==1,"Friday artisan stock contains exactly four machines and the artisan handbook");
        var wed=FarmStock.Quantities(3,true,true,true,true,true,true);Check(ArtisanMachines.Keys.All(k=>wed["v2_"+k]==0)&&wed["v2_plant_manual"]==1&&wed["v2_animal_manual"]==1,"Wednesday retains farming books and crops without artisan machinery");
        Patch(h,AccessTools.PropertyGetter(typeof(FarmStock),"Farming"),"Ready");foreach(string name in new[]{"Mammals","Aquaculture","Advanced"})Patch(h,AccessTools.PropertyGetter(typeof(FarmStock),name),"Dna");dna=false;
        Patch(h,AccessTools.Method(typeof(Dialogue),"SetText",new[]{typeof(string),typeof(string)}),"DialogueText");
        day=75;saved["farmer_visits"]=3;saved["farmer_last_visit_day"]=73;var artisan=new StoreClient{identifier=Merchant.Id,displayName="Artisan"};Merchant.OnVisit(artisan);Merchant.OnVisit(artisan);
        Check(saved["farmer_visits"]==3&&saved["artisan_visits"]==1&&TownDialogue.CurrentCode==400,"Friday persists one artisan visit without advancing ancient-seed gift visit counts");
        foreach(string key in new[]{"farmer_chat_artisan_0","farmer_chat_artisan_1","farmer_chat_artisan_2","farmer_chat_artisan_3","farmer_goods_artisan"})Check(locale.ContainsKey(key)&&!string.IsNullOrWhiteSpace(locale[key]),"artisan visit text is localized: "+key);
    }
    private static void FishingChecks()
    {
        FishingDifficultyChecks();
        int[] ready=new int[4];foreach(bool premium in new[]{false,true})
        {
            int[] counts=new int[9];for(int n=0;n<100000;n++)counts[Fishing.Select((n+0.5)/100000.0,0.2,premium,day,ready)]++;
            Check(counts[4]==(premium?750:500)&&counts[5]==(premium?1500:1000)&&counts[6]==counts[5]&&counts[7]==counts[5]&&counts[8]==(premium?3000:8000),"exact per-cast king and junk intervals: premium="+premium);
            ready[0]=day+1;Check(Fishing.Select(0.001,0.8,premium,day,ready)==2&&Fishing.Select(0.01,0.8,premium,day,ready)==5,"ineligible legend interval returns ordinary without changing another king");ready[0]=0;
        }
        Check(Fishing.TargetCenter(0,0)!=Fishing.TargetCenter(0,1)&&Fishing.TargetSpeed(4)>Fishing.TargetSpeed(0),"green target moves horizontally and kings move faster");
        bool bounded=true;for(int fish=0;fish<9;fish++)for(int frame=0;frame<800;frame++){float center=Fishing.TargetCenter(fish,frame/10f),width=Fishing.ZoneWidth(fish);bounded&=center-width/2>=-0.0001f&&center+width/2<=1.0001f;}
        Check(bounded,"moving target stays entirely inside its track for ordinary fish, every king and junk");
        Check(Fishing.Pull(0.5f,true,0,0.1f,0)>0.5f&&Fishing.Pull(0.5f,false,0,0.1f,0)<0.5f,"holding raises the cursor and releasing lowers it");
        float moved=Fishing.TargetCenter(0,1);Check(Fishing.ReelProgress(0.5f,moved,1,0,1)>0.5f&&Fishing.ReelProgress(0.5f,0,1,0,1)<0.5f,"reeling depends on the current moving target rather than a fixed center");
        Check(Fishing.ViewScale(1920,1080,490,400)==1&&Fishing.ViewScale(400,300,490,500)*490<=376.001f&&Fishing.ViewScale(400,300,490,500)*500<=276.001f,"oversized panel scales inside the actual visible viewport including margins");
        saved.Clear();Fishing.Leave();
        var normal=Owned(Expansion.Product(8));normal.SetUnitCount(10);var premiumBait=Owned(Expansion.Product(9));premiumBait.SetUnitCount(3);
        var pouch=(Item)Owned(new Item("pouch"));pouch.StorageEdges=new List<GraphNodeStorage>{NewGrid(premiumBait)};
        var inaccessible=(Item)new Item("unowned_pouch");var excluded=Owned(Expansion.Product(8));excluded.SetUnitCount(20);inaccessible.StorageEdges=new List<GraphNodeStorage>{NewGrid(excluded)};
        var bag=(Item)Owned(new Item("backpack"));var bagGrid=NewGrid(normal,pouch,inaccessible);bag.StorageEdges=new List<GraphNodeStorage>{bagGrid};
        emporium.afterhourPocketSlotInvBackpack=NewSlot();Field(typeof(GameSlotInventory),"currentItem").SetValue(emporium.afterhourPocketSlotInvBackpack,bag);bag.parentInventory=emporium.afterhourPocketSlotInvBackpack;
        var groundBait=Owned(Expansion.Product(8));groundBait.SetUnitCount(30);emporium.afterhourInventory=NewGrid(groundBait);
        emporium.afterhourPocketSlotInvLeft=NewSlot();var pocketBait=Owned(Expansion.Product(8));pocketBait.SetUnitCount(50);Field(typeof(GameSlotInventory),"currentItem").SetValue(emporium.afterhourPocketSlotInvLeft,pocketBait);
        int money=cash;Fishing.Arrive();Check(Fishing.CountBait(false)==10&&Fishing.CountBait(true)==3&&Fishing.Remaining==13,"entry counts nested backpack bait and excludes ground, side pockets and inaccessible containers");
        Check(cash==money&&Progress.Get("fishing_gift")==0&&emporium.afterhourInventory.childItems.Count==1,"arrival creates no gift or purchase charge");
        normal.SetUnitCount(20);Fishing.Arrive();Check(Fishing.Remaining==13&&Progress.Get("fishing_limit")==13,"adding bait and reentering cannot reset or enlarge the entry quota");normal.SetUnitCount(10);
        Field(typeof(UnityEngine.Time),"CodexTime").SetValue(null,0f);Check(Fishing.SelectBait(true)&&Progress.Get("fishing_selected_bait")==1,"premium bait button chooses the explicit premium type");
        var levelFixture=new Harmony("pelican_cultivation.fishing_difficulty_fixture");
        try
        {
            fishingConfigLevel=1;Patch(levelFixture,AccessTools.PropertyGetter(typeof(FishingDifficulty),"Current"),"FishingLevelFixture");
            Fishing.Cast();Check((bool)Field(typeof(Fishing),"active").GetValue(null)&&premiumBait.unitCount==2&&normal.unitCount==10&&Progress.Get("fishing_used")==1,"casting premium immediately spends one real premium bait and one attempt only");
            int selectedFish=(int)Field(typeof(Fishing),"choice").GetValue(null);fishingConfigLevel=5;Fishing.Advance(true,0.4f);
            Check((int)Field(typeof(Fishing),"fightDifficulty").GetValue(null)==1&&Math.Abs((float)Field(typeof(Fishing),"tension").GetValue(null)-Fishing.Pull(0.5f,true,0.05f,0.05f,selectedFish,1))<0.0001f,"changing the setting during a paid cast preserves its captured cursor response, target phase and width");
        }
        finally{levelFixture.UnpatchAll(levelFixture.Id);fishingConfigLevel=FishingDifficulty.Default;}
        Check(!Fishing.SelectBait(false)&&(bool)Field(typeof(Fishing),"premiumCast").GetValue(null),"bait choice cannot change during an already paid cast");
        Field(typeof(Fishing),"choice").SetValue(null,4);AccessTools.Method(typeof(Fishing),"Complete").Invoke(null,new object[]{false});AccessTools.Method(typeof(Fishing),"Complete").Invoke(null,new object[]{false});
        Check(premiumBait.unitCount==2&&normal.unitCount==10&&Progress.Get("fishing_used")==1&&Progress.Get("fishing_pending")==0&&Progress.Get("fishing_ready_legend")==0,"failure spends the paid bait once, creates no reward and starts no cooldown");
        Fishing.SelectBait(false);Fishing.Cast();Check(normal.unitCount==9&&premiumBait.unitCount==2&&Progress.Get("fishing_used")==2,"ordinary bait button spends ordinary bait without touching premium stock");
        Check((int)Field(typeof(Fishing),"fightDifficulty").GetValue(null)==FishingDifficulty.Default,"a later cast reads the current default difficulty rather than retaining the previous fight snapshot");
        Check(Fishing.UpdatePause(true,0.1f)&&Fishing.UpdatePause(true,60f),"menus pause once without replacing the initial pause timestamp");
        Fishing.Advance(true,60f);Check((float)Field(typeof(Fishing),"tension").GetValue(null)==0.5f&&(float)Field(typeof(Fishing),"reeled").GetValue(null)==0&&normal.unitCount==9&&Progress.Get("fishing_used")==2,"paused input changes neither fight state nor prepaid bait and attempt count");
        Check(!Fishing.UpdatePause(false,70.1f)&&Math.Abs((float)Field(typeof(Fishing),"starts").GetValue(null)-70.35f)<0.001f&&Math.Abs((float)Field(typeof(Fishing),"lastTick").GetValue(null)-70.35f)<0.001f,"resuming shifts both clocks and preserves the remaining cast delay");
        Fishing.Advance(false,70.4f);float beforePause=(float)Field(typeof(Fishing),"tension").GetValue(null),beforeProgress=(float)Field(typeof(Fishing),"reeled").GetValue(null);Field(typeof(Fishing),"danger").SetValue(null,0.25f);
        Fishing.UpdatePause(true,70.45f);AccessTools.Method(typeof(Fishing),"ClearPanel").Invoke(null,null);Fishing.Advance(true,150f);Fishing.UpdatePause(false,170.45f);
        Check((bool)Field(typeof(Fishing),"active").GetValue(null)&&(float)Field(typeof(Fishing),"tension").GetValue(null)==beforePause&&(float)Field(typeof(Fishing),"reeled").GetValue(null)==beforeProgress&&(float)Field(typeof(Fishing),"danger").GetValue(null)==0.25f&&normal.unitCount==9&&Progress.Get("fishing_used")==2,"destroying and restoring presentation preserves the active paid fight and exact paused state");
        Fishing.Advance(false,170.5f);Check((bool)Field(typeof(Fishing),"active").GetValue(null)&&normal.unitCount==9&&Progress.Get("fishing_used")==2,"long menu pause does not timeout or pay the same cast again on resume");
        Fishing.UpdatePause(true,170.6f);Fishing.Leave();Check(!(bool)Field(typeof(Fishing),"paused").GetValue(null)&&normal.unitCount==9&&Progress.Get("fishing_used")==2,"returning during a cast clears pause state and retains its single up-front payment");
        // r5 can leave an unpaid pending catch. Its original payment flag must
        // still settle one matching backpack bait without resetting the quota.
        emporium.afterhourInventory=NewGrid();capacity=0;Progress.Set("fishing_pending",5);Progress.Set("fishing_pending_payment",1);Progress.Set("fishing_pending_bait",0);Fishing.Arrive();
        Check(Progress.Get("fishing_pending")==5&&normal.unitCount==9&&Progress.Get("fishing_used")==2&&Progress.Get("fishing_limit")==13,"legacy unpaid full-ground reward survives reentry without resetting or charging quota");
        Detach(normal);capacity=1;Fishing.TryClaim();Check(Progress.Get("fishing_pending")==5&&emporium.afterhourInventory.childItems.Count==0,"legacy unpaid reward cannot use ground bait or a different bait type");
        ((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(bagGrid)).Add(normal);normal.parentInventory=bagGrid;Fishing.TryClaim();
        Check(Progress.Get("fishing_pending")==0&&normal.unitCount==8&&Progress.Get("fishing_used")==3&&Progress.Get("fishing_limit")==13&&Progress.Get("fishing_ready_legend")==day+12,"legacy pending reward spends one original bait and one old-night attempt exactly once");
        Fishing.TryClaim();Check(normal.unitCount==8&&emporium.afterhourInventory.childItems.Count==1,"repeat claim neither duplicates catch nor charges bait twice");
        Fishing.Cast();Check(normal.unitCount==7&&Progress.Get("fishing_used")==4,"new cast pays even while ground inventory is full");
        float now=0.35f;for(int n=0;n<500&&(bool)Field(typeof(Fishing),"active").GetValue(null);n++){now+=0.05f;Fishing.Advance((float)Field(typeof(Fishing),"tension").GetValue(null)<Fishing.TargetCenter(1,now-0.35f),now);}
        Check(Progress.Get("fishing_pending")==2&&Progress.Get("fishing_pending_payment")==0&&normal.unitCount==7&&Progress.Get("fishing_used")==4,"tracking the moving green zone lands an ordinary fish and full ground retains its already-paid reward");
        capacity=2;Fishing.TryClaim();Check(Progress.Get("fishing_pending")==0&&normal.unitCount==7&&Progress.Get("fishing_used")==4,"claiming a new prepaid reward consumes no second bait or attempt");
        Progress.Set("fishing_pending",3);Progress.Set("fishing_pending_roe",3);Progress.Set("fishing_pending_sex",1);capacity=3;Fishing.TryClaim();var tuna=emporium.afterhourInventory.childItems.Single(i=>i.identifier=="pelican_tuna");
        Check(Progress.Get("fishing_pending")==0&&Progress.Get("fishing_pending_roe")==3&&normal.unitCount==7&&tuna.GetTagReadonly("FISH_AGE_TAG").GetInt()==14&&tuna.GetTagReadonly("FISH_SEX_TAG").GetString()=="FEMALE","partial egg claim preserves fish age and sex without charging extra bait");
        int used=Progress.Get("fishing_used");Fishing.Cast();Check(!(bool)Field(typeof(Fishing),"active").GetValue(null)&&Progress.Get("fishing_used")==used,"pending eggs block further paid casts");capacity=4;Fishing.TryClaim();Check(Progress.Get("fishing_pending_roe")==0&&normal.unitCount==7,"remaining eggs are delivered once without extra bait");
        Detach(premiumBait);Fishing.SelectBait(true);Fishing.Cast();Check(!(bool)Field(typeof(Fishing),"active").GetValue(null)&&normal.unitCount==7&&Progress.Get("fishing_used")==used,"missing selected premium bait never silently spends ordinary bait");
        premiumBait.SetUnitCount(1);pouch.StorageEdges=new List<GraphNodeStorage>{NewGrid(premiumBait)};Fishing.Cast();Check((bool)Field(typeof(Fishing),"active").GetValue(null)&&Fishing.CountBait(true)==0&&Progress.Get("fishing_used")==used+1,"the final selected bait is destroyed at cast while its paid fight remains active");
        Fishing.Advance(false,0.4f);Check((bool)Field(typeof(Fishing),"active").GetValue(null),"a fight continues after its last bait was consumed at cast");Field(typeof(Fishing),"choice").SetValue(null,5);AccessTools.Method(typeof(Fishing),"Complete").Invoke(null,new object[]{false});
        Check(Progress.Get("fishing_used")==used+1&&Fishing.CountBait(true)==0&&Progress.Get("fishing_ready_crimsonfish")==0,"escaped king spends only the cast payment and starts no cooldown");
        normal.SetUnitCount(2);var extraStack=Owned(Expansion.Product(8));extraStack.SetUnitCount(1);((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(bagGrid)).Add(extraStack);extraStack.parentInventory=bagGrid;premiumBait.SetUnitCount(3);pouch.StorageEdges=new List<GraphNodeStorage>{NewGrid(premiumBait)};
        day++;capacity=100;emporium.afterhourInventory=NewGrid();Fishing.Arrive();Check(Fishing.Remaining==6,"next night snapshots ordinary stacks and nested premium stack exactly");
        for(int n=0;n<6;n++){Fishing.SelectBait(n>=3);Fishing.Cast();Check((bool)Field(typeof(Fishing),"active").GetValue(null),"each brought bait permits one actual cast across multiple stacks");Field(typeof(Fishing),"choice").SetValue(null,0);AccessTools.Method(typeof(Fishing),"Complete").Invoke(null,new object[]{n%2==0});}
        Check(Fishing.CountBait(false)==0&&Fishing.CountBait(true)==0&&Progress.Get("fishing_used")==6&&Fishing.Remaining==0&&emporium.afterhourInventory.childItems.Count==3,"six casts with alternating wins and losses spend exactly six original bait and create only three fish");
        var added=Owned(Expansion.Product(8));((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(bagGrid)).Add(added);added.parentInventory=bagGrid;Fishing.SelectBait(false);Fishing.Arrive();Fishing.Cast();Check(!(bool)Field(typeof(Fishing),"active").GetValue(null)&&Fishing.Remaining==0,"adding bait and reopening the location cannot bypass an exhausted quota");
        foreach(string king in Fishing.Kings){var item=ExpansionStock.King(king);Check(!AquacultureHelper.IsFish(item)&&!FoodItemHelper.IsMeatFood(item)&&!item.IsTag("CALORIE_VALUE_TAG"),"king remains collectible and cannot farm or feed: "+king);}capacity=100;
    }
    private static int fishingConfigLevel=FishingDifficulty.Default;
    public static bool FishingLevelFixture(ref int __result){__result=fishingConfigLevel;return false;}
    private static void FishingDifficultyChecks()
    {
        Check(FishingDifficulty.Default==3&&FishingDifficulty.Current==3&&FishingDifficulty.Clamp(-1)==1&&FishingDifficulty.Clamp(9)==5,"night fishing has five clamped difficulty levels and defaults to level three");
        Check(Fishing.ZoneWidth(0)==0.16f&&Fishing.ZoneWidth(4)==0.10f&&Fishing.ZoneWidth(5)==0.13f&&Fishing.TargetSpeed(0)==0.42f&&Fishing.TargetSpeed(4)==0.75f,"default ordinary and king targets are narrow and move fifty percent faster than r6");
        for(int fish=0;fish<9;fish++)
        {
            for(int level=1;level<=5;level++)
            {
                float width=Fishing.ZoneWidth(fish,level),speed=Fishing.TargetSpeed(fish,level);
                Check(Fishing.CursorSpeed(fish,level)-0.06f>(1-width)*speed,"cursor can catch the fastest target drift in both directions including adverse pull: fish="+fish+",level="+level);
                if(level>1)Check(width<Fishing.ZoneWidth(fish,level-1)&&speed>Fishing.TargetSpeed(fish,level-1),"each higher difficulty narrows the target and increases movement speed");
                bool bounded=true;for(int n=0;n<800;n++){float center=Fishing.TargetCenter(fish,n/10f,level);bounded&=center-width/2>=-0.0001f&&center+width/2<=1.0001f;}
                Check(bounded,"difficulty target remains wholly inside the track: fish="+fish+",level="+level);
                foreach(int fps in new[]{30,60,144})
                {
                    float cursorPosition=0.5f,progress=0,extreme=0;float step=1f/fps;
                    for(int frame=1;frame<45*fps&&progress<1&&extreme<0.8f;frame++)
                    {
                        float elapsed=frame*step;bool hold=cursorPosition<Fishing.TargetCenter(fish,elapsed,level);
                        cursorPosition=Fishing.Pull(cursorPosition,hold,elapsed,step,fish,level);
                        progress=Fishing.ReelProgress(progress,cursorPosition,step,fish,elapsed,level);
                        extreme=cursorPosition<0.02f||cursorPosition>0.98f?extreme+step:0;
                    }
                    Check(progress>=1&&extreme<0.8f,"actual cursor and moving-target physics permit a catch before timeout at "+fps+" fps: fish="+fish+",level="+level+",progress="+progress);
                }
            }
        }
    }
    private static void FishDescriptionChecks(string localePath)
    {
        var previous=new Dictionary<string,string>(locale);
        try
        {
            foreach(string language in new[]{"zh","en"})
            {
                locale.Clear();foreach(string line in File.ReadAllLines(Path.Combine(localePath,language+".csv")))
                {
                    int comma=line.IndexOf(',');if(comma<=0)continue;string value=line.Substring(comma+1).Trim();
                    if(value.StartsWith("\"")&&value.EndsWith("\""))value=value.Substring(1,value.Length-2).Replace("\"\"","\"");locale[line.Substring(0,comma)]=value.Replace("\\n","\n");
                }
                ExpansionStock.Register();
                foreach(string key in ExpansionStock.FishKeys)
                {
                    string flavorKey="v2_"+key+"_flavor";Check(locale.TryGetValue(flavorKey,out string flavor)&&!string.IsNullOrWhiteSpace(flavor),"new reservoir species has translated flavor: "+language+"/"+key);
                    Check(ExpansionStock.Fish[key].GetFlavorText()==flavor,"native FishSpecies flavor entry is populated for the current locale: "+language+"/"+key);
                    for(int kind=0;kind<3;kind++)
                    {
                        string stage=key+(kind==1?"_roe":kind==2?"_fry":""),descriptionKey="v2_"+stage+"_desc";
                        Check(locale.TryGetValue(descriptionKey,out string description)&&!string.IsNullOrWhiteSpace(description),"fish/roe/fry has nonempty translated description: "+language+"/"+stage);
                        Check(!System.Text.RegularExpressions.Regex.IsMatch(description+flavor,"[0-9]|v2_|pelican_|千卡|kcal|机制|implementation",System.Text.RegularExpressions.RegexOptions.IgnoreCase),"fish prose contains narrative rather than internal identifiers or numeric mechanics: "+language+"/"+stage);
                        var item=Owned(ExpansionStock.CreateFish(key,kind));Check(item.shortDescription==description&&item.flavorText==flavor,"fresh native fish factory displays its stage description and flavor: "+language+"/"+stage);
                        int age=kind==0?17:kind==1?1:3;Crops.SetInt(item,"FISH_AGE_TAG",age);FarmSpecies.SetFloat(item,"FISH_HUNGER_TAG",123);FarmSpecies.SetFloat(item,"FISH_HEALTH_TAG",67);FarmSpecies.SetFloat(item,"FISH_KCAL_EATEN_TAG",789);FarmSpecies.SetString(item,"FISH_SEX_TAG","FEMALE");FarmSpecies.SetFloat(item,"ANIMAL_FERTILITY_TAG_ALLELE_A",1.75f);
                        item.shortDescription="";item.flavorText="old blank-description save";var loaded=RoundTrip(item);
                        Check(loaded.shortDescription==description&&loaded.flavorText==flavor,"actual old save reload receives both missing fish texts: "+language+"/"+stage);
                        ExpansionStock.Refresh(loaded);ExpansionStock.Refresh(loaded);
                        Check(loaded.GetTagReadonly("FISH_AGE_TAG").GetInt()==age&&loaded.GetTagReadonly("FISH_HUNGER_TAG").GetFloat()==123&&loaded.GetTagReadonly("FISH_HEALTH_TAG").GetFloat()==67&&loaded.GetTagReadonly("FISH_KCAL_EATEN_TAG").GetFloat()==789&&loaded.GetTagReadonly("FISH_SEX_TAG").GetString()=="FEMALE"&&loaded.GetTagReadonly("ANIMAL_FERTILITY_TAG_ALLELE_A").GetFloat()==1.75f,"description repair and repeated refresh preserve age, hunger, health, feeding, sex and inherited state: "+language+"/"+stage);
                        if(kind==0)
                        {
                            Crops.SetInt(loaded,"FISH_AGE_TAG",ExpansionStock.Fish[key].stageStartDays[4]);loaded.shortDescription="";loaded.flavorText="";AquacultureHelper.UpdateNameAndSprite(loaded);
                            Check(loaded.shortDescription==description&&loaded.flavorText==flavor&&loaded.GetTagReadonly("FISH_HUNGER_TAG").GetFloat()==123,"native growth-stage refresh retains the new narrative and current hunger: "+language+"/"+key);
                        }
                    }
                }
            }
        }
        finally{locale.Clear();foreach(var pair in previous)locale[pair.Key]=pair.Value;ExpansionStock.Register();}
    }
    public static string Run(string localePath,string artifactPath)
    {
        checks=0;bool enabled=ModEntry.Enabled;ModEntry.Enabled=true;ExpansionStock.Register();FarmSpecies.Register();
        var masterField=Field(typeof(GameMaster),"<current>k__BackingField");object oldMaster=masterField.GetValue(null);masterField.SetValue(null,FormatterServices.GetUninitializedObject(typeof(GameMaster)));
        store=(PlayerStore)FormatterServices.GetUninitializedObject(typeof(PlayerStore));emporium=(EmporiumEntry)FormatterServices.GetUninitializedObject(typeof(EmporiumEntry));var h=new Harmony("pelican_cultivation.v2_fixture");
        try
        {
            Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"Ready");Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Day"),"Day");Patch(h,AccessTools.Method(typeof(Progress),"Get"),"Get");Patch(h,AccessTools.Method(typeof(Progress),"Set"),"Set");
            Patch(h,AccessTools.PropertyGetter(typeof(PlayerStore),"Instance"),"Store");Patch(h,AccessTools.PropertyGetter(typeof(EmporiumEntry),"Instance"),"Emporium");Patch(h,AccessTools.Method(typeof(StoreClientInstance),"GetClientBlueprint"),"Client");Patch(h,AccessTools.Method(typeof(PlayerStore),"GetCash"),"Cash");Patch(h,AccessTools.Method(typeof(PlayerStore),"RefreshCounterItem"),"NoOp");
            Patch(h,AccessTools.Method(typeof(ModHelper),"AddCredit"),"Credit");Patch(h,AccessTools.Method(typeof(ModHelper),"GetCurrentAfterhourLocationId"),"Location");Patch(h,AccessTools.Method(typeof(ModEntry),"Message"),"NoOp");
            Patch(h,AccessTools.Method(typeof(ItemDirectory),"CreateEmptyItem",new[]{typeof(string)}),"Empty");Patch(h,AccessTools.Method(typeof(GameItem),"SetSpriteAndShape",new[]{typeof(string),typeof(string)}),"Sprite");Patch(h,AccessTools.Method(typeof(Crops),"Sprite"),"CropSprite");Patch(h,AccessTools.Method(typeof(DirectoryMaster),"Item",new[]{typeof(string),typeof(bool)}),"Factory");
            Patch(h,AccessTools.Method(typeof(WineHelper),"InitWineBottle"),"BottleUiFixture");
            Patch(h,AccessTools.Method(typeof(GraphUtils),"TryAcceptAll"),"Accept");Patch(h,AccessTools.Method(typeof(GraphUtils),"CanAccept"),"CanAccept");Patch(h,AccessTools.Method(typeof(GameInventory),"GetParentItem"),"Host");Patch(h,AccessTools.Method(typeof(AnimalCage),"GetCageInventory"),"Cage");Patch(h,AccessTools.Method(typeof(HusbandryHelper),"GetReachableDispensers"),"Reachable");
            Patch(h,AccessTools.Method(typeof(WaterHelper),"GetCurrentCapacityML"),"Water");Patch(h,AccessTools.Method(typeof(WaterHelper),"GetContaminantPercentage"),"Purity");Patch(h,AccessTools.Method(typeof(WaterHelper),"RemoveMl"),"Drain");
            foreach(string name in new[]{"GetLocalizedMechanic","GetLocalizedItem"})Patch(h,AccessTools.Method(typeof(LocHelper),name),"Text");Patch(h,AccessTools.Method(typeof(LocHelper),"GetLocalizedMechanicOrDefault"),"English");
            Patch(h,AccessTools.Method(typeof(ReservoirEvents),"EnsureToday"),"NoOp");Patch(h,AccessTools.PropertyGetter(typeof(ReservoirEvents),"Multiplier"),"NoKingActivity");
            Crafting();BarrelChecks();DietChecks();GeneticsChecks();ScentChecks();FishingChecks();ProductionChecks(h);RevisionChecks(h);ReservoirChecks(h);BookChecks(h,localePath,artifactPath);RevisionThreeChecks(h);RevisionFourChecks(h);SeafoodChecks();FishDescriptionChecks(localePath);
            return "Passed "+checks+" v2 checks: native recipes/resource conservation/output rollback, barrel staged reward, plant-only feed, two-parent egg inheritance/incubation, hen/goat production, exact fish-mother costs/age gates, perfume upgrades/charges, fishing rates/timing/cash/merge/pending/cooldowns, dedicated seafood dishes and 57-item catalog, native handbooks' locales/pagination/unlock gates. Unity scene rendering and physical placement remain fixtures; no player save opened.";
        }
        finally{h.UnpatchAll(h.Id);masterField.SetValue(null,oldMaster);ModEntry.Enabled=enabled;Fishing.Leave();saved.Clear();hosts.Clear();cages.Clear();}
    }
}
