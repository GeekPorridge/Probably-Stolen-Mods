using System;

namespace PelicanCultivation
{
    public static class GiantHarvest
    {
        public static string MissTag(CropDefinition crop) => Rules.GiantMissPrefix+crop.Key.ToUpperInvariant();
        public static int Misses(GameItem machine,CropDefinition crop)
            => Math.Max(0,Math.Min(Rules.GiantPityMisses,machine.GetTagReadonly(MissTag(crop)).GetInt()));
        public static PlantHarvest.SizeSplit Resolve(GameItem machine,CropDefinition crop,int count,int care,int quality,int roll)
        {
            var split=crop.Sized?Rules.SplitCrop(count,care,quality):new PlantHarvest.SizeSplit{normal=Math.Max(0,count)};
            machine.DisableTag(Rules.LastGiantTag);
            if(!crop.Sized || !Rules.GiantEligible(count,care,quality)) return split;
            int misses=Misses(machine,crop);
            bool giant=Rules.RollGiant(misses,roll);
            Crops.SetInt(machine,MissTag(crop),giant?0:Math.Min(Rules.GiantPityMisses,misses+1));
            if(giant) Crops.SetInt(machine,Rules.LastGiantTag,Rules.Index(crop)+1);
            return Rules.WithGiant(split,giant);
        }
    }
}
