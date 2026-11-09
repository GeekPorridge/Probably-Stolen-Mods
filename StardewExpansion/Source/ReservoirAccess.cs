using System;
using System.Linq;
using HarmonyLib;
using TMPro;

namespace PelicanCultivation
{
    public static class ReservoirAccess
    {
        public static bool IsBait(GameItem item)=>item!=null&&item.unitCount>0&&GeneralHelper.IsItemOwned(item)
            &&(item.identifier=="pelican_bait"||item.identifier=="pelican_premium_bait");
        public static int CountInside(GameItem backpack)
        {
            if(backpack==null||backpack.unitCount<1||!GeneralHelper.IsItemOwned(backpack)||!backpack.IsTag("BACKPACK_TAG"))return 0;
            // Start at this exact backpack. Ground, store stock and side pockets
            // are never roots; owned inner bags remain part of its contents.
            long count=GraphUtils.FindAllChildrenType<GameItem>(backpack,IsBait,item=>item!=null&&item.unitCount>0&&GeneralHelper.IsItemOwned(item)).Sum(item=>(long)item.unitCount);
            return (int)Math.Min(int.MaxValue,count);
        }
        public static GameItem DepartureBackpack(EmporiumEntry entry,bool alreadyOutside)
        {
            if(ReferenceEquals(entry,null))return null;
            if(alreadyOutside)return entry.afterhourPocketSlotInvBackpack?.childItem;
            // MapUIManager.Travel checks availability BEFORE OnGoingOutside
            // transfers selectedBackpack into the afterhour backpack slot.
            var selected=entry.selectedBackpack;
            return selected?.parentInventory==null?null:selected;
        }
        public static int CountForDeparture(EmporiumEntry entry,bool alreadyOutside)=>CountInside(DepartureBackpack(entry,alreadyOutside));
        public static bool HasBaitForDeparture()
        {
            if(!Progress.Ready)return false;
            var map=MapUIManager.Instance;
            return CountForDeparture(EmporiumEntry.Instance,!ReferenceEquals(map,null)&&map.Current!=null)>0;
        }
        public static string TransitStatus()=>Expansion.L("reservoir_bait_required");
        public static void ExplainBlockedDeparture()=>ModEntry.Message("v2_reservoir_bait_required_hint");
    }

    [HarmonyPatch(typeof(MapUIManager),nameof(MapUIManager.Travel))]
    public static class ReservoirDeparturePatch
    {
        public static bool Prefix(MapUIManager __instance,AfterhourLocation location)
        {
            if(!ModEntry.Enabled||location?.Id!=Fishing.LocationId||__instance.IsInTransit)return true;
            if(!ReservoirAccess.HasBaitForDeparture()){ReservoirAccess.ExplainBlockedDeparture();return false;}
            return true;
        }
    }
    [HarmonyPatch(typeof(TransitUI),"BuildCard")]
    public static class ReservoirTransitStatusPatch
    {
        public static void Postfix(AfterhourLocation location,TransitCardView __result)
        {
            if(!ModEntry.Enabled||location?.Id!=Fishing.LocationId||__result==null||ReservoirAccess.HasBaitForDeparture())return;
            // The installed native location API has no virtual status provider.
            // Replace only this card's existing unavailable caption; native
            // events, layout, locked state and every other card stay intact.
            var text=__result.transform.Find("Status")?.GetComponent<TextMeshProUGUI>();
            if(text!=null){text.text=ReservoirAccess.TransitStatus();WildNetworkUI.AutoSize(text,12);}
        }
    }
}
