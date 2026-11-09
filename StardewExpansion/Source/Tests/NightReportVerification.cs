using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using PelicanCultivation;

public static class NightReportVerification
{
    private static int checks;
    private static string localeDirectory;
    private static readonly Dictionary<string,string> locale=new Dictionary<string,string>();
    private static readonly Dictionary<string,int> data=new Dictionary<string,int>();
    private static List<GameItem> owned=new List<GameItem>();
    private static IEnumerable<GameItem> supplies;
    private static PlayerStore store;
    private static bool automatic;
    private static readonly List<GameItem> harvested=new List<GameItem>();
    private static bool wasteAuto=true,onlyLow;
    private static int transferCalls;
    private static EmporiumEntry emporium;
    private static void Check(bool value,string why){if(!value) throw new Exception("NIGHT REPORT FAIL: "+why);checks++;}
    private sealed class Item : GameItem
    {
        public Item(string id,int w=1,int h=1):base(id){modifiedState=state;var cells=new byte[w*h];for(int i=0;i<cells.Length;i++) cells[i]=1;SetShape(new GridShapeBuilder().SetData(cells,w));}
        public override void Validate(){modifiedState=state;}
        public override void Destroy(){unitCount=0;parentInventory=null;}
        public void BindWindow(PixelWindow window){contentWindow=window;}
    }
    public static void Configure(string directory){localeDirectory=directory;}
    private sealed class HeadlessGrid : GameGridInventory
    {public HeadlessGrid():base(1,1){} public override void Validate(){}}
    private static GameGridInventory Grid(int width,int height,params GameItem[] items)
    {
        // Inventory constructors build Unity render objects. Supply only the
        // native storage fields needed for non-rendering inventory operations.
        var grid=(GameGridInventory)FormatterServices.GetUninitializedObject(typeof(HeadlessGrid));
        AccessTools.Field(typeof(GameGridInventory),"items").SetValue(grid,new List<GameItem>(items));
        AccessTools.Field(typeof(GameGridInventory),"<inventoryShape>k__BackingField").SetValue(grid,new GridShapeBuilder().SetData(new byte[width*height],width).Build());
        AccessTools.Field(typeof(TagSystem),"<dict>k__BackingField").SetValue(grid,new Dictionary<string,TagState>());
        foreach(var item in items) item.parentInventory=grid;
        return grid;
    }
    private static void LoadLocale(string language)
    {
        locale.Clear();
        foreach(string line in File.ReadAllLines(Path.Combine(localeDirectory,language+".csv")))
        {
            int comma=line.IndexOf(',');if(comma<=0) continue;
            string value=line.Substring(comma+1);
            if(value.StartsWith("\"") && value.EndsWith("\"")) value=value.Substring(1,value.Length-2).Replace("\"\"","\"");
            locale[line.Substring(0,comma)]=value;
        }
    }
    public static string Text(string key,object[] args)
        => locale.TryGetValue(key,out string value)?string.Format(value,args):key;
    private static Item Animal(string id,int age,string sex="FEMALE",bool ownedItem=true)
    {
        var item=new Item(id);item.EnableTag("ANIMAL_ITEM_TAG");
        FarmSpecies.SetString(item,"ANIMAL_SPECIES_TAG",id);FarmSpecies.SetString(item,"ANIMAL_SEXE_TAG",sex);Crops.SetInt(item,"ANIMAL_AGE_TAG",age);
        FarmSpecies.SetFloat(item,"ANIMAL_HUNGER_TAG",1000);FarmSpecies.SetFloat(item,"ANIMAL_MAX_HUNGER_TAG",1200);FarmSpecies.SetFloat(item,"ANIMAL_HEALTH_TAG",100);
        foreach(string gene in AnimalSpecies.All[id].geneticTraitIDs) GeneticHelper.SetTrait(item,AnimalSpecies.All[id],gene,gene=="ANIMAL_IMMUNITY_TAG"?2:1);
        GeneralHelper.SetItemOwned(item,ownedItem);IngesterHelper.InitIngester(item);return item;
    }
    private static Item Fish(FishSpecies species,int age,int count=1,bool egg=false)
    {
        var item=new Item(species.fishIdentifier);item.EnableTag(count>1 || egg?"FISH_GROUP_TAG":"FISH_ITEM_TAG");
        if(egg){item.EnableTag("FISH_ROE_TAG");item.EnableTag("FISH_FERTILIZED_TAG");}else if(count>1) item.EnableTag("FISH_FRY_TAG");
        FarmSpecies.SetString(item,"FISH_SPECIES_TAG",species.speciesID);Crops.SetInt(item,"FISH_AGE_TAG",age);Crops.SetInt(item,"FISH_GROUP_COUNT_TAG",count);
        FarmSpecies.SetFloat(item,"FISH_HUNGER_TAG",species.maxHunger);FarmSpecies.SetFloat(item,"FISH_HEALTH_TAG",species.maxHealth);
        GeneralHelper.SetItemOwned(item,true);return item;
    }
    public static bool Ready(ref bool __result){__result=true;return false;}
    public static bool Day(ref int __result){__result=42;return false;}
    public static bool Get(string key,int fallback,ref int __result){__result=data.TryGetValue(key,out int value)?value:fallback;return false;}
    public static bool Set(string key,int value){data[key]=value;return false;}
    public static bool Owned(ref List<GameItem> __result){__result=owned;return false;}
    public static bool Store(ref PlayerStore __result){__result=store;return false;}
    public static bool Automatic(ref bool __result){__result=automatic;return false;}
    public static bool Supplies(ref IEnumerable<GameItem> __result){__result=supplies;return false;}
    public static bool NoRoll(ref bool __result){__result=false;return false;}
    public static bool NoLog(){return false;}
    public static bool Empty(string identifier,ref GameItem __result){__result=new Item(identifier);return false;}
    public static bool Sprite(GameItem __instance,ref GameItem __result){__result=__instance;return false;}
    public static bool Harvest(GameItem animal){harvested.Add(animal);return false;}
    public static bool AutoWaste(ref bool __result){__result=wasteAuto;return false;}
    public static bool OnlyLow(ref bool __result){__result=onlyLow;return false;}
    public static bool Emporium(ref EmporiumEntry __result){__result=emporium;return false;}
    public static bool CanTransfer(GraphNodeStorage __0,GameItem __1,ref bool __result)
    {
        var grid=__0 as GameGridInventory;
        __result=grid!=null && grid.inventoryShape.width*grid.inventoryShape.height-grid.childItems.Sum(i=>i.GetItemSize())>=__1.GetItemSize();return false;
    }
    public static bool Transfer(GraphNodeStorage __0,GameItem __1,ref int __result)
    {
        // Simulate only the engine-backed placement boundary. Selection,
        // ownership, robot thresholds, lifecycle and preservation run intact.
        transferCalls++;var old=__1.parentInventory as GameGridInventory;var target=(GameGridInventory)__0;
        if(old!=null) ((List<GameItem>)AccessTools.Field(typeof(GameGridInventory),"items").GetValue(old)).Remove(__1);
        ((List<GameItem>)AccessTools.Field(typeof(GameGridInventory),"items").GetValue(target)).Add(__1);__1.parentInventory=target;__result=0;return false;
    }
    private static void BindPanels(Item item,params GameGridInventory[] grids)
    {
        var panels=new List<MachineryPanel>();foreach(var grid in grids)
        {
            var panel=(MachineryPanel)FormatterServices.GetUninitializedObject(typeof(MachineryPanel));
            AccessTools.Field(typeof(MachineryPanel),"<content>k__BackingField").SetValue(panel,grid);panels.Add(panel);
        }
        var row=(MachineryPanelRow)FormatterServices.GetUninitializedObject(typeof(MachineryPanelRow));AccessTools.Field(typeof(MachineryPanelRow),"panels").SetValue(row,panels);
        var window=(PixelWindow)FormatterServices.GetUninitializedObject(typeof(PixelWindow));AccessTools.Field(typeof(PixelWindow),"<childElement>k__BackingField").SetValue(window,row);item.BindWindow(window);
    }
    private static Item Feces(int cells=1,bool isOwned=true)
    {var item=new Item("feces_small",cells,1);item.EnableTag("ANIMAL_FECES_TAG");GeneralHelper.SetItemOwned(item,isOwned);return item;}
    private static Item Feeder(int other=0,int meat=0,int fish=0)
    {
        var item=new Item("livestock_feed_dispenser");item.EnableTag("FEED_DISPENSER_TAG");GeneralHelper.SetItemOwned(item,true);
        Crops.SetInt(item,"STORED_OTHER",other);Crops.SetInt(item,"STORED_MEAT",meat);Crops.SetInt(item,"STORED_INDUSTRIAL_FISH_FEED",fish);Crops.SetInt(item,"STORED_KCAL_TAG",other+meat+fish);return item;
    }
    private static void FeedAndWasteChecks()
    {
        int started=checks;LoadLocale("zh");data.Clear();
        var h=new Harmony("pelican_cultivation.feed_waste_verification");
        try
        {
            Patch(h,AccessTools.PropertyGetter(typeof(FarmSettings),"AutoFeces"),"AutoWaste");Patch(h,AccessTools.PropertyGetter(typeof(FarmSettings),"FeedOnlyLow"),"OnlyLow");
            Patch(h,AccessTools.PropertyGetter(typeof(EmporiumEntry),"Instance"),"Emporium");
            Patch(h,AccessTools.Method(typeof(GraphUtils),"CanAccept"),"CanTransfer");Patch(h,AccessTools.Method(typeof(GraphUtils),"TryAcceptAll"),"Transfer");
            var trash=Grid(20,2);emporium=(EmporiumEntry)FormatterServices.GetUninitializedObject(typeof(EmporiumEntry));emporium.trashcanInvElement=trash;
            var liveTrash=new Item("trashcan");BindPanels(liveTrash,trash);emporium.trashcan=liveTrash;
            var feces=Feces();var other=new Item("valuable_food");var cage=Grid(10,3,feces,other,Feces(1,false));
            FarmWaste.Reset();FarmWaste.ServiceCage(cage,trash,80);
            Check(feces.parentInventory==trash && other.parentInventory==cage && cage.childItems.Count==2 && FarmWaste.Moved==1,"without a robot, transfer only owned feces and preserve food/vendor items");
            var untouched=Feces();cage=Grid(10,3,untouched);var machine=new Item("animal_cage");machine.EnableTag("CAGE_TAG");GeneralHelper.SetItemOwned(machine,true);BindPanels(machine,cage,Grid(1,1));
            wasteAuto=false;FarmWaste.Service(new[]{machine});Check(untouched.parentInventory==cage,"disabled feces setting preserves stock for water production");wasteAuto=true;
            emporium.trashcanInvElement=Grid(1,1,new Item("stale_probe_blocker"));
            FarmWaste.Reset();FarmWaste.Service(new[]{machine});
            Check(untouched.parentInventory==trash && !FarmWaste.TrashBlocked,"no-robot cleaning uses the live trashcan even when its factory cache points to a full temporary probe");
            Check(Object.ReferenceEquals(FarmWaste.TrashStorage(liveTrash),trash),"live scene trash storage resolves through the native window graph");
            untouched=Feces();cage=Grid(10,3,untouched);BindPanels(machine,cage,Grid(1,1));emporium.trashcanInvElement=null;
            FarmWaste.Service(new[]{machine});Check(untouched.parentInventory==trash,"a null factory cache does not disable a real trashcan");
            untouched=Feces();cage=Grid(10,3,untouched);BindPanels(machine,cage,Grid(1,1));emporium.trashcanInvElement=trash;emporium.trashcan=null;
            FarmWaste.Service(new[]{machine});Check(untouched.parentInventory==cage,"a cached grid alone cannot substitute for a missing scene trashcan");emporium.trashcan=liveTrash;
            var full=Grid(1,1,new Item("blocker"));FarmWaste.Reset();int before=transferCalls;
            FarmWaste.ServiceCage(cage,full,80);Check(untouched.parentInventory==cage && untouched.unitCount==1 && FarmWaste.TrashBlocked && transferCalls==before,"full trash preserves original feces without calling a destructive fallback");
            FarmWaste.ServiceCage(cage,null,80);Check(untouched.parentInventory==cage,"missing trash destination preserves the source");
            FarmWaste.ServiceCage(cage,trash,80);Check(untouched.parentInventory==trash && !FarmWaste.TrashBlocked,"a successful late retry clears an earlier full-bin warning");untouched=Feces();
            var robot=new Item("vacuum_robot");GeneralHelper.SetItemOwned(robot,true);var stored=Feces(7);var storage=Grid(10,1,stored);BindPanels(robot,Grid(1,1),storage);
            Check(Object.ReferenceEquals(FarmWaste.RobotStorage(robot),storage),"actual native panel lookup finds robot collection storage, not its power slot");
            cage=Grid(10,3,robot,untouched);FarmWaste.Reset();before=transferCalls;FarmWaste.ServiceCage(cage,trash,80);
            Check(stored.parentInventory==storage && untouched.parentInventory==cage && transferCalls==before,"a 70-percent robot keeps collecting instead of dumping every night");
            emporium.trashcanInvElement=null;emporium.trashcan=null;BindPanels(machine,cage,Grid(1,1));FarmWaste.Service(new[]{machine});
            Check(!FarmWaste.TrashBlocked,"a robot below its threshold does not need a trash bin or invent a bin warning");emporium.trashcanInvElement=trash;emporium.trashcan=liveTrash;
            Check(!FarmWaste.ShouldEmpty(storage,80) && FarmWaste.ShouldEmpty(storage,70),"robot fill threshold is configurable and inclusive");
            stored=Feces(8);storage=Grid(10,1,stored);BindPanels(robot,Grid(1,1),storage);FarmWaste.ServiceCage(cage,trash,80);
            Check(stored.parentInventory==trash && untouched.parentInventory==cage && FarmWaste.Moved==1,"at 80 percent, empty the robot while leaving cage collection to it");
            var replenished=Feces(8);storage=Grid(10,1,replenished);BindPanels(robot,Grid(1,1),storage);FarmWaste.ServiceCage(cage,trash,80);
            Check(replenished.parentInventory==storage,"the same robot is successfully emptied at most once per night");
            FarmWaste.Reset();FarmWaste.ServiceCage(cage,full,80);Check(replenished.parentInventory==storage && replenished.unitCount==1,"a near-full robot retains feces when the trash is full");
            var unrelatedRobot=new Item("vacuum_robot");GeneralHelper.SetItemOwned(unrelatedRobot,true);Grid(5,5,unrelatedRobot);
            FarmWaste.Reset();var noRobot=Feces();FarmWaste.ServiceCage(Grid(10,3,noRobot),trash,80);Check(noRobot.parentInventory==trash,"a robot stored elsewhere does not suppress cleaning this cage");
            var rat=Animal(AnimalSpecies.Rat.speciesID,5);double next;
            Check(Math.Abs(FarmFeedForecast.Estimate(new FarmFeedForecast.Pool{Other=650},new[]{rat},out next)-10)<0.0001 && next==65,"rat feed runway matches ten native daily hunger charges");
            Check(Math.Abs(FarmFeedForecast.Estimate(new FarmFeedForecast.Pool{Other=682.5},new[]{rat},out next)-10.5)<0.0001,"fractional remaining nights do not round upward");
            var cow=Animal(FarmRules.Cow,6,"MALE");Check(Math.Abs(FarmFeedForecast.Estimate(new FarmFeedForecast.Pool{Plant=1100},new[]{cow},out next)-10)<0.0001,"male cattle use their own native hunger rate and plant diet");
            var dairy=Animal(FarmRules.Cow,6);double hunger=dairy.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat();
            Check(Math.Abs(FarmFeedForecast.Estimate(new FarmFeedForecast.Pool{Plant=1140},new[]{dairy},out next)-4)<0.0001,"milk's 350-kcal production cycle is included in plant supply planning");
            Check(dairy.GetTagReadonly(FarmRules.ProductClock).GetInt()==0 && dairy.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==hunger,"forecasting never changes real hunger or production progress");
            var pig=Animal(FarmRules.Pig,10,"MALE");Check(Math.Abs(FarmFeedForecast.Estimate(new FarmFeedForecast.Pool{Other=575},new[]{pig},out next)-3)<0.0001,"male-pig truffle cost is included in its native three-night cycle");
            foreach(var species in FishSpecies.All.Values) foreach(int age in new[]{0,1,2,5,14,30,60,3650})
                Check(FarmFeedForecast.WeightAtAge(species,age)==(int)AccessTools.Method(typeof(AquacultureHelper),"GetWeightAtAge").Invoke(null,new object[]{species,age}),"projected weight agrees with native species curve: "+species.speciesID);
            var lobster=Fish(FarmSpecies.Lobsters,14);
            Check(FarmFeedForecast.Estimate(new FarmFeedForecast.Pool{Other=10000,FishFeed=10000},new[]{lobster},out next)==0 && next>0,"industrial feed/plant calories do not count as food for carnivorous lobsters");
            Check(FarmFeedForecast.Estimate(new FarmFeedForecast.Pool{Meat=10000},new[]{lobster},out next)>0,"compatible meat supplies fund a lobster forecast");
            var growing=Fish(FarmSpecies.SeaCucumbers,8);double firstDemand;
            double projected=FarmFeedForecast.Estimate(new FarmFeedForecast.Pool{Other=300},new[]{growing},out firstDemand);
            Check(Math.Abs(projected-300/firstDemand)>0.01,"future aquatic growth changes demand rather than freezing the first night's appetite");
            var feeder=Feeder(650);var foreign=Feeder(9000);GeneralHelper.SetItemOwned(foreign,false);
            Check(FarmFeedForecast.Supply(new[]{feeder,feeder,foreign}).Other==650,"processed supplies are not duplicated and vendor feed is excluded");
            var a=new Item("cage_a");a.EnableTag("CAGE_TAG");var b=new Item("cage_b");b.EnableTag("CAGE_TAG");GeneralHelper.SetItemOwned(a,true);GeneralHelper.SetItemOwned(b,true);
            FarmFeedForecast.AssignNumbers(new[]{a,b});int numberA=a.GetTagReadonly(FarmFeedForecast.NumberTag).GetInt(),numberB=b.GetTagReadonly(FarmFeedForecast.NumberTag).GetInt();
            FarmFeedForecast.AssignNumbers(new[]{b,a});Check(numberA!=numberB && a.GetTagReadonly(FarmFeedForecast.NumberTag).GetInt()==numberA && b.GetTagReadonly(FarmFeedForecast.NumberTag).GetInt()==numberB,"machine numbers stay stable when inventory enumeration order changes");
            var cloned=new Item("cage_clone");Crops.SetInt(cloned,FarmFeedForecast.NumberTag,numberA);FarmFeedForecast.AssignNumbers(new[]{a,b,cloned});
            Check(cloned.GetTagReadonly(FarmFeedForecast.NumberTag).GetInt()>Math.Max(numberA,numberB),"copied machine numbers are repaired without renumbering the existing pair");
            GeneralHelper.SetCustomName(a,"育种一号");var feedGrid=Grid(10,3,rat,feeder);BindPanels(a,feedGrid,Grid(1,1));
            var row=FarmFeedForecast.InspectMachine(a,feedGrid,new[]{feeder});Check(row.Name=="育种一号" && row.Usable==650 && row.Nights==10,"custom names identify a machine's actual local feed supply");
            Check(FarmFeedForecast.InspectMachine(a,Grid(10,3,lobster),new[]{feeder})==null,"a fish placed outside an actual aquaculture tank does not invent a cage feeding demand");
            var missing=FarmFeedForecast.InspectMachine(b,Grid(10,3,rat),new GameItem[0]);Check(missing.Missing,"an absent dispenser gets a specific warning instead of invented food");
            Crops.SetInt(feeder,"FEED_DISPENSER_TARGET_PERCENT_INT",50);Check(FarmFeedForecast.InspectMachine(a,feedGrid,new[]{feeder}).LowTarget,"low feeding targets are reported instead of promising adequate care");
            Check(FarmFeedForecast.InspectMachine(a,feedGrid,new[]{feeder,Feeder(650)}).LowTarget,"one underfeeding dispenser is still warned when another dispenser has a normal target");
            Crops.SetInt(feeder,"FEED_DISPENSER_TARGET_PERCENT_INT",85);
            var ordered=FarmFeedForecast.Sort(new[]{row,new FarmFeedForecast.Row{Number=99,Name="soon",Usable=20,Nights=1},missing});
            Check(ordered[0].Missing && ordered[1].Name=="soon" && ordered[2].Name=="育种一号","urgent/missing supplies precede well-stocked machines");
            onlyLow=true;Check(FarmFeedForecast.Lines(new[]{row,missing}).Count()==1,"optional shortage-only view reduces long machine lists");onlyLow=false;
            foreach(string language in new[]{"zh","en"})
            {
                LoadLocale(language);var many=Enumerable.Range(1,100).Select(i=>new FarmFeedForecast.Row{Number=i,Name="Machine "+i,Usable=650,Nights=10}).ToList();
                var lines=FarmFeedForecast.Lines(many).ToList();Check(lines.Count==100 && lines.Last().Contains("Machine 100"),"all machines are listed without silent truncation: "+language);
                Check(lines.All(s=>!s.Contains("farm_feed_") && !s.Contains("{0}") && !s.Contains("<size")),"feed rows are localized plain native log text: "+language);
            }
            Check(NightReportLayout.ContentHeight(300,new float[]{28,40},5,12)==300,"a short report fits without forcing a needless scroll range");
            Check(NightReportLayout.RowHeight(120,20)>120,"wrapped messages receive enough row height instead of hiding their lower lines");
            float longReport=NightReportLayout.ContentHeight(300,Enumerable.Repeat(36f,100),5,12);
            Check(longReport>4000 && longReport-300>0,"a hundred native/add-on messages produce a real vertical scroll range");
            foreach(string language in new[]{"zh","en"})
            {
                LoadLocale(language);var text=FarmNightReport.Lines(new FarmNightReport.Snapshot{Alive=20},0,0,0);
                Check(text[0].StartsWith(language=="zh"?"动物情况：":"Animals:"),"concise animal-condition label: "+language);
                var growth=new FarmNightReport.Snapshot();growth.Growth.Add(new FarmNightReport.Forecast{Species=FarmRules.Pig,Stage="adult",Count=5,Nights=1});
                string line=FarmNightReport.Lines(growth,0,0,0)[1];
                Check(!line.Contains("供养正常时") && !line.Contains("adequate care"),"growth report omits the parenthetical qualifier: "+language);
                var mixed=new FarmNightReport.Snapshot();
                mixed.Growth.Add(new FarmNightReport.Forecast{Species=AnimalSpecies.Rat.speciesID,Stage="adult",Count=2,Nights=1});
                mixed.Growth.Add(new FarmNightReport.Forecast{Species=AnimalSpecies.Rat.speciesID,Stage="elder",Count=2,Nights=2});
                string[] speciesIds={FarmRules.Cow,FarmRules.Pig,FarmRules.Lobster,FarmRules.Sea};string[] names={"farm_cow","farm_pig","farm_lobster","farm_sea_cucumber"};
                for(int index=0;index<speciesIds.Length;index++)mixed.Growth.Add(new FarmNightReport.Forecast{Species=speciesIds[index],Stage="adult",Count=2,Nights=index+3});
                string complete=FarmNightReport.Lines(mixed,0,0,0)[1];
                for(int index=0;index<names.Length;index++)Check(complete.Contains(locale[names[index]]),"two earlier rat forecasts cannot hide the added species: "+language+"/"+names[index]);
                mixed.Growth.Clear();for(int index=1;index<=100;index++)mixed.Growth.Add(new FarmNightReport.Forecast{Species=FarmRules.Cow,Stage="adult",Count=1,Nights=index});
                complete=FarmNightReport.Lines(mixed,0,0,0)[1];
                Check(complete.Contains(string.Format(locale["farm_night_forecast"],locale["farm_cow"],1,100,locale["farm_night_stage_adult"])),"growth forecasts have no silent two-entry or later arbitrary cutoff: "+language);
            }
            LoadLocale("zh");wasteAuto=false;data.Clear();store=(PlayerStore)FormatterServices.GetUninitializedObject(typeof(PlayerStore));store.nightLogs=new List<string>{"电费已结清。","财务模组：今日净收益 +321"};
            owned=new List<GameItem>{a,rat,feeder};FarmNightReport.BeginNight();FarmNightReport.RecordProduct(true);FarmNightReport.BeginNight();FarmNightReport.EndNight();
            Check(store.nightLogs.Count==6 && store.nightLogs.Last().Contains("育种一号") && store.nightLogs.Last().Contains("约10晚"),"native report appends feed rows directly, without a redundant feed-header entry");
            Check(store.nightLogs[0]=="电费已结清。","per-machine feed report preserves native messages");
            Check(store.nightLogs[1]=="财务模组：今日净收益 +321","finance-mod report data remains intact beside husbandry data");
            Check(!store.nightLogs.Any(s=>s.Contains("饲料余量（")),"no empty standalone feed-reserves heading is added");
            Check(store.nightLogs[2].Contains("牛奶1份"),"a repeated sleep-start callback does not reset an active night's counters");
        }
        finally{h.UnpatchAll(h.Id);wasteAuto=true;onlyLow=false;FarmNightReport.Reset();}
        Console.WriteLine("Passed "+(checks-started)+" feces-transfer and per-machine feed planning checks (transfer boundary fixture; native diets, growth, production, stable names and complete lists).");
    }
    private static void Patch(Harmony h,MethodInfo target,string prefix){h.Patch(target,new HarmonyMethod(typeof(NightReportVerification).GetMethod(prefix)));}
    public static string Run(string directory)
    {
        checks=0;Configure(directory);FarmSpecies.Register();
        var h=new Harmony("pelican_cultivation.night_report_verification");bool enabled=ModEntry.Enabled;
        try
        {
            Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"Ready");Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Day"),"Day");
            Patch(h,AccessTools.Method(typeof(Progress),"Get"),"Get");Patch(h,AccessTools.Method(typeof(Progress),"Set"),"Set");
            Patch(h,AccessTools.Method(typeof(FarmNightReport),"OwnedItems"),"Owned");Patch(h,AccessTools.PropertyGetter(typeof(PlayerStore),"Instance"),"Store");
            Patch(h,AccessTools.PropertyGetter(typeof(FarmSettings),"AutoImmunity"),"Automatic");Patch(h,AccessTools.Method(typeof(FarmTreatment),"AvailableSerums"),"Supplies");
            Patch(h,AccessTools.Method(typeof(RNG),"Roll",new[]{typeof(int)}),"NoRoll");Patch(h,AccessTools.Method(typeof(UnityEngine.Debug),"Log",new[]{typeof(object)}),"NoLog");
            Patch(h,typeof(ItemDirectory).GetMethods().Single(m=>m.Name=="CreateEmptyItem" && !m.IsGenericMethod),"Empty");
            Patch(h,AccessTools.Method(typeof(GameItem),"SetSpriteAndShape"),"Sprite");
            h.Patch(AccessTools.Method(typeof(LocHelper),"GetLocalizedItem"),new HarmonyMethod(typeof(PelicanVerification).GetMethod("MechanicTextFixture")));
            ModEntry.Enabled=true;SnapshotChecks();NativeAutomation(h);FeedAndWasteChecks();SerumAndLifecycle();
            return "Passed "+checks+" native night-report, equipment and serum checks (both languages, append-only logs, repeat guards, forecasts, real doses and optional treatment).";
        }
        finally{h.UnpatchAll(h.Id);ModEntry.Enabled=enabled;FarmNightReport.Reset();locale.Clear();}
    }
    private static void SnapshotChecks()
    {
        var cow=Animal(FarmRules.Cow,5);cow.EnableTag("ANIMAL_DISEASED_TAG");cow.EnableTag("ANIMAL_PRION_TAG");
        var corpse=Animal(FarmRules.Pig,9);corpse.EnableTag("ANIMAL_IS_DEAD_TAG");
        var fry=Fish(FishSpecies.Tilapia,1,6);var egg=Fish(FarmSpecies.SeaCucumbers,0,2,true);
        var snapshot=FarmNightReport.Inspect(new GameItem[]{cow,cow,corpse,fry,egg,Animal(FarmRules.Pig,3,"MALE",false)});
        Check(snapshot.Alive==7 && snapshot.EggClutches==1 && snapshot.Corpses==1,"count individuals, exclude vendor stock, deduplicate, keep eggs separate from live animals");
        Check(snapshot.Ill==1 && snapshot.Prion==1,"ordinary illness and prion disease remain distinct");
        Check(snapshot.Growth.Any(g=>g.Species==FarmRules.Cow && g.Nights==1 && g.Stage=="adult"),"cattle forecast follows the configured native species age");
        Check(snapshot.Growth.Any(g=>g.Species==FarmRules.Sea && g.Count==2 && g.Stage=="hatch"),"fertilized egg forecasts its actual clutch size");
        var allAdded=FarmNightReport.Inspect(new GameItem[]{Animal(FarmRules.Cow,FarmSettings.CowAdult-2),Animal(FarmRules.Pig,FarmSettings.PigAdult-3),Fish(FarmSpecies.Lobsters,FarmSpecies.Lobsters.stageStartDays[4]-4),Fish(FarmSpecies.SeaCucumbers,FarmSpecies.SeaCucumbers.stageStartDays[4]-5)});
        foreach(string id in new[]{FarmRules.Cow,FarmRules.Pig,FarmRules.Lobster,FarmRules.Sea})Check(allAdded.Growth.Any(g=>g.Species==id && g.Stage=="adult"),"actual owned added stock contributes an adult-growth estimate: "+id);
        egg.DisableTag("FISH_FERTILIZED_TAG");Check(FarmNightReport.Inspect(new[]{egg}).Growth.Count==0,"unfertilized eggs have no invented hatch date");
        fry.EnableTag("PELICAN_FARM_TRANSITION_PENDING_BOOL");Check(FarmNightReport.Inspect(new[]{fry}).Blocked==1 && FarmNightReport.Inspect(new[]{fry}).Growth.Count==0,"full-tank transitions pause their forecast");
        var lobster=Fish(FarmSpecies.Lobsters,13);FarmSpecies.SetFloat(lobster,"FISH_INDUSTRIAL_DIET_TAG",1);
        Check(FarmNightReport.Inspect(new[]{lobster}).Growth.Single().Nights==2,"industrial-feed slowdown is included in growth estimates");
        FarmSpecies.SetFloat(lobster,"FISH_GROWTH_PROGRESS_TAG",0.25f);Check(FarmNightReport.Inspect(new[]{lobster}).Growth.Single().Nights==1,"saved fractional growth is retained in estimates");
        FarmSpecies.SetFloat(lobster,"FISH_HUNGER_TAG",0);Check(FarmNightReport.Inspect(new[]{lobster}).Growth.Count==0,"starved aquatic growth is not promised a completion date");
        var grid=Grid(10,2,new Item("feces",5,1));grid.childItems[0].EnableTag("ANIMAL_FECES_TAG");
        Check(FarmNightReport.Dirty(grid),"native hygiene warning begins at 25 percent contaminated cells");grid=Grid(10,2,new Item("feces",4,1));grid.childItems[0].EnableTag("ANIMAL_FECES_TAG");
        Check(!FarmNightReport.Dirty(grid),"less than 25 percent does not invent a hygiene threshold");
        grid=Grid(10,2,cow,new Item("feces",5,1));grid.childItems[1].EnableTag("ANIMAL_FECES_TAG");
        Check(FarmNightReport.Inspect(new[]{cow}).DirtyCages==1,"hygiene checks also cover uncaged animals in ordinary inventory grids");
        Check(FarmNightReport.UnsafeWater(999,1000,0,10,false) && !FarmNightReport.UnsafeWater(1000,1000,10,10,false),"water and waste warning boundaries match native checks");
        Check(!FarmNightReport.UnsafeWater(1000,1000,99,10,true),"sea cucumber waste immunity is respected");
        foreach(string language in new[]{"zh","en"})
        {
            LoadLocale(language);var logs=new List<string>{"native electricity paid","cosmetic mod message"};
            FarmNightReport.Append(logs.Add,snapshot,1,2,1);
            Check(logs.Count==5 && logs[0]=="native electricity paid" && logs[1]=="cosmetic mod message","exactly three lines append without replacing native/mod logs: "+language);
            Check(logs.Skip(2).All(s=>!s.Contains("farm_night_") && !s.Contains("{0}") && !s.StartsWith("-") && !s.Contains("<size") && !s.Contains("\\n")),"native UI owns prefixes and layout; localization resolves: "+language);
            Check(logs[4].Contains(language=="zh"?"血清无法清除":"serum cannot clear"),"prion warning does not promise a serum cure: "+language);
            Check(logs[4].Contains(language=="zh"?"自动使用绿色血清1次":"automatically used 1"),"report shows actual automatic dose count: "+language);
        }
    }
    private static void NativeAutomation(Harmony h)
    {
        Patch(h,AccessTools.Method(typeof(MachineAutoButcher),"ProcessAnimal"),"Harvest");
        var machine=new Item("auto_butcher");Crops.SetInt(machine,"AUTO_BUTCHER_CAGE_HARVEST_INT",3);Crops.SetInt(machine,"AUTO_BUTCHER_CAGE_AGE_MAX_INT",999);
        Crops.SetInt(machine,"AUTO_BUTCHER_CAGE_KEEP_FEMALES_INT",1);Crops.SetInt(machine,"AUTO_BUTCHER_CAGE_KEEP_MALES_INT",1);
        var animals=new List<GameItem>();
        foreach(string id in new[]{AnimalSpecies.Rat.speciesID,FarmRules.Cow,FarmRules.Pig})
        {
            foreach(string sex in new[]{"MALE","FEMALE"}) for(int i=0;i<2;i++) animals.Add(Animal(id,12+i,sex));
            var animal=Animal(id,5);FarmSpecies.SetFloat(animal,"ANIMAL_HUNGER_TAG",0);
            var dispenser=new Item("livestock_feed_dispenser");dispenser.EnableTag("FEED_DISPENSER_TAG");Crops.SetInt(dispenser,"STORED_KCAL_TAG",5000);Crops.SetInt(dispenser,"STORED_OTHER",5000);
            Crops.SetInt(dispenser,PlantDiet.PlantKcal,5000);
            var inventory=Grid(10,3,animal,dispenser);
            HusbandryHelper.TryFeed(animal,inventory);
            Check(Math.Abs(animal.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()-1020)<0.01 && MachineFeedDispenser.GetAnimalKcal(dispenser)==3980,"actual native dispenser feeds registered species and spends calories: "+id);
        }
        harvested.Clear();AccessTools.Method(typeof(MachineAutoButcher),"RunCage").Invoke(null,new object[]{machine,animals,Grid(30,3),0});
        Check(harvested.Count==6,"native keeper rules retain two breeders per species rather than two across the whole cage");
        foreach(string id in new[]{AnimalSpecies.Rat.speciesID,FarmRules.Cow,FarmRules.Pig})
            Check(harvested.Count(a=>a.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()==id && a.GetTagReadonly("ANIMAL_SEXE_TAG").GetString()=="FEMALE")==1 && harvested.Count(a=>a.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()==id && a.GetTagReadonly("ANIMAL_SEXE_TAG").GetString()=="MALE")==1,"native butcher spares a male and female of "+id);
        Check(typeof(ModEntry).Assembly.GetType("PelicanCultivation.FarmAutomation")==null && typeof(ModEntry).Assembly.GetType("PelicanCultivation.FarmBreedPatch")==null,"obsolete automation and hidden tank-population overrides are gone");
    }
    private static void SerumAndLifecycle()
    {
        LoadLocale("zh");data.Clear();store=(PlayerStore)FormatterServices.GetUninitializedObject(typeof(PlayerStore));store.nightLogs=new List<string>{"电费已结清。"};
        var directory=(HusbandryDirectory)FormatterServices.GetUninitializedObject(typeof(HusbandryDirectory));
        var serum=(GameItem)AccessTools.Method(typeof(HusbandryDirectory),"SerumGreen").Invoke(directory,null);serum.identifier="serum_green";GeneralHelper.SetItemOwned(serum,true);supplies=new[]{serum};
        var cow=Animal(FarmRules.Cow,5);cow.EnableTag("ANIMAL_DISEASED_TAG");owned=new List<GameItem>{cow};
        string part=Liquid.GetCurrentPartFromId("livestock_immunity");float before=GeneticHelper.GetCurrentStat(cow,"ANIMAL_IMMUNITY_TAG");
        automatic=false;FarmNightReport.BeginNight();HusbandryHelper.HandleDisease(cow);FarmNightReport.RecordFullProducts(cow);FarmNightReport.RecordFullProducts(cow);FarmNightReport.EndNight();
        Check(serum.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt()==10 && cow.GetTagReadonly(part).GetInt()==0,"automatic treatment defaults off and leaves manual serum untouched");
        Check(store.nightLogs.Count==4 && store.nightLogs[0]=="电费已结清。","actual native AddNightLog receives three husbandry entries after the electricity message");
        Check(store.nightLogs[2].Contains("1处产物栏已满"),"full product storage is reported once per host and cannot silently lose a production pause");
        FarmNightReport.EndNight();FarmNightReport.BeginNight();FarmNightReport.EndNight();Check(store.nightLogs.Count==4,"repeated callbacks and same-day reload guard do not append duplicate reports");
        data.Clear();automatic=true;FarmNightReport.BeginNight();HusbandryHelper.HandleDisease(cow);
        Check(serum.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt()==9 && cow.GetTagReadonly(part).GetInt()==20,"actual native green-serum callback spends exactly one real dose");
        Check(Math.Abs(GeneticHelper.GetCurrentStat(cow,"ANIMAL_IMMUNITY_TAG")-before*1.75f)<0.01 && cow.IsTag("ANIMAL_DISEASED_TAG"),"native temporary immunity boost is applied without directly curing disease");
        HusbandryHelper.HandleDisease(cow);Check(serum.GetTagReadonly("CURRENT_USE_COUNT_INT").GetInt()==9,"an active effect prevents stacking or wasting another dose");
        cow.DisableTag("ANIMAL_DISEASED_TAG");cow.EnableTag("ANIMAL_PRION_TAG");Crops.SetInt(cow,part,0);cow.DisableTag(part+"_INGESTER_ACTIVE");
        Check(!FarmTreatment.UseSerum(cow,supplies,part) && cow.IsTag("ANIMAL_PRION_TAG"),"prion disease alone never triggers automatic serum or loses its tag");
        cow.EnableTag("ANIMAL_DISEASED_TAG");Crops.SetInt(serum,"CURRENT_USE_COUNT_INT",0);Check(!FarmTreatment.UseSerum(cow,supplies,part),"an empty bottle cannot supply free treatment");
        Crops.SetInt(serum,"CURRENT_USE_COUNT_INT",5);GeneralHelper.SetItemOwned(serum,false);Check(!FarmTreatment.UseSerum(cow,supplies,part),"unowned vendor medicine is never consumed");
        GeneralHelper.SetItemOwned(serum,true);cow.EnableTag("ANIMAL_IS_DEAD_TAG");Check(!FarmTreatment.UseSerum(cow,supplies,part),"carcasses never receive serum");
        FarmNightReport.EndNight();Check(store.nightLogs.Last().Contains("自动使用绿色血清1次"),"only successful treatment is reported");
        data.Clear();owned.Clear();store.nightLogs.Clear();FarmNightReport.BeginNight();FarmNightReport.EndNight();Check(store.nightLogs.Count==0,"no livestock means no empty husbandry report");
        ModEntry.Enabled=false;owned.Add(Animal(FarmRules.Cow,5));FarmNightReport.BeginNight();FarmNightReport.EndNight();Check(store.nightLogs.Count==0,"disabled mod never adds reports");
    }
}
