using HarmonyLib;

namespace PelicanCultivation
{
    [HarmonyPatch(typeof(MachineFridge),nameof(MachineFridge.MayZoneAcceptItem))]
    public static class BeerFridgeAcceptancePatch
    {
        public static bool Prefix(GameItem item,ref bool __result)
        {
            if(!ModEntry.Enabled || !BeverageStorage.CanChill(item)) return true;
            __result=BeverageStorage.CanEnterFridge(item);return false;
        }
    }
    [HarmonyPatch(typeof(MachineFridge),nameof(MachineFridge.IsChillable))]
    public static class BeerChillablePatch
    {
        public static void Postfix(GameItem item,ref bool __result)
        { if(ModEntry.Enabled && BeverageStorage.CanChill(item)) __result=true; }
    }
    [HarmonyPatch(typeof(MachineWineRack),nameof(MachineWineRack.CanAgeWine))]
    public static class BeerRackPatch
    {
        public static void Postfix(GameItem wine,ref bool __result)
        { if(ModEntry.Enabled && BeverageStorage.IsBeer(wine)) __result=false; }
    }
    [HarmonyPatch(typeof(AgableHelper),nameof(AgableHelper.IsAgable))]
    public static class BeerAgablePatch
    {
        public static void Postfix(GameItem item,ref bool __result)
        { if(ModEntry.Enabled && BeverageStorage.IsBeer(item)) __result=false; }
    }
    [HarmonyPatch(typeof(AgableHelper),nameof(AgableHelper.OnAge))]
    public static class BeerAgePatch
    {
        // Also covers beer already inside an old saved rack and direct aging
        // callbacks. Historical age and value are left intact.
        public static bool Prefix(GameItem item) => !ModEntry.Enabled || !BeverageStorage.IsBeer(item);
    }
    [HarmonyPatch(typeof(WineHelper),nameof(WineHelper.CreateTooltip))]
    public static class BeerWineTooltipPatch
    {
        // For finished beer this native section contains only aging help and
        // developer scores. The late mod tooltip supplies the fridge help.
        public static bool Prefix(GameItem item) => !ModEntry.Enabled || !BeverageStorage.CanChill(item);
    }
    [HarmonyPatch(typeof(WineHelper),"CanBottleSourceTarget")]
    public static class CustomWineTargetPatch
    {
        public static bool Prefix(GameItem s,GameItem t,ref bool __result)
        {
            if(!WineTransferProtection.Blocks(s,t)) return true;
            __result=false;return false;
        }
    }
    [HarmonyPatch(typeof(WineHelper),nameof(WineHelper.PourWineIntoContainer))]
    public static class CustomWinePourPatch
    {
        public static bool Prefix(GameItem bottle,GameItem container) => !WineTransferProtection.Blocks(bottle,container);
    }
    [HarmonyPatch(typeof(WaterHelper),nameof(WaterHelper.TransferLiquid))]
    public static class CustomWineLiquidTransferPatch
    {
        public static bool Prefix(GameItem sourceContainer,GameItem targetContainer) => !WineTransferProtection.Blocks(sourceContainer,targetContainer);
    }
    [HarmonyPatch(typeof(ModHook),"FireOnCreateTooltipLate")]
    public static class CropModInfoPatch
    {
        // Native seed/food/genetics rows are built outside this hook. Suppress
        // only late mod-information callbacks for requested crops/farm items
        // and hydroponics; native machine status/power/water rows stay intact.
        public static bool Prefix(GameItem item) => !ModEntry.Enabled || !TooltipPolicy.HideModInfo(item);
    }
    [HarmonyPatch(typeof(HydroponicHelper),nameof(HydroponicHelper.ContinueGrowing))]
    public static class AutoReplantHarvestPatch
    {
        public static void Prefix(GameItem growingSeed,ref int progress,GameItem hydroponic)
        {
            if(!ModEntry.Enabled) return;
            CropCare.ClampGrowingProgress(growingSeed,ref progress);
            AutoPlant.Remember(hydroponic,growingSeed);
        }
        public static void Postfix(GameItem hydroponic) { AutoPlant.TryReplant(hydroponic); }
    }
    [HarmonyPatch(typeof(GameItemElement),nameof(GameItemElement.Validate))]
    public static class ItemHitSurfacePatch
    {
        public static void Postfix(GameItemElement __instance) { if(ModEntry.Enabled) Crops.RepairHitSurface(__instance); }
    }
    [HarmonyPatch(typeof(MachineHydroponic),nameof(MachineHydroponic.Hydroponic))]
    public static class HydroponicPatch
    {
        public static void Postfix(GameItem __result) { if(ModEntry.Enabled && __result!=null) Crops.AttachHydroponic(__result); }
    }
    [HarmonyPatch(typeof(WineHelper),nameof(WineHelper.InitWineBottle))]
    public static class BottlePatch
    {
        public static void Postfix(GameItem item) { if(ModEntry.Enabled) Brewing.AttachBottle(item); }
    }
    [HarmonyPatch(typeof(WineHelper),nameof(WineHelper.TryStartFermentation))]
    public static class TryStartPatch
    {
        public static bool Prefix(GameItem item,ref bool __result)
        {
            if(!ModEntry.Enabled || (BottleGeometry.EnsureEmptyRoom(item,true) && Brewing.ValidateStart(item,true))) return true;
            __result=false; return false;
        }
    }
    [HarmonyPatch(typeof(WineHelper),nameof(WineHelper.StartFermentation))]
    public static class StartPatch
    {
        public static bool Prefix(GameItem item,out bool __state)
        {
            __state=ModEntry.Enabled && item.GetTagReadonly(WineHelper.WINE_CONTAINER_STATE).GetInt()==0;
            return !__state || (BottleGeometry.EnsureEmptyRoom(item,false) && Brewing.ValidateStart(item,false));
        }
        public static void Postfix(GameItem item,bool __state) { if(__state && ModEntry.Enabled) Brewing.AfterStart(item); }
    }
    [HarmonyPatch(typeof(WineHelper),nameof(WineHelper.FinishFermentation))]
    public static class FinishPatch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(GameItem item) { if(ModEntry.Enabled) Brewing.RefreshFinished(item,true); }
    }
    [HarmonyPatch(typeof(GameItem),nameof(GameItem.GetDisplayName))]
    public static class BrewDisplayNamePatch
    {
        // Loaded bottles retain the native wine_bottle identifier. Do not let
        // a native/default name replace the custom drink represented by its tags.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(GameItem __instance,bool preferCustomName,ref string __result)
        {
            if(!ModEntry.Enabled || (preferCustomName && GeneralHelper.HasCustomName(__instance))) return;
            string name=Brewing.FinishedName(__instance);if(name!=null) __result=name;
        }
    }
    [HarmonyPatch(typeof(WineHelper),nameof(WineHelper.ResetToEmptyBottle))]
    public static class EmptyBottlePatch
    {
        public static void Prefix(GameItem item,out bool __state)
        {
            if(ModEntry.Enabled)Barrel.Clear(item);
            __state=ModEntry.Enabled && Rules.WideBottle(item.GetTagReadonly(Rules.WineTag).GetInt());
            if(ModEntry.Enabled&&BottleGeometry.IsBeer(item)) BeverageStorage.ClearChilled(item);
            if(ModEntry.Enabled)Brewing.RestoreNativeBottleId(item);
            if(ModEntry.Enabled) item.DisableTag(Rules.SizeScoreTag);
            if(!ModEntry.Enabled || !item.IsTag(Rules.WineTag)) return;
            // The native setter does not clear this flag when restoring its atlas.
            item.useSpriteFromMod=false;
            item.DisableTag(Rules.WineTag);
        }
        public static void Postfix(GameItem item,bool __state) { if(ModEntry.Enabled) BottleGeometry.AfterEmpty(item,__state); }
    }
    [HarmonyPatch(typeof(ScavHelper),nameof(ScavHelper.SpawnDumpingGroundLoot))]
    public static class AncientLootPatch
    {
        public static void Postfix(ref GameItem __result) { if(ModEntry.Enabled) Discovery.MaybeReplace(ref __result); }
    }
    [HarmonyPatch(typeof(StoreClientInstance),nameof(StoreClientInstance.IsCurrentClientStillHaveThingToSell))]
    public static class StockLeftPatch
    {
        public static void Postfix(ref bool __result)
        { if(ModEntry.Enabled && Merchant.IsPresent && Merchant.HasPendingStock) __result=true; }
    }
    [HarmonyPatch(typeof(StoreClientManager),nameof(StoreClientManager.TrackClientVisit))]
    public static class FarmerVisitPatch
    {
        public static void Postfix(StoreClient storeClient) { if(ModEntry.Enabled) Merchant.OnVisit(storeClient); }
    }
}
