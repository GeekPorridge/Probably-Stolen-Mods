namespace PelicanCultivation
{
    public static class TooltipPolicy
    {
        public static bool HideModInfo(GameItem item)
        {
            if(item==null) return false;
            if(item.IsTag(WineHelper.WINE_CONTAINER_TAG)||item.IsTag(Rules.WineTag)||item.IsGameItemType("ALCOHOL"))return true;
            // The native machine section already shows status, power and water.
            // Do not add the late care/breeding appendix to hydroponic machines.
            if(item.IsTag(MachineHydroponic.HYDROPONIC_MACHINE_TAG) || item.identifier=="hydroponic") return true;
            if(HideCropInfo(item)) return true;
            if(FarmRules.Mammal(item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString()) || FarmRules.Aquatic(item.GetTagReadonly("FISH_SPECIES_TAG").GetString())) return true;
            if(FarmPresentation.TryMeat(item.identifier,out _,out _)) return true;
            return item.identifier=="pelican_milk" || item.identifier=="pelican_truffle_small" || item.identifier=="pelican_truffle" || item.identifier=="pelican_sea_cucumber_food";
        }
        public static bool HideCropInfo(GameItem item)
        {
            if(item==null) return false;
            var seed=Rules.BySeed(item.identifier);
            if(seed!=null || item.IsGameItemType("SEED")) return true;
            int index=Crops.Index(item);
            if(index>=0 && index<Rules.Crops.Length) return true;
            foreach(var plant in PlantRegistry.All)if(plant.id==item.identifier || plant.harvestItemId==item.identifier)return true;
            return false;
        }
    }
}
