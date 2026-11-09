using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PelicanCultivation
{
    public static class AutoPlant
    {
        public const string LastPlantTag="PELICAN_REPLANT_SPECIES_STRING";
        private static readonly ConditionalWeakTable<GameItem,object> attached=new ConditionalWeakTable<GameItem,object>();
        public static void Remember(GameItem machine,GameItem seed)
        {
            if(machine==null || seed==null || !machine.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG)) return;
            string id=HydroponicHelper.GetPlantId(seed);
            if(string.IsNullOrEmpty(id) || machine.GetTagReadonly(LastPlantTag).GetString()==id) return;
            machine.ModifyTag(LastPlantTag,s=>s.Enable().SetString(id));
        }
        public static void Attach(GameItem machine)
        {
            if(machine==null || !machine.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG) || attached.TryGetValue(machine,out var _)) return;
            attached.Add(machine,new object());
            MachineHydroponic.GetInputSlot(machine).onSlotAddItemLateFunc+=(seed,inventory,slot)=>{ if(ModEntry.Enabled) Remember(machine,seed); };
            var native=machine.onCycleEndSlotItemFunc;
            machine.onCycleEndSlotItemFunc=(self,parent,slot)=>{
                if(ModEntry.Enabled) Remember(self,MachineHydroponic.GetInputSlot(self)?.childItem);
                native?.Invoke(self,parent,slot);
                // Also handles old idle machines whose output already held seeds.
                TryReplant(self);
            };
        }
        public static bool TrySelectAndMove(GameItem currentSeed,IEnumerable<GameItem> output,string species,bool enabled,bool machineOwned,Func<GameItem,bool> moveOne)
        {
            if(!enabled || !machineOwned || currentSeed!=null || output==null) return false;
            foreach(var seed in output)
            {
                if(seed==null || seed.unitCount<1 || !GeneralHelper.IsItemOwned(seed) || !seed.IsTag("SEED") || !seed.IsTag(HydroponicHelper.SEED_ITEM_TAG) || seed.IsTag("CONTAINER_TAG") || HydroponicHelper.IsGrowingSeed(seed)) continue;
                string id=HydroponicHelper.GetPlantId(seed);
                if(string.IsNullOrEmpty(id) || PlantRegistry.Get(id)==null || (!string.IsNullOrEmpty(species) && id!=species)) continue;
                if(moveOne(seed)) return true;
            }
            return false;
        }
        public static bool TryFromSources(GameItem currentSeed,IEnumerable<GameItem> output,IEnumerable<GameItem> backpack,IEnumerable<GameItem> warehouse,string species,bool enabled,bool machineOwned,Func<GameItem,bool> moveOne)
        {
            if(TrySelectAndMove(currentSeed,output,species,enabled,machineOwned,moveOne)) return true;
            // An unconfigured empty machine must not pick a random inventory seed.
            if(string.IsNullOrEmpty(species)) return false;
            return TrySelectAndMove(currentSeed,backpack,species,enabled,machineOwned,moveOne)
                || TrySelectAndMove(currentSeed,warehouse,species,enabled,machineOwned,moveOne);
        }
        public static string InferSpecies(IEnumerable<GameItem> output)
        {
            string inferred=null;
            foreach(var item in output)
            {
                string id=HydroponicHelper.GetPlantId(item);
                if(PlantRegistry.Get(id)==null)
                {
                    int crop=Crops.Index(item);
                    if(crop>=0 && crop<Rules.Crops.Length) id=Rules.Crops[crop].SeedId;
                    else
                    {
                        id=null;
                        foreach(var plant in PlantRegistry.All)
                            if(plant.harvestItemId==item.identifier)
                            { if(id!=null && id!=plant.id) return ""; id=plant.id; }
                    }
                }
                if(string.IsNullOrEmpty(id)) continue;
                if(inferred!=null && inferred!=id) return "";
                inferred=id;
            }
            return inferred??"";
        }
        private static IEnumerable<GameItem> StoredSeeds(GraphNodeStorage root)
        {
            if(root==null) yield break;
            foreach(var seed in GraphUtils.FindAllChildrenType<GameItem>(root))
            {
                if(!seed.IsTag("SEED")) continue;
                bool insideMachine=false;
                foreach(var parent in GraphUtils.FindParentsType<GameItem>(seed.parentInventory))
                    if(parent.IsTag("STANDARD_MACHINE_TAG") || parent.IsTag("manufacture") || parent.IsGameItemType("MACHINE") || parent.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG))
                    { insideMachine=true; break; }
                if(!insideMachine) yield return seed;
            }
        }
        public static void TryReplant(GameItem machine)
        {
            if(!ModEntry.Enabled || !ModEntry.AutoReplantEnabled || machine==null || HydroponicControl.StopRequested(machine) || !Progress.Ready || !machine.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG) || !GeneralHelper.IsItemOwned(machine)) return;
            try
            {
                var input=MachineHydroponic.GetInputSlot(machine);
                if(input==null || input.childItem!=null) return; // Perennial parents remain in place.
                if(MachineProgressHelper.GetMachineState(machine)!="STATE_READY") return;
                var output=MachineHydroponic.GetOutputSlot(machine);
                string species=machine.GetTagReadonly(LastPlantTag).GetString();
                var produced=new List<GameItem>(output.childItems);
                if(string.IsNullOrEmpty(species)) species=InferSpecies(produced);
                TryFromSources(input.childItem,produced,StoredSeeds(EmporiumEntry.Instance.invElement),StoredSeeds(EmporiumEntry.Instance.backInvinvElement),species,true,true,seed=>MoveOne(seed,input,machine));
            }
            catch(Exception e) { ModEntry.Error("Automatic hydroponic replant",e); }
        }
        private static bool MoveOne(GameItem source,GameSlotInventory input,GameItem machine)
        {
            if(input.childItem!=null || source.MaxNumRemove()<1) return false;
            bool split=source.unitCount>1;
            GameItem one=source;
            if(split)
            {
                // Native GetSplitItem creates a factory seed and loses edited
                // genetics. Copy the instance state before moving one unit.
                one=DirectoryMaster.Item(source.identifier);
                CopySeedInstance(source,one);
            }
            bool accepted=false;
            try
            {
                var slot=input.TryFindOneValidInventorySlot(one);
                if(slot==null) return false;
                slot.TryAcceptOnce(1);
                accepted=one.parentInventory==input && input.childItem==one;
                if(accepted) Remember(machine,one);
                return accepted;
            }
            finally
            {
                if(split)
                {
                    // A later UI callback may throw after placement; conserve
                    // the stack even in that case, and never consume on failure.
                    if(one.parentInventory==input) ConsumeStackUnit(source);
                    else one.Destroy();
                }
            }
        }
        public static void CopySeedInstance(GameItem source,GameItem one)
        {
            // TagSystem.Clone only copies the dictionary, sharing mutable tag
            // values. Clone each TagState so editing the replanted seed cannot
            // mutate the remaining stack.
            one.state=new TagSystem();
            foreach(var pair in source.state.dict) one.state.dict[pair.Key]=pair.Value.Clone();
            one.modifiedState=one.state.Clone();
            one.itemTypes=new List<string>(source.itemTypes);
            one.itemFeatures=new List<ItemFeature>(source.itemFeatures);
            one.unitCount=1;
            one.name=source.name;
            one.shortDescription=source.shortDescription;
            one.flavorText=source.flavorText;
            one.unitValue=source.unitValue;
            one.unitBaseValue=source.unitBaseValue;
            one.ValidateShapeState();
        }
        private static void ConsumeStackUnit(GameItem source)
        {
            source.SetUnitCount(source.unitCount-1);
            source.onChangeAmountItemFunc?.Invoke(source);
            var parent=source.parentInventory;
            if(parent==null) return;
            var slot=parent.GetIncompleteItemInventorySlot(source);
            source.onSlotChangeAmountItemFunc?.Invoke(source,parent,slot);
            parent.onSlotChangeAmountItemFunc?.Invoke(source,parent,slot);
        }
        public static void RefreshExistingMachines()
        {
            if(!ModEntry.Enabled || !Progress.Ready) return;
            foreach(var item in new List<GameItem>(EmporiumEntry.Instance.GetAllItems()))
                if(item.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG))
                { Attach(item); HydroponicControl.Attach(item); Remember(item,MachineHydroponic.GetInputSlot(item)?.childItem); TryReplant(item); }
        }
    }
}
