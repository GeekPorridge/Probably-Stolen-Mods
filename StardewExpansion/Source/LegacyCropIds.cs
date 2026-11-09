using System;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class LegacyCropIds
    {
        // Historical IDs are load aliases, rather than catalog entries. The
        // native save decoder requests Item before restoring saved instance
        // state; size/value migration then runs through the normal load hook.
        public static string Current(string identifier)
        {
            if(identifier==null || !identifier.StartsWith("pelican_",StringComparison.Ordinal)) return identifier;
            foreach(var crop in Rules.Crops)
            {
                if((crop.Sized || crop.Key=="parsnip") && identifier==crop.ProduceId+"_large") return crop.ProduceId;
                if(crop.Key=="parsnip" && identifier==crop.ProduceId+"_small") return crop.ProduceId;
            }
            return identifier;
        }
    }
    [HarmonyPatch(typeof(DirectoryMaster),nameof(DirectoryMaster.Item),new[]{typeof(string),typeof(bool)})]
    public static class LegacyCropItemPatch
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(ref string identifier)
        {if(ModEntry.Enabled)identifier=LegacyCropIds.Current(identifier);}
    }
}
