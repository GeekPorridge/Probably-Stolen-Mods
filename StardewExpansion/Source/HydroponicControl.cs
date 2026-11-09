using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class HydroponicControl
    {
        public const string StopTag="PELICAN_HYDRO_STOP_AFTER_HARVEST_BOOL";
        public const string RestingTag="PELICAN_RESTING_PERENNIAL_MACHINE_INT";
        private static readonly ConditionalWeakTable<GameItem,object> attached=new ConditionalWeakTable<GameItem,object>();
        public static bool StopRequested(GameItem machine) => ModEntry.Enabled && machine!=null
            && machine.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG) && GeneralHelper.IsItemOwned(machine)
            && machine.GetTagReadonly(StopTag).GetBool();
        public static bool IsStopped(GameItem machine)
        {
            if(!StopRequested(machine) || MachineProgressHelper.GetMachineState(machine)==MachineProgressHelper.STATE_WORKING) return false;
            var seed=MachineHydroponic.GetInputSlot(machine)?.childItem;
            // Stop AFTER a harvest: a manually inserted, unstarted seed still
            // gets one complete crop. Only an empty or resting machine waits.
            return seed==null || RequiresHatchet(seed,machine);
        }
        public static void SetStop(GameItem machine,bool stop)
        {
            if(!ModEntry.Enabled || machine==null || !machine.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG) || !GeneralHelper.IsItemOwned(machine)) return;
            machine.ModifyTag(StopTag,s=>s.Enable().SetBool(stop));
            if(!stop) AutoPlant.TryReplant(machine);
            machine.Validate();
        }
        public static void Attach(GameItem machine)
        {
            if(machine==null || !machine.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG) || attached.TryGetValue(machine,out var _)) return;
            attached.Add(machine,new object());
            var input=MachineHydroponic.GetInputSlot(machine);
            var nativeRemove=input.mayInventoryRemoveItemFunc;
            input.mayInventoryRemoveItemFunc=(seed,inventory)=>{
                // A paused perennial is still an established plant. Preserve
                // the native hatchet-only clearing rule instead of refunding
                // its seed merely because the machine is now ready.
                if(ModEntry.Enabled && RequiresHatchet(seed,machine)) return false;
                return nativeRemove?.Invoke(seed,inventory)??true;
            };
            input.onSlotRemoveItemFunc+=(seed,inventory,slot)=>{
                if(seed!=null && seed.IsTag(RestingTag) && seed.GetTagReadonly(RestingTag).GetInt()==machine.GetUniqueID()) ClearResting(seed);
            };
            // Gate the native nightly callback before it draws power/water or
            // starts the next crop. The native inventory indices stay intact.
            var native=machine.onCycleEndSlotItemFunc;
            machine.onCycleEndSlotItemFunc=(self,parent,slot)=>{
                if(!IsStopped(self)) native?.Invoke(self,parent,slot);
            };
            var row=machine.contentWindow?.childElement as MachineryPanelRow;
            if(row==null || row.GetPanel("pelican_cycle_control")!=null) return;
            // The native panel measures its children when attached. Supplying
            // the caption now avoids locking an empty button to a narrow width.
            var button=new MachineryButton(ModEntry.L("hydro_stop_off"));
            Action refresh=()=>{
                bool stop=machine.GetTagReadonly(StopTag).GetBool();
                // Native layout assigns a fixed width during ResizePixels.
                // Release it before measuring a new localized state caption.
                button.SetSize(-1);
                button.SetText(ModEntry.L(stop?"hydro_stop_on":"hydro_stop_off"));
                button.description=ModEntry.L("hydro_stop_hint")+"\n"+ModEntry.L("hydro_clear_hint");
                row.Validate();
            };
            button.SetCallback(()=>{SetStop(machine,!machine.GetTagReadonly(StopTag).GetBool());refresh();});
            row.Add(MachineHelper.Panel(ModEntry.L("hydro_stop_label"),button,"pelican_cycle_control"));
            machine.onLoaded+=self=>refresh();refresh();
        }
        public static void PausePerennial(GameItem seed,GameItem machine)
        {
            // Called only at the native post-harvest restart boundary. The
            // harvest already exists; keep the same parent, genes and identity.
            Crops.SetInt(seed,HydroponicHelper.PERENNIAL_ESTABLISHED_INT,1);
            Crops.SetInt(seed,RestingTag,machine.GetUniqueID());
            Crops.SetInt(seed,HydroponicHelper.GROWING_PROGRESS_INT,0);
            seed.DisableTag(HydroponicHelper.GROWING_TAG);
            seed.name=UngrowingName(seed.name);
            MachineProgressHelper.FinishProgressTypeMachine(machine);
            MachineHelper.UnlockModuleInv(machine);
            seed.Validate();machine.Validate();
        }
        private static string UngrowingName(string name)
        {
            const string marker="\uFFFC";
            string format=LocHelper.GetLocalizedMechanic("mech_hydroponic_growing_prefix",marker);
            int position=format.IndexOf(marker,StringComparison.Ordinal);
            if(position<0 || name==null) return name;
            string prefix=format.Substring(0,position),suffix=format.Substring(position+marker.Length);
            return name.Length>=prefix.Length+suffix.Length && name.StartsWith(prefix,StringComparison.Ordinal) && name.EndsWith(suffix,StringComparison.Ordinal)
                ?name.Substring(prefix.Length,name.Length-prefix.Length-suffix.Length):name;
        }
        public static bool CanResume(GameItem seed,GameItem machine) => seed!=null && machine!=null
            && seed.IsTag(RestingTag) && seed.GetTagReadonly(RestingTag).GetInt()==machine.GetUniqueID()
            && HydroponicHelper.IsPerennial(seed);
        public static bool RequiresHatchet(GameItem seed,GameItem machine) => seed!=null && machine!=null
            && seed.IsTag(RestingTag) && seed.GetTagReadonly(RestingTag).GetInt()==machine.GetUniqueID();
        public static void ResumePerennial(GameItem seed,GameItem machine)
        {
            seed.DisableTag(RestingTag);
            seed.EnableTag(HydroponicHelper.GROWING_TAG);
            Crops.SetInt(seed,HydroponicHelper.ASSOCIATED_HYDROPONIC_ID,machine.GetUniqueID());
            HydroponicHelper.StartNextFruitingCycle(seed,machine);
        }
        public static void ClearResting(GameItem seed)
        {
            seed.DisableTag(RestingTag);
            seed.DisableTag(HydroponicHelper.GROWING_TAG);
            seed.DisableTag(HydroponicHelper.ASSOCIATED_HYDROPONIC_ID);
            seed.DisableTag(HydroponicHelper.PERENNIAL_ESTABLISHED_INT);
            Crops.SetInt(seed,HydroponicHelper.GROWING_PROGRESS_INT,0);
        }
    }
    [HarmonyPatch(typeof(HydroponicHelper),nameof(HydroponicHelper.StartNextFruitingCycle))]
    public static class StopPerennialAfterHarvestPatch
    {
        public static bool Prefix(GameItem seed,GameItem hydroponic)
        {
            if(seed==null || !HydroponicControl.StopRequested(hydroponic)) return true;
            HydroponicControl.PausePerennial(seed,hydroponic);return false;
        }
    }
    [HarmonyPatch(typeof(HydroponicHelper),nameof(HydroponicHelper.StartGrowing))]
    public static class ResumeHydroponicPatch
    {
        public static bool Prefix(GameItem seed,GameItem hydroponic)
        {
            if(!ModEntry.Enabled || seed==null || !seed.IsTag(HydroponicControl.RestingTag)) return true;
            if(HydroponicControl.CanResume(seed,hydroponic))
            {HydroponicControl.ResumePerennial(seed,hydroponic);return false;}
            HydroponicControl.ClearResting(seed);return true;
        }
    }
    [HarmonyPatch(typeof(MachineHydroponic),nameof(MachineHydroponic.GetGrowStatus))]
    public static class StoppedHydroponicStatusPatch
    {
        public static bool Prefix(GameItem hydroponic,ref string __result)
        {
            if(!HydroponicControl.IsStopped(hydroponic)) return true;
            __result=ModEntry.L("hydro_stopped");return false;
        }
    }
}
