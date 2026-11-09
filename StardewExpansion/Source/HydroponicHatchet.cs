using System.Runtime.CompilerServices;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class HydroponicHatchet
    {
        private static readonly ConditionalWeakTable<GameItem,object> attached=new ConditionalWeakTable<GameItem,object>();

        public static GameItem MachineForPlant(GameItem target)
        {
            if(target==null || !target.IsTag(HydroponicHelper.SEED_ITEM_TAG) || target.parentInventory==null) return null;
            var machine=target.parentInventory.GetParentItem();
            if(machine==null || !machine.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG)
                || MachineHydroponic.GetInputSlot(machine)!=target.parentInventory
                || MachineHydroponic.GetInputSlot(machine).childItem!=target) return null;
            return HydroponicHelper.IsGrowingSeed(target) || HydroponicHelper.IsPerennialEstablished(target)
                || HydroponicControl.RequiresHatchet(target,machine)?machine:null;
        }

        public static bool CanClear(GameItem source,GameItem target)
        {
            if(!ModEntry.Enabled || source==null || source.identifier!="hatchet") return false;
            var machine=MachineForPlant(target);
            return machine!=null && GeneralHelper.IsItemOwned(source,target) && GeneralHelper.IsItemOwned(machine);
        }

        public static void Attach(GameItem item)
        {
            // Native directories assign the identifier after their factory has
            // returned, so it may still be null at this initialization point.
            if(item==null || attached.TryGetValue(item,out var _)) return;
            attached.Add(item,new object());
            // The native hatchet accepts the machine only. Add the seed-slot
            // target as well, retaining all native animal/fish/tool callbacks.
            var nativeMay=item.mayThisTargetItemFunc;
            var nativeCan=item.canThisTargetItemFunc;
            item.mayThisTargetItemFunc=(source,target)=>CanClear(source,target)
                || (nativeMay?.InvokeAllReduce(source,target,InvokeFuncExtensions.BoolReduceOR)??false);
            item.canThisTargetItemFunc=(source,target)=>CanClear(source,target)
                || (nativeCan?.InvokeAllReduce(source,target,InvokeFuncExtensions.BoolReduceOR)??false);
            item.onThisTargetItemFunc+=(source,target)=>{
                if(CanClear(source,target)) HydroponicHelper.RemovePlantFromHydroponic(MachineForPlant(target));
            };
        }
    }

    [HarmonyPatch(typeof(HydroponicHelper),nameof(HydroponicHelper.InitHatchetPlantRemoval))]
    public static class HydroponicHatchetPatch
    {
        public static void Postfix(GameItem item) { HydroponicHatchet.Attach(item); }
    }

    [HarmonyPatch(typeof(MachineHydroponic),nameof(MachineHydroponic.CreateMachineTooltip))]
    public static class StoppedHydroponicTooltipPatch
    {
        public static bool Prefix(RichTextBuilder builder,GameItem item)
        {
            if(!HydroponicControl.IsStopped(item)) return true;
            builder.AddLine(ModEntry.L("hydro_stopped"));return false;
        }
    }
}
