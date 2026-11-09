using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using PelicanCultivation;

public static class CropUpdateVerification
{
    private static int checks,nextId=5000,nightCalls,readinessQueries,powerDraws;
    private static readonly Dictionary<GameItem,GraphNodeStorage[]> storage=new Dictionary<GameItem,GraphNodeStorage[]>();
    private static readonly Dictionary<GameInventory,GameItem> parents=new Dictionary<GameInventory,GameItem>();
    private static readonly List<GameItem> products=new List<GameItem>();
    private static Item currentMachine;
    private sealed class Item : GameItem
    {
        public Item(string id):base(id){modifiedState=state;uniqueId=++nextId;SetShape(new GridShapeBuilder().SetData(new byte[]{1},1));}
        public override void Validate(){modifiedState=state;}
        public override void Destroy(){unitCount=0;if(parentInventory is GameSlotInventory slot){slot.onSlotRemoveItemFunc?.Invoke(this,slot,null);Field(typeof(GameSlotInventory),"currentItem").SetValue(slot,null);}parentInventory=null;}
    }
    private sealed class Grid : GameGridInventory
    {public Grid():base(1,1){} public override void Validate(){}}
    private sealed class Slot : GameSlotInventory
    {public Slot():base(){} public override void Validate(){}}
    private static FieldInfo Field(Type type,string name)=>AccessTools.Field(type,name);
    private static void Check(bool value,string why){if(!value) throw new Exception("CROP UPDATE FAIL: "+why);checks++;}
    private static void Patch(Harmony h,MethodBase target,string fixture)
    {if(target==null)throw new Exception("Missing fixture target: "+fixture);h.Patch(target,new HarmonyMethod(typeof(CropUpdateVerification).GetMethod(fixture)));}
    private static GameGridInventory NewGrid()
    {
        var grid=(Grid)FormatterServices.GetUninitializedObject(typeof(Grid));
        Field(typeof(GameGridInventory),"items").SetValue(grid,new List<GameItem>());
        Field(typeof(TagSystem),"<dict>k__BackingField").SetValue(grid,new Dictionary<string,TagState>());return grid;
    }
    private static GameSlotInventory NewSlot()=> (GameSlotInventory)FormatterServices.GetUninitializedObject(typeof(Slot));
    private static Item Machine(bool nativeNight=false)
    {
        var machine=new Item("hydroponic");machine.EnableTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG);GeneralHelper.SetItemOwned(machine,true);
        MachineProgressHelper.InitProgressTypeMachine(machine,8,false);
        var input=NewSlot();storage[machine]=new GraphNodeStorage[]{NewSlot(),NewGrid(),NewSlot(),input,NewGrid(),NewSlot()};parents[input]=machine;
        input.mayInventoryRemoveItemFunc=RemovalRule();
        machine.onCycleEndSlotItemFunc=nativeNight?NativeNight(machine):(self,parent,slot)=>nightCalls++;
        AutoPlant.Attach(machine);HydroponicControl.Attach(machine);HydroponicControl.Attach(machine);return machine;
    }
    private static Action<GameItem,GameInventory,SlotMarker> NativeNight(Item machine)
    {
        // Execute the installed game's actual nightly closure, including its
        // READY -> start -> progress -> harvest path. Only UI/resource edges
        // are fixtures; this catches the premature stop missed by direct calls.
        var method=typeof(MachineHydroponic).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic)
            .SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            .Single(m=>m.Name.StartsWith("<Hydroponic>b__") && m.ReturnType==typeof(void)
                && m.GetParameters().Length==3 && m.GetParameters()[0].ParameterType==typeof(GameItem));
        object closure=FormatterServices.GetUninitializedObject(method.DeclaringType);
        var panels=storage[machine];
        foreach(var pair in new[]{new{field="inputSlot",index=3},new{field="batterySlot",index=0},new{field="moduleGrid",index=1}})
            Field(method.DeclaringType,pair.field).SetValue(closure,panels[pair.index]);
        return (Action<GameItem,GameInventory,SlotMarker>)Delegate.CreateDelegate(typeof(Action<GameItem,GameInventory,SlotMarker>),closure,method);
    }
    private static Item Seed(string id)
    {
        var seed=new Item(id);HydroponicHelper.InitSeedItem(seed,id);Crops.EnsureSeedClassification(seed);GeneralHelper.SetItemOwned(seed,true);seed.name="Edited "+id;return seed;
    }
    private static void Put(Item machine,GameItem seed)
    {
        var input=MachineHydroponic.GetInputSlot(machine);Field(typeof(GameSlotInventory),"currentItem").SetValue(input,seed);if(seed!=null) seed.parentInventory=input;
    }
    public static bool Body(GameItem machine,int index,ref GraphNodeStorage __result)
    {if(!storage.TryGetValue(machine,out var panels)) return true;__result=panels[index];return false;}
    public static bool Parent(GameInventory __instance,ref GameItem __result)
    {if(!parents.TryGetValue(__instance,out var machine)) return true;__result=machine;return false;}
    public static bool Associated(GameItem seed,ref GameItem __result){__result=currentMachine;return false;}
    public static bool Empty(string identifier,ref GameItem __result){__result=new Item(identifier);return false;}
    public static bool Sprite(GameItem __instance,ref GameItem __result){__result=__instance;return false;}
    public static bool CropSprite(GameItem item,string key,int width,int height){Crops.BindSprite(item,key,width,height);return false;}
    public static bool Factory(string identifier,ref GameItem __result)
    {
        string id=identifier;
        var seed=Rules.BySeed(id);if(seed!=null){__result=Crops.CreateSeed(seed);return false;}
        foreach(var crop in Rules.Crops)foreach(int size in crop.Sized?Rules.ActiveSizes:new[]{1})
            if(Rules.ProduceId(crop,size)==id){__result=Crops.CreateProduce(crop,size);return false;}
        if(id=="wine_berry"){__result=new Item(id);return false;}
        if(id=="fillerweed" || id=="nutrifruit")
        {
            var type=typeof(HydroponicDirectory);var directory=FormatterServices.GetUninitializedObject(type);
            __result=(GameItem)AccessTools.Method(type,id=="fillerweed"?"Fillerweed":"Nutrifruit").Invoke(directory,null);return false;
        }
        if(PlantRegistry.Get(id)!=null){__result=Seed(id);return false;}
        throw new Exception("Unregistered crop fixture item: "+id);
    }
    public static bool Deliver(GraphNodeStorage __0,GameItem __1,ref int __result){products.Add(__1);__result=0;return false;}
    public static bool NoOp()=>false;
    public static bool Quality(ref int __result){__result=0;return false;}
    public static bool Roll(ref bool __result){__result=false;return false;}
    public static bool RandomInt(ref int __result){__result=99;return false;}
    public static bool NotReady(ref bool __result){readinessQueries++;__result=false;return false;}
    public static bool English(string englishFallback,ref string __result){__result=englishFallback;return false;}
    public static bool SavedText(string text,ref string __result){__result=text;return false;}
    public static bool Power(ref bool __result){__result=true;return false;}
    public static bool DrawPower(ref bool __result){powerDraws++;__result=true;return false;}
    public static bool Capacity(ref int __result){__result=6000;return false;}
    public static bool EffectiveOwner(GameItem item,ref bool __result){__result=GeneralHelper.IsItemOwned(item);return false;}
    public static bool Text(string key,object[] arguments,ref string __result)
    {
        if(key!="mech_hydroponic_growing_prefix"){__result=key;return false;}
        __result="Growing ["+(arguments.Length==0?"":arguments[0])+"]";return false;
    }
    private static Func<GameItem,GameInventory,bool> RemovalRule()
    {
        var method=typeof(MachineHydroponic).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic)
            .SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static))
            .Single(m=>m.Name.StartsWith("<SetupInputSlot>b__") && m.Name.EndsWith("_1"));
        return (Func<GameItem,GameInventory,bool>)Delegate.CreateDelegate(typeof(Func<GameItem,GameInventory,bool>),method.IsStatic?null:Activator.CreateInstance(method.DeclaringType,true),method);
    }
    private static void FinishCycle(Item machine,Item seed)
    {
        currentMachine=machine;
        int required=seed.GetTagReadonly(HydroponicHelper.REQUIRED_PROGRESS_INT).GetInt();
        Crops.SetInt(machine,MachineProgressHelper.MACHINE_PROGRESS_CURRENT_TAG,required);
        Crops.SetInt(seed,HydroponicHelper.GROWING_PROGRESS_INT,Math.Max(0,required-8));
        HydroponicHelper.ContinueGrowing(seed,8,machine);
    }
    private static void PerennialChecks()
    {
        var removable=RemovalRule();
        foreach(string id in new[]{Rules.Crops[2].SeedId,Rules.Crops[4].SeedId,Rules.Crops[5].SeedId,Rules.Crops[7].SeedId,"bloomberry_seed"})
        {
            var machine=Machine();var other=Machine();var seed=Seed(id);Put(machine,seed);currentMachine=machine;
            PlantGeneticsHelper.SetTraitRank(seed,"hardy",4);Crops.SetInt(seed,PlantGeneticsHelper.GENETIC_HEAT_INT,17);
            HydroponicHelper.StartGrowing(seed,machine);seed.name="Growing [Edited "+id+"]";
            int identity=seed.uniqueId;HydroponicControl.SetStop(machine,true);
            Check(MachineProgressHelper.GetMachineState(machine)=="STATE_WORKING" && !removable(seed,MachineHydroponic.GetInputSlot(machine)),"arming stop does not interrupt/ unlock an unfinished crop: "+id);
            int calls=nightCalls;machine.onCycleEndSlotItemFunc(machine,null,null);Check(nightCalls==calls+1,"armed working crop still gets its native nightly callback: "+id);
            products.Clear();FinishCycle(machine,seed);
            int harvest=products.Count;
            Check(harvest>0 && HydroponicControl.IsStopped(machine) && !HydroponicControl.IsStopped(other),"actual native harvest stops only the selected machine: "+id);
            Check(seed.unitCount==1 && seed.uniqueId==identity && MachineHydroponic.GetInputSlot(machine).childItem==seed && !HydroponicHelper.IsGrowingSeed(seed),"same perennial parent survives in a paused state: "+id);
            Check(!MachineHydroponic.GetInputSlot(machine).mayInventoryRemoveItemFunc(seed,MachineHydroponic.GetInputSlot(machine)) && !machine.IsTag(MachineHelper.LOCKED_WORKING_TAG),"paused perennial still requires a hatchet while its module bay unlocks: "+id);
            Check(seed.name=="Edited "+id && PlantGeneticsHelper.GetTraitRank(seed,"hardy")==4 && PlantGeneticsHelper.GetHeat(seed)==17,"plant name, edited genes and heat survive stopping: "+id+"; actual="+seed.name+", hardy="+PlantGeneticsHelper.GetTraitRank(seed,"hardy")+", heat="+PlantGeneticsHelper.GetHeat(seed));
            Check(MachineHydroponic.GetGrowStatus(machine)=="hydro_stopped","native grow-status entry reports the stop: "+id);
            calls=nightCalls;for(int i=0;i<3;i++) machine.onCycleEndSlotItemFunc(machine,null,null);
            Check(nightCalls==calls && products.Count==harvest,"stopped nights do not run the original power/water callback or duplicate a harvest: "+id);
            var loaded=Machine();loaded.state=new TagSystem();foreach(var tag in machine.state.dict) loaded.state.dict[tag.Key]=tag.Value.Clone();loaded.modifiedState=loaded.state;
            Check(HydroponicControl.IsStopped(loaded),"stop persists through saved tag restoration: "+id);
            int queries=readinessQueries;AutoPlant.TryReplant(machine);
            Check(readinessQueries==queries,"per-machine stop blocks replanting before reserve/scene lookup: "+id);
            HydroponicControl.SetStop(machine,false);currentMachine=machine;HydroponicHelper.StartGrowing(seed,machine);
            Check(MachineProgressHelper.GetMachineState(machine)=="STATE_WORKING" && HydroponicHelper.IsPerennialEstablished(seed) && seed.GetTagReadonly(HydroponicHelper.REQUIRED_PROGRESS_INT).GetInt()==HydroponicHelper.GetRequiredProgress(seed),"resuming the same parent uses regrowth, not its first-harvest period: "+id);
            Check(!removable(seed,MachineHydroponic.GetInputSlot(machine)) && seed.uniqueId==identity,"resumed work relocks the same seed: "+id);
            // Off cancels an armed stop, retaining the native continuous loop.
            products.Clear();FinishCycle(machine,seed);
            Check(products.Count>0 && MachineProgressHelper.GetMachineState(machine)=="STATE_WORKING" && !seed.IsTag(HydroponicControl.RestingTag),"default continuous production still starts a fresh native regrowth cycle: "+id);
            HydroponicControl.SetStop(machine,true);FinishCycle(machine,seed);
            var input=MachineHydroponic.GetInputSlot(machine);
            Check(!input.mayInventoryRemoveItemFunc(seed,input),"finished perennial cannot be dragged out for a seed refund: "+id);
            var hatchet=new Item("hatchet");GeneralHelper.SetItemOwned(hatchet,true);HydroponicHelper.InitHatchetPlantRemoval(hatchet);
            Check(hatchet.mayThisTargetItemFunc.InvokeAllReduce(hatchet,machine,InvokeFuncExtensions.BoolReduceOR) && hatchet.canThisTargetItemFunc.InvokeAllReduce(hatchet,machine,InvokeFuncExtensions.BoolReduceOR),"actual native hatchet can target the paused machine: "+id);
            int beforeClearing=products.Count;hatchet.onThisTargetItemFunc(hatchet,machine);
            Check(seed.unitCount==0 && input.childItem==null && products.Count==beforeClearing,"native hatchet clears the plant without returning a seed or making another harvest: "+id);
            Check(!seed.IsTag(HydroponicControl.RestingTag) && !seed.IsTag(HydroponicHelper.PERENNIAL_ESTABLISHED_INT),"hatchet removal clears the old machine/cycle link: "+id);
            var replacement=Seed(Rules.Crops[1].SeedId);Put(machine,replacement);calls=nightCalls;machine.onCycleEndSlotItemFunc(machine,null,null);
            Check(nightCalls==calls+1 && !HydroponicControl.IsStopped(machine),"manually inserted replacement can start one crop while stop-after-harvest stays armed: "+id);
        }
        var oneShotMachine=Machine();var oneShot=Seed(Rules.Crops[0].SeedId);Put(oneShotMachine,oneShot);currentMachine=oneShotMachine;
        HydroponicHelper.StartGrowing(oneShot,oneShotMachine);HydroponicControl.SetStop(oneShotMachine,true);products.Clear();FinishCycle(oneShotMachine,oneShot);
        Check(products.Any(p=>p.identifier==Rules.Crops[0].SeedId) && oneShot.unitCount==0 && HydroponicControl.IsStopped(oneShotMachine),"native annual harvest returns its seeds and leaves the empty machine stopped despite automatic replanting");
        int readyBefore=readinessQueries;AutoPlant.TryReplant(oneShotMachine);Check(readinessQueries==readyBefore,"an empty stopped annual machine cannot request reserve seeds");
        var unowned=Machine();GeneralHelper.SetItemOwned(unowned,false);HydroponicControl.SetStop(unowned,true);Check(!unowned.GetTagReadonly(HydroponicControl.StopTag).GetBool(),"unowned machines cannot be configured");
        var disabled=Machine();HydroponicControl.SetStop(disabled,true);ModEntry.Enabled=false;int before=nightCalls;disabled.onCycleEndSlotItemFunc(disabled,null,null);
        Check(nightCalls==before+1 && !HydroponicControl.IsStopped(disabled),"disabling the mod restores the original nightly callback");ModEntry.Enabled=true;
    }
    private static void HotfixChecks()
    {
        foreach(var crop in Rules.Crops)
        {
            var machine=Machine(true);var seed=Seed(crop.SeedId);Put(machine,seed);currentMachine=machine;
            Field(typeof(GameSlotInventory),"currentItem").SetValue(storage[machine][2],new Item("water"));
            HydroponicControl.SetStop(machine,true);
            Check(!HydroponicControl.IsStopped(machine),"unstarted crop is not prematurely stopped: "+crop.Key);
            int draws=powerDraws;
            machine.onCycleEndSlotItemFunc(machine,null,null);
            Check(HydroponicHelper.IsGrowingSeed(seed) && seed.GetTagReadonly(HydroponicHelper.GROWING_PROGRESS_INT).GetInt()>0 && powerDraws==draws+1,"native night starts and advances a fresh seed with one power draw: "+crop.Key);
            products.Clear();int limit=100;
            while(!HydroponicControl.IsStopped(machine) && --limit>0) machine.onCycleEndSlotItemFunc(machine,null,null);
            Check(limit>0 && products.Count>0,"actual nightly path reaches one harvest and stops: "+crop.Key);
            int count=products.Count;draws=powerDraws;
            for(int i=0;i<3;i++)machine.onCycleEndSlotItemFunc(machine,null,null);
            Check(products.Count==count && powerDraws==draws,"paused nights neither duplicate harvest nor draw power: "+crop.Key);
        }
        // Use the complete native hatchet factory and public targeting gates,
        // rather than only calling its plant-removal delegate in isolation.
        var directory=FormatterServices.GetUninitializedObject(typeof(MeleeWeaponItemDirectory));
        var hatchet=(GameItem)AccessTools.Method(typeof(MeleeWeaponItemDirectory),"CreateHatchetWeapon").Invoke(directory,null);
        hatchet.identifier="hatchet";GeneralHelper.SetItemOwned(hatchet,true);
        foreach(bool paused in new[]{false,true})
        {
            var machine=Machine();var seed=Seed(Rules.Crops[7].SeedId);Put(machine,seed);currentMachine=machine;
            HydroponicHelper.StartGrowing(seed,machine);
            if(paused){HydroponicControl.SetStop(machine,true);FinishCycle(machine,seed);}
            Check(hatchet.MayTarget(machine) && hatchet.CanTarget(machine),"full native hatchet gates accept the machine; paused="+paused);
            Check(hatchet.MayTarget(seed) && hatchet.CanTarget(seed),"full native hatchet gates accept the plant inside its input slot; paused="+paused);
            GeneralHelper.SetItemOwned(machine,false);Check(!hatchet.MayTarget(seed) && !hatchet.CanTarget(seed),"unowned machines cannot be cleared through the seed slot");GeneralHelper.SetItemOwned(machine,true);
            int count=products.Count;hatchet.onThisTargetItemFunc(hatchet,seed);
            Check(seed.unitCount==0 && MachineHydroponic.GetInputSlot(machine).childItem==null && products.Count==count,"seed-slot clearing removes the original plant without a harvest or refund");
        }
        var loose=Seed(Rules.Crops[7].SeedId);Check(!hatchet.MayTarget(loose) && !hatchet.CanTarget(loose),"loose inventory seeds cannot be chopped");
        foreach(string tag in new[]{"ANIMAL_ITEM_TAG","FISH_ITEM_TAG"})
        {
            var nativeTarget=new Item("native_stock");nativeTarget.EnableTag(tag);GeneralHelper.SetItemOwned(nativeTarget,true);
            Check(hatchet.MayTarget(nativeTarget) && hatchet.CanTarget(nativeTarget),"hatchet keeps its native livestock/fish targeting: "+tag);
        }
        var otherTool=new Item("kitchen_knife");GeneralHelper.SetItemOwned(otherTool,true);HydroponicHelper.InitHatchetPlantRemoval(otherTool);
        var otherMachine=Machine();var otherSeed=Seed(Rules.Crops[7].SeedId);Put(otherMachine,otherSeed);currentMachine=otherMachine;HydroponicHelper.StartGrowing(otherSeed,otherMachine);
        Check(!otherTool.CanTarget(otherSeed),"seed-slot clearing remains exclusive to the hatchet");
        ModEntry.Enabled=false;Check(!hatchet.CanTarget(otherSeed),"disabling the mod restores native seed-slot rejection");ModEntry.Enabled=true;
    }
    private static void EconomyChecks()
    {
        foreach(var crop in Rules.Crops)foreach(int size in crop.Sized?Rules.ActiveSizes:new[]{1})
        {
            var fresh=Crops.CreateProduce(crop,size);long value=fresh.unitValue;int kcal=fresh.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();
            CropGeometry.PrepareLegacy(fresh,crop);CropGeometry.PrepareLegacy(fresh,crop);
            Check(fresh.unitValue==value && fresh.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==kcal,"fresh produce is stamped and never receives a second upgrade: "+crop.Key+size);
            foreach(int remaining in Rules.Index(crop)<8?new[]{0,1,25,50,100}:new int[0])
            {
                var saved=new Item(Rules.ProduceId(crop,size));Crops.BindSprite(saved,"old",Rules.CropWidth(crop,size),Rules.CropHeight(crop,size));
                Crops.SetInt(saved,Rules.SizeTag,size);Crops.SetInt(saved,Rules.CropFormatTag,18);Crops.SetInt(saved,"CALORIE_VALUE_TAG",remaining);
                saved.unitValue=CropEconomy.PreviousPrice(crop)*2;Crops.SetInt(saved,"OTHER_MOD_STORED",991);
                CropGeometry.PrepareLegacy(saved,crop);long once=saved.unitValue;int calories=saved.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();CropGeometry.PrepareLegacy(saved,crop);
                Check(saved.unitValue==once && saved.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==calories && saved.GetTagReadonly("OTHER_MOD_STORED").GetInt()==991,"old save upgrades exactly once and keeps foreign metadata: "+crop.Key+size+remaining);
                Check((remaining==0?calories==0:calories>0) && calories==(int)Math.Round(remaining*(double)Rules.CropCalories(crop,size)/(size==0?50:size==3?300:100),MidpointRounding.AwayFromZero),"remaining portions scale proportionally, including exhausted food: "+crop.Key+size+remaining);
            }
            Check(crop.Price>=12,"ordinary crops meet the native regular Nutrifruit base price: "+crop.Key);
        }
        // Build real native Fillerweed and Nutrifruit through their installed
        // factories; only render/placement/resource lookup boundaries are fixtures.
        var nutri=DirectoryMaster.Item("nutrifruit");Check(nutri.unitValue==12 && nutri.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==300,"native comparison baseline is a 12-value, 300-kcal Nutrifruit after innate nutrition");
        var filler=Seed("fillerweed_seed");var fm=Machine();Put(fm,filler);currentMachine=fm;HydroponicHelper.StartGrowing(filler,fm);products.Clear();FinishCycle(fm,filler);
        int fillerCalories=products.Where(p=>p.IsGameItemType("FOOD")).Sum(p=>p.GetTagReadonly("CALORIE_VALUE_TAG").GetInt());
        int fillerNights=Rules.Days(HydroponicHelper.GetRequiredProgress(filler),8);
        var corn=Seed(Rules.Crops[2].SeedId);var cm=Machine();Put(cm,corn);currentMachine=cm;HydroponicHelper.StartGrowing(corn,cm);int cornFirst=HydroponicHelper.GetRequiredProgress(corn);products.Clear();FinishCycle(cm,corn);
        int cornCalories=products.Sum(p=>p.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()),cornRegrowth=HydroponicHelper.GetRequiredProgress(corn);
        Check(cornFirst==30 && cornRegrowth==15 && products.Count==8,"actual innate Corn has 30 first work, 15 regrowth work and eight harvested items");
        Check(cornCalories/(double)Rules.Days(cornRegrowth,8)>fillerCalories/(double)fillerNights,"regular-care mature Corn beats unmodified native Fillerweed calories per baseline-powered night");
        Console.WriteLine("Native balance comparison: Nutrifruit="+nutri.unitValue+" value / "+nutri.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()+" kcal; Fillerweed="+fillerCalories+" kcal / "+fillerNights+" night; mature Corn="+cornCalories+" kcal / "+Rules.Days(cornRegrowth,8)+" nights; no quality/production modules.");
        Check(NightReportLayout.FontSize(15,24,true,15)==24 && NightReportLayout.FontSize(15,24,true,11)==24,"the actual native prefab's fitted 15/11 text uses its original auto-size maximum of 24");
        Check(NightReportLayout.FontSize(15,24,true,30)==30 && NightReportLayout.FontSize(18,72,false,11)==18 && NightReportLayout.FontSize(0,0,false,18)==18,"external enlargements survive, fixed-size templates retain their font and missing templates safely fall back");
        var old=new Item(Rules.Crops[2].ProduceId){unitValue=8};Crops.BindSprite(old,"corn",2,2);Crops.SetInt(old,Rules.CropTag,3);Crops.SetInt(old,Rules.SizeTag,1);Crops.SetInt(old,Rules.CropFormatTag,18);FoodItemHelper.InitFoodItem(old,50,false);
        var node=new SaveItemNode{identifier=old.identifier,unitCount=1,uuid=12345,uniqueId=old.uniqueId,itemType=typeof(GameItem).FullName,itemState=old.state.Clone(),itemShape=new GridShapeBuilder(old.shape),itemModifiedShape=new GridShapeBuilder(old.modifiedShape),unitValue=8,itemTypes=new List<string>(old.itemTypes),name="Old corn",spriteAtlasPath=ModEntry.ModId,spritePath="corn"};
        node.OnBeforeSerialize();node.OnAfterDeserialize();var decoded=SaveManager.DecodeNodes(new List<SaveItemNode>{node});
        Check(!decoded.IsTag(CropEconomy.FormatTag),"actual native save decoder replaces the new factory marker with the old saved tag set");
        decoded.onLoaded?.Invoke(decoded);long savedValue=decoded.unitValue;int savedCalories=decoded.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();decoded.onLoaded?.Invoke(decoded);
        Check(savedValue==16 && savedCalories==160 && decoded.unitValue==16 && decoded.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==160,"actual saved partial Corn reloads at the new value, keeps its half portion and cannot upgrade twice");
        LegacyCatalogChecks();
    }
    private static void LegacyCatalogChecks()
    {
        var directory=new ModItemDirectory();Field(typeof(ModItemDirectory),"<factoryDictionary>k__BackingField").SetValue(directory,new Dictionary<string,Func<GameItem>>());Crops.RegisterItems(directory);DirectoryMaster.AddDirectory<GameItem>(directory);
        var entries=DirectoryMaster.GetIdentifierList<GameItem>(typeof(ModItemDirectory).Name);
        Check(entries.Count==36 && !entries.Any(id=>id.EndsWith("_large")) && !entries.Contains("pelican_parsnip_small"),"actual native crop catalog has 36 entries including three blueberry sizes and no obsolete duplicates");
        foreach(var crop in Rules.Crops)
        {
            Check(entries.Contains(crop.SeedId) && entries.Contains(crop.ProduceId),"current seeds and normal produce remain in the native catalog: "+crop.Key);
            if(crop.Sized)Check(entries.Contains(crop.ProduceId+"_small") && entries.Contains(crop.ProduceId+"_huge"),"small and giant produce remain available: "+crop.Key);
            foreach(string alias in (crop.Sized&&Rules.Index(crop)<8) || crop.Key=="parsnip"?new[]{crop.ProduceId+"_large"}:new string[0]) CheckLegacySave(crop,alias,2);
            if(crop.Key=="parsnip")CheckLegacySave(crop,crop.ProduceId+"_small",0);
        }
        foreach(string id in new[]{"nutrifruit","wine_berry","unrelated_large","pelican_other_large",Rules.Crops[2].SeedId,Rules.Crops[2].ProduceId+"_huge"})
            Check(LegacyCropIds.Current(id)==id,"legacy alias repair leaves unrelated and current IDs untouched: "+id);
    }
    private static void CheckLegacySave(CropDefinition crop,string alias,int oldSize)
    {
        Check(!DirectoryMaster.GetIdentifierList<GameItem>(typeof(ModItemDirectory).Name).Contains(alias),"retired ID is absent from the cheat catalog: "+alias);
        var old=new Item(alias);Crops.BindSprite(old,"legacy",oldSize==0?1:2,oldSize==0?1:3);Crops.SetInt(old,Rules.CropTag,Rules.Index(crop)+1);Crops.SetInt(old,Rules.SizeTag,oldSize);Crops.SetInt(old,Rules.CropFormatTag,17);FoodItemHelper.InitFoodItem(old,oldSize==0?50:200,false);
        long oldValue=(long)Math.Round(CropEconomy.PreviousPrice(crop)*(oldSize==0?0.5:2),MidpointRounding.AwayFromZero);
        var node=new SaveItemNode{identifier=alias,unitCount=1,uuid=++nextId,uniqueId=old.uniqueId,itemType=typeof(GameItem).FullName,itemState=old.state.Clone(),itemShape=new GridShapeBuilder(old.shape),itemModifiedShape=new GridShapeBuilder(old.modifiedShape),unitValue=oldValue,itemTypes=new List<string>(old.itemTypes),name="Legacy crop",spriteAtlasPath=ModEntry.ModId,spritePath="legacy"};
        node.OnBeforeSerialize();node.OnAfterDeserialize();var loaded=SaveManager.DecodeNodes(new List<SaveItemNode>{node});loaded.onLoaded?.Invoke(loaded);
        Check(loaded.identifier==crop.ProduceId && loaded.unitValue==crop.Price && loaded.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==Rules.CropCalories(crop,1),"actual native save decoding converts the retired ID and upgrades its value/calories: "+alias);
        loaded.onLoaded?.Invoke(loaded);Check(loaded.unitValue==crop.Price && loaded.GetTagReadonly(Rules.SizeTag).GetInt()==1,"reloading the converted legacy item is stable: "+alias);
    }
    public static string Run()
    {
        checks=0;bool previous=ModEntry.Enabled;ModEntry.Enabled=true;
        PlantTraitList.Register();PlantList.Register();foreach(var crop in Rules.Crops)PlantRegistry.Add(Crops.DefinePlant(crop,crop.Key));
        var h=new Harmony("pelican_cultivation.crop_update_fixture");
        var masterField=Field(typeof(GameMaster),"<current>k__BackingField");
        object previousMaster=masterField.GetValue(null);
        masterField.SetValue(null,FormatterServices.GetUninitializedObject(typeof(GameMaster)));
        try
        {
            Patch(h,AccessTools.Method(typeof(MachineHelper),"GetBodyStorage"),"Body");
            Patch(h,AccessTools.Method(typeof(GameInventory),"GetParentItem"),"Parent");
            Patch(h,AccessTools.Method(typeof(HydroponicHelper),"GetAssociatedHydroponic"),"Associated");
            Patch(h,AccessTools.Method(typeof(ItemDirectory),"CreateEmptyItem",new[]{typeof(string)}),"Empty");
            Patch(h,AccessTools.Method(typeof(GameItem),"SetSpriteAndShape",new[]{typeof(string),typeof(string)}),"Sprite");
            Patch(h,AccessTools.Method(typeof(Crops),"Sprite"),"CropSprite");
            Patch(h,AccessTools.Method(typeof(DirectoryMaster),"Item",new[]{typeof(string),typeof(bool)}),"Factory");
            Patch(h,AccessTools.Method(typeof(GraphUtils),"TryAcceptAll"),"Deliver");
            foreach(string name in new[]{"HandleNutriment","HandleContaminant","HandleSmugglerModule"})Patch(h,AccessTools.Method(typeof(HydroponicHelper),name),"NoOp");
            Patch(h,AccessTools.Method(typeof(MachineHydroponic),"HandleWaterConsumption"),"NoOp");
            Patch(h,AccessTools.Method(typeof(ChemicalCompositionHelper),"SetPotatoComposition"),"NoOp");
            Patch(h,AccessTools.Method(typeof(ModuleHelper),"GetAjustedQuality"),"Quality");
            foreach(var method in typeof(RNG).GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name=="Roll"))Patch(h,method,"Roll");
            Patch(h,AccessTools.Method(typeof(RNG),"GetRandomInt",new[]{typeof(int),typeof(int)}),"RandomInt");
            Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"NotReady");
            Patch(h,AccessTools.Method(typeof(LocHelper),"GetLocalizedMechanic"),"Text");
            Patch(h,AccessTools.Method(typeof(LocHelper),"GetLocalizedItem"),"Text");
            Patch(h,AccessTools.Method(typeof(LocHelper),"GetLocalizedMechanicOrDefault"),"English");
            Patch(h,AccessTools.Method(typeof(LocHelper),"RelocalizeIfMissing"),"SavedText");
            foreach(var method in typeof(MachineHelper).GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name=="CanPower"))Patch(h,method,"Power");
            Patch(h,AccessTools.Method(typeof(MachineHelper),"TryDrawCyclePower"),"DrawPower");
            Patch(h,AccessTools.Method(typeof(MachineHelper),"OnMachineActioned"),"NoOp");
            Patch(h,AccessTools.Method(typeof(WaterHelper),"GetCurrentCapacityML"),"Capacity");
            Patch(h,AccessTools.Method(typeof(GeneralHelper),"IsItemEffectivelyOwned"),"EffectiveOwner");
            HotfixChecks();PerennialChecks();EconomyChecks();
            return "Passed "+checks+" crop-update checks: actual native harvest/stop/regrowth, native seed-removal rules, save migration, partial food, original-crop balance and report font baseline. Unity render, scene inventory, care inputs and RNG are fixtures; no player save is opened.";
        }
        finally{h.UnpatchAll(h.Id);masterField.SetValue(null,previousMaster);ModEntry.Enabled=previous;storage.Clear();parents.Clear();products.Clear();}
    }
}
