using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using PelicanCultivation;
using WaterSellerPack;

public static class NeonIntegration
{
    private static string gameManaged,pelican,neonDirectory,fixtureDirectory;
    private static bool expectBaselineFailure;
    private static int checks,day=50,acceptCalls,rejectAcceptAt;
    private static readonly Dictionary<string,int> data=new Dictionary<string,int>();
    private static readonly List<Exception> conversionErrors=new List<Exception>();
    private sealed class Item : GameItem
    {
        public Item(string id):base(id){modifiedState=state;SetShape(new GridShapeBuilder().SetData(new byte[]{1},1));}
        public override void Validate(){modifiedState=state;}
        public override void Destroy(){unitCount=0;if(parentInventory is GameGridInventory grid)Items(grid).Remove(this);parentInventory=null;}
    }
    private sealed class Grid : GameGridInventory
    {
        public Grid():base(1,1){} public override void Validate(){}
        public override bool Expel(GameItem item){bool removed=Items(this).Remove(item);if(removed)item.parentInventory=null;return removed;}
        public override bool UncheckedAccept(GameItem item){if(!Items(this).Contains(item))Items(this).Add(item);item.parentInventory=this;return true;}
        public override SlotMarker TryInventorySlot(GameItem item,int requestedNum,UnityEngine.Vector2 center,GridShape shape,TagSystem state)
            =>new SlotMarker{item=item,inventory=this,itemGridShape=shape,numTransfer=1};
    }
    private static List<GameItem> Items(GameGridInventory grid)=>(List<GameItem>)AccessTools.Field(typeof(GameGridInventory),"items").GetValue(grid);
    private static GameGridInventory NewGrid(int w,int h,params GameItem[] items)
    {
        var grid=(Grid)FormatterServices.GetUninitializedObject(typeof(Grid));
        AccessTools.Field(typeof(GameGridInventory),"items").SetValue(grid,new List<GameItem>(items));
        AccessTools.Field(typeof(GameGridInventory),"<inventoryShape>k__BackingField").SetValue(grid,new GridShapeBuilder().SetData(new byte[w*h],w).Build());
        AccessTools.Field(typeof(TagSystem),"<dict>k__BackingField").SetValue(grid,new Dictionary<string,TagState>());
        foreach(var item in items)item.parentInventory=grid;return grid;
    }
    private static Item Fry(FishSpecies species,int count)
    {
        var item=new Item(species.fryIdentifier??"fixture_fry");AquacultureHelper.InitFryItem(item,species);Crops.SetInt(item,"FISH_GROUP_COUNT_TAG",count);
        GeneralHelper.SetItemOwned(item,true);return item;
    }
    private static Item Roe(FishSpecies species,int count)
    {
        var item=new Item(species.roeIdentifier);AquacultureHelper.InitRoeItem(item,species);item.EnableTag("FISH_FERTILIZED_TAG");Crops.SetInt(item,"FISH_GROUP_COUNT_TAG",count);GeneralHelper.SetItemOwned(item,true);return item;
    }
    public static bool Factory(string identifier,ref GameItem __result)
    {
        foreach(var species in FishSpecies.All.Values)
        {
            if(identifier==species.fryIdentifier){__result=Fry(species,1);return false;}
            if(identifier==species.fishIdentifier){var item=new Item(identifier);AquacultureHelper.InitFishItem(item,species,"MALE");GeneralHelper.SetItemOwned(item,true);__result=item;return false;}
        }
        throw new Exception("Unknown peer fixture factory: "+identifier);
    }
    public static bool Text(string key,ref string __result){__result=key;return false;}
    public static bool ModText(string key,ref string __result){__result=key;return false;}
    public static bool NoOp()=>false;
    public static bool NoRoll(ref bool __result){__result=false;return false;}
    public static bool Day(ref int __result){__result=day;return false;}
    public static bool Ready(ref bool __result){__result=true;return false;}
    public static bool Get(string key,int fallback,ref int __result){__result=data.TryGetValue(key,out int value)?value:fallback;return false;}
    public static bool Set(string key,int value){data[key]=value;return false;}
    public static bool Error(string context,Exception e){conversionErrors.Add(e);return false;}
    public static bool Geometry(GameItem item,string key,ref bool __result)
    {if(FarmGeometry.Shape(key,out int w,out int h))Crops.BindSprite(item,key,w,h);__result=true;return false;}
    public static bool Sprite(GameItem __instance,string spriteAtlasPath,string spritePath,ref GameItem __result)
    {__instance.SetSprite(spriteAtlasPath,spritePath);__result=__instance;return false;}
    public static bool Accept(SlotMarker __instance,ref int __result)
    {
        acceptCalls++;if(rejectAcceptAt>0 && acceptCalls==rejectAcceptAt){__result=0;return false;}
        var grid=(Grid)__instance.inventory;__instance.item.modifiedShape=new GridShapeBuilder(__instance.itemGridShape);grid.UncheckedAccept(__instance.item);__result=1;return false;
    }
    private static void Fixture(Harmony h,MethodBase method,string prefix)
    {if(method==null)throw new Exception("Missing fixture target: "+prefix);h.Patch(method,new HarmonyMethod(typeof(NeonIntegration).GetMethod(prefix)));}
    private static void InstallFixtures(Harmony h)
    {
        Fixture(h,AccessTools.Method(typeof(DirectoryMaster),"Item",new[]{typeof(string),typeof(bool)}),"Factory");
        foreach(string name in new[]{"GetLocalizedItem","GetLocalizedMechanic","GetLocalizedUI"})Fixture(h,AccessTools.Method(typeof(LocHelper),name),"Text");
        Fixture(h,AccessTools.Method(typeof(ModEntry),"L"),"ModText");Fixture(h,AccessTools.Method(typeof(ModEntry),"Error"),"Error");
        Fixture(h,AccessTools.Method(typeof(GameItem),"SetSpriteAndShape",new[]{typeof(string),typeof(string)}),"Sprite");
        Fixture(h,AccessTools.Method(typeof(FarmGeometry),"Apply"),"Geometry");Fixture(h,AccessTools.Method(typeof(SlotMarker),"TryAcceptOnce"),"Accept");
        Fixture(h,AccessTools.Method(typeof(StoreStation),"GetDayCounter"),"Day");Fixture(h,AccessTools.PropertyGetter(typeof(Progress),"Day"),"Day");
        Fixture(h,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"Ready");Fixture(h,AccessTools.Method(typeof(Progress),"Get"),"Get");Fixture(h,AccessTools.Method(typeof(Progress),"Set"),"Set");
        foreach(var method in typeof(RNG).GetMethods().Where(m=>m.Name=="Roll"))Fixture(h,method,"NoRoll");
        // Scene movement, food/water placement and audio are engine boundaries.
        foreach(string name in new[]{"MoveAround","Poop"})Fixture(h,AccessTools.Method(typeof(HusbandryHelper),name),"NoOp");
        foreach(string name in new[]{"ApplyWaterChecks","WanderStock"})Fixture(h,AccessTools.Method(typeof(AquacultureHelper),name),"NoOp");
        Fixture(h,AccessTools.Method(typeof(UnityEngine.Debug),"Log",new[]{typeof(object)}),"NoOp");
    }
    private static void Check(bool value,string why){if(!value)throw new Exception("NEON FAIL: "+why);checks++;}
    private static void InstallPelican(Harmony harmony)
    {
        foreach(var type in typeof(ModEntry).Assembly.GetTypes())
            if(type.IsDefined(typeof(HarmonyPatch),false) && type!=typeof(NightReportScrollPatch))harmony.CreateClassProcessor(type).Patch();
    }
    private static void InstallCohort(Harmony harmony)
    {
        foreach(string name in new[]{"HatchRoe","GrowFry"})harmony.Patch(AccessTools.Method(typeof(AquacultureHelper),name),
            new HarmonyMethod(typeof(NeonFish),"BeforeCohort"),new HarmonyMethod(typeof(NeonFish),name=="HatchRoe"?"Hatched":"Grown"));
    }
    private static void InstallNeon(Harmony harmony)
    {
        InstallCohort(harmony);NeonRatMilk.Patch(harmony);PetBreeding.Patch(harmony);
        foreach(var binding in new[]{new[]{"GetGrowthSpeed","Growth"},new[]{"GetStockDrain","Drain"}})
            harmony.Patch(AccessTools.Method(typeof(AquacultureHelper),binding[0]),postfix:new HarmonyMethod(typeof(NeonFish),binding[1]));
        harmony.Patch(AccessTools.Method(typeof(AquacultureHelper),"AgeOneDay"),prefix:new HarmonyMethod(typeof(NeonFish),"Age"));
        harmony.Patch(AccessTools.Method(typeof(AquacultureHelper),"InitRoeItem"),postfix:new HarmonyMethod(typeof(NeonFish),"InitGroup"));
        harmony.Patch(AccessTools.Method(typeof(AquacultureHelper),"InitFryItem"),postfix:new HarmonyMethod(typeof(NeonFish),"InitGroup"));
        NeonFishBreeding.Start(harmony);
    }
    private static void SeedBrood(Item source,int count,bool male=false)
    {
        var genes=new Dictionary<string,int>();foreach(string gene in FishCatalogue.Genes)foreach(string side in new[]{"_M_A","_M_B","_P_A","_P_B"})genes[gene+side]=Math.Min(2,FishCatalogue.Max(gene));
        NeonFish.CopyPool(source,genes);FarmSpecies.SetString(source,"NU_FISH_BROOD_V1",FishBroodRules.Create(genes,count,()=>male));
        FarmSpecies.SetString(source,"FOREIGN_SAVE_DATA","preserve-me");FarmSpecies.SetFloat(source,"FISH_HUNGER_TAG",400);FarmSpecies.SetFloat(source,"FISH_KCAL_EATEN_TAG",1000);FarmSpecies.SetFloat(source,"FISH_KCAL_GROWN_TAG",600);
    }
    private static void Convert(Item source,FishSpecies species,GameGridInventory tank,bool hatch,Dictionary<GameItem,float> gain=null)
        =>AccessTools.Method(typeof(AquacultureHelper),hatch?"HatchRoe":"GrowFry").Invoke(null,hatch?new object[]{source,species,tank}:new object[]{source,species,tank,gain??new Dictionary<GameItem,float>()});
    private static void CohortChecks()
    {
        foreach(var species in NeonFish.ShelfSpecies.Concat(new[]{FarmSpecies.Lobsters,FarmSpecies.SeaCucumbers}))
        {
            var source=Fry(species,2);SeedBrood(source,2);var full=NewGrid(1,1,source);string dna=NeonFish.Dna(source);Convert(source,species,full,false);
            Check(source.IsTag("PELICAN_FARM_TRANSITION_PENDING_BOOL") && source.unitCount==1 && full.childItems.Single()==source && NeonFish.Dna(source)==dna,"full tank pauses without null-state errors, losses or rerolled brood DNA: "+species.speciesID);
            var tank=NewGrid(20,20,source);var gains=new Dictionary<GameItem,float>();acceptCalls=0;rejectAcceptAt=2;Convert(source,species,tank,false,gains);rejectAcceptAt=0;
            Check(source.unitCount==1 && tank.childItems.Single()==source && source.parentInventory==tank && gains.Count==0,"partial placement rollback restores the same cohort before Neon postprocessing: "+species.speciesID);
            conversionErrors.Clear();Convert(source,species,tank,false,gains);
            Check(conversionErrors.Count==0 && source.unitCount==0 && tank.childItems.Count==2 && gains.Count==2,"both mods commit a complete two-fish transformation: "+species.speciesID);
            Check(tank.childItems.All(i=>NeonFish.Colour(i)==2 && NeonFish.Pattern(i)==2 && i.GetTagReadonly("FISH_SEX_TAG").GetString()=="FEMALE"),"original Neon DNA/sex inheritance survives the safe replacement: "+species.speciesID);
            Check(tank.childItems.All(i=>i.GetTagReadonly("FOREIGN_SAVE_DATA").GetString()=="preserve-me" && i.GetTagReadonly("NU_PET_BRED_V1").GetInt()==1),"foreign metadata and original Neon offspring traits survive: "+species.speciesID);
            Check(Math.Abs(tank.childItems.Sum(AquacultureHelper.GetHunger)-400)<0.001 && Math.Abs(tank.childItems.Sum(AquacultureHelper.GetKcalEaten)-1000)<0.001,"cohort food reserves and eaten calories are conserved: "+species.speciesID);
            var eggs=Roe(species,2);SeedBrood(eggs,2);var hatchTank=NewGrid(20,20,eggs);dna=NeonFish.Dna(eggs);Convert(eggs,species,hatchTank,true);
            Check(eggs.unitCount==0 && hatchTank.childItems.Count==1 && hatchTank.childItems[0].IsTag("FISH_FRY_TAG") && hatchTank.childItems[0].GetTagReadonly("FISH_GROUP_COUNT_TAG").GetInt()==2,"both mods hatch one grouped cohort without changing its count: "+species.speciesID);
            Check(NeonFish.Dna(hatchTank.childItems[0])==dna && PetBreeding.Clutch(hatchTank.childItems[0]).Length>0,"actual Neon hatch postfix retains the complete brood/trait pool: "+species.speciesID);
        }
        foreach(bool male in new[]{false,true})
        {
            data.Clear();var source=Fry(FarmSpecies.SeaCucumbers,2);SeedBrood(source,2,male);Crops.SetInt(source,SeaFounderPair.BatchTag,1);var tank=NewGrid(20,20,source);Convert(source,FarmSpecies.SeaCucumbers,tank,false);
            Check(tank.childItems[0].GetTagReadonly("FISH_SEX_TAG").GetString()=="MALE" && tank.childItems[1].GetTagReadonly("FISH_SEX_TAG").GetString()=="FEMALE","first purchased sea-cucumber pair remains male/female after Neon inheritance: "+male);
        }
    }
    private static void NightChecks()
    {
        Check(typeof(ModEntry).Assembly.GetReferencedAssemblies().All(a=>!a.Name.StartsWith("WaterSeller")),"the runtime remains loadable without installing Neon as a required assembly");
        foreach(var species in new[]{AnimalSpecies.Rat,FarmSpecies.Cattle,FarmSpecies.Pigs})
        {
            var animal=new Item(species==AnimalSpecies.Rat?"rat":species==FarmSpecies.Cattle?"pelican_cow":"pelican_pig");HusbandryHelper.InitAnimalItem(animal,species,"FEMALE");GeneralHelper.SetItemOwned(animal,true);
            Crops.SetInt(animal,"ANIMAL_AGE_TAG",species.growthStages[3]);FarmSpecies.SetFloat(animal,"ANIMAL_HUNGER_TAG",HusbandryHelper.GetMaxHunger(animal));var cage=NewGrid(20,20,animal);
            int age=animal.GetTagReadonly("ANIMAL_AGE_TAG").GetInt();animal.onCycleEndSlotItemFunc(animal,cage,null);
            Check(animal.GetTagReadonly("ANIMAL_AGE_TAG").GetInt()==age+1 && animal.unitCount==1,"actual native mammal night callback completes with both mods: "+species.speciesID);
        }
        var mother=new Item("rat");HusbandryHelper.InitAnimalItem(mother,AnimalSpecies.Rat,"FEMALE");NeonRats.SetPair(mother,"NU_RAT_MILK_LINE",1,1);
        Crops.SetInt(mother,"ANIMAL_AGE_TAG",AnimalSpecies.Rat.growthStages[3]);Crops.SetInt(mother,"NU_RAT_NURSING_AGE",AnimalSpecies.Rat.growthStages[3]);Crops.SetInt(mother,"NU_RAT_MILK_DAY",day-1);Crops.SetInt(mother,"NU_RAT_MILK_UL",0);
        FarmSpecies.SetFloat(mother,"ANIMAL_HUNGER_TAG",HusbandryHelper.GetMaxHunger(mother));var nursingCage=NewGrid(20,20,mother);mother.onCycleEndSlotItemFunc(mother,nursingCage,null);
        Check(mother.GetTagReadonly("NU_RAT_MILK_DAY").GetInt()==day && NeonRatMilk.Available(mother)>0,"original Neon rat-milk production still runs after the native nightly animal callback");
        foreach(var species in NeonFish.ShelfSpecies.Concat(new[]{FarmSpecies.Lobsters,FarmSpecies.SeaCucumbers}))foreach(bool hatch in new[]{false,true})
        {
            var source=hatch?Roe(species,2):Fry(species,2);SeedBrood(source,2);Crops.SetInt(source,"FISH_AGE_TAG",species.stageStartDays[hatch?1:2]-1);FarmSpecies.SetFloat(source,"FISH_HUNGER_TAG",species.maxHunger);
            var tank=NewGrid(20,20,source);AquacultureHelper.TickTank(new Item("ras"),tank,null,new List<GameSlotInventory>(),null,true);
            Check(source.unitCount==0 && tank.childItems.Count==(hatch?1:2),"actual native tank night reaches and completes the joint stage transition: "+species.speciesID+"/"+hatch);
        }
    }
    private static void ForecastChecks()
    {
        foreach(var species in NeonFish.ShelfSpecies.Concat(new[]{FarmSpecies.Lobsters,FarmSpecies.SeaCucumbers}))foreach(bool industrial in new[]{false,true})
        {
            var fish=new Item(species.fishIdentifier);AquacultureHelper.InitFishItem(fish,species,"FEMALE");GeneralHelper.SetItemOwned(fish,true);NeonFish.Pair(fish,"GROWTH",2,2);
            FarmSpecies.SetFloat(fish,"FISH_INDUSTRIAL_DIET_TAG",1);FarmSpecies.SetFloat(fish,"FISH_MEAT_DIET_TAG",0);FarmSpecies.SetFloat(fish,"FISH_RECENT_KCAL_TAG",100);FarmSpecies.SetFloat(fish,"FISH_RECENT_INDUSTRIAL_TAG",industrial?100:0);FarmSpecies.SetFloat(fish,"FISH_RECENT_MEAT_TAG",industrial?0:100);
            var next=new Item(species.fishIdentifier);foreach(var tag in fish.state.dict)next.state.dict[tag.Key]=tag.Value.Clone();next.modifiedState=next.state;
            AccessTools.Method(typeof(AquacultureHelper),"UpdateRecentDiet").Invoke(null,new object[]{next});float expected=AquacultureHelper.GetGrowthSpeed(next);
            Check(Math.Abs(FarmNightReport.ProjectedGrowthSpeed(fish,species)-expected)<0.0001,"forecast retains actual Neon growth genes after the pending native diet update: "+species.speciesID+"/"+industrial);
            var demand=AccessTools.Method(typeof(FarmFeedForecast),"Demand").Invoke(null,new object[]{fish});double speed=(double)AccessTools.Field(demand.GetType(),"Speed").GetValue(demand);
            Check(Math.Abs(speed-expected)<0.0001 && fish.GetTagReadonly("FISH_RECENT_KCAL_TAG").GetFloat()==100,"feed projection uses the same genetic growth speed without mutating pending diet data: "+species.speciesID+"/"+industrial);
        }
    }
    public static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("MONOMOD_DMDType","cecil");
        gameManaged=Path.Combine(args[0],"Probably Stolen_Data/Managed");pelican=args[1];neonDirectory=args[2];expectBaselineFailure=args.Length>3 && args[3]=="expect-baseline-failure";
        fixtureDirectory=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"EngineFixture");
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>{
            string name=new AssemblyName(eventArgs.Name).Name;
            string path=name=="PelicanCultivation"?pelican:Path.Combine(gameManaged,name+".dll");
            string fixture=Path.Combine(fixtureDirectory,name+".dll"),peer=Path.Combine(neonDirectory,name+".dll");
            if(File.Exists(fixture))path=fixture;else if(File.Exists(peer))path=peer;
            return File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        return Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run()
    {
        var own=new Harmony("pelican.neon.integration.own");var peer=new Harmony("pelican.neon.integration.peer");var fixture=new Harmony("pelican.neon.integration.fixture");
        ModEntry.Enabled=true;ModEntry.GameplayReady=false;FarmSpecies.Register();
        try
        {
            InstallFixtures(fixture);InstallPelican(own);InstallCohort(peer);
            var source=Fry(FishSpecies.Tilapia,2);var tank=NewGrid(1,1,source);
            try{AccessTools.Method(typeof(AquacultureHelper),"GrowFry").Invoke(null,new object[]{source,FishSpecies.Tilapia,tank,new Dictionary<GameItem,float>()});}
            catch(TargetInvocationException error)
            {
                if(expectBaselineFailure && error.InnerException is NullReferenceException && error.InnerException.StackTrace.Contains("WaterSellerPack.NeonFish.Grown"))
                {Console.WriteLine("REPRODUCED v1.1.9 overnight failure with the original Neon 0.5.3 DLL: skipped BeforeCohort leaves null __state in NeonFish.Grown. Original fry count and full tank are preserved.");return 0;}
                throw;
            }
            if(expectBaselineFailure)throw new Exception("Expected the unfixed installed version to reproduce the reported failure.");
            Check(source.IsTag("PELICAN_FARM_TRANSITION_PENDING_BOOL") && tank.childItems.Count==1 && tank.childItems[0]==source,"full tank pauses the native cohort conversion without interrupting the remaining night");
            peer.UnpatchAll(peer.Id);own.UnpatchAll(own.Id);
            foreach(bool peerFirst in new[]{false,true})
            {
                if(peerFirst){InstallNeon(peer);InstallPelican(own);}else{InstallPelican(own);InstallNeon(peer);}
                CohortChecks();NightChecks();ForecastChecks();NeonFishBreeding.Stop();peer.UnpatchAll(peer.Id);own.UnpatchAll(own.Id);
            }
            Console.WriteLine("Passed "+checks+" original Neon 0.5.3 DLL integration checks in both patch-registration orders: hatch/fry state, metadata, DNA, rollback, founder pair and native animal/tank nightly callbacks. Scene/render/resource boundaries are fixtures; no user save/config is opened.");return 0;
        }
        catch(Exception error){Console.WriteLine(error);return 1;}
        finally{own.UnpatchAll(own.Id);peer.UnpatchAll(peer.Id);fixture.UnpatchAll(fixture.Id);ModEntry.Enabled=false;}
    }
}
