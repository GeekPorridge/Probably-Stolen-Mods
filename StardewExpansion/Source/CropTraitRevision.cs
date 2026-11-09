using System.Linq;

namespace PelicanCultivation
{
    public static class CropTraitRevision
    {
        public const string Processed="PELICAN_V2_TRAITS_REVIEWED";
        public static void Migrate(GameItem seed,CropDefinition crop)
        {
            int index=Rules.Index(crop);if(index<8||seed.IsTag(Processed))return;
            seed.EnableTag(Processed);
            // Only the exact unedited r2 factory strain is upgraded. Extracted,
            // added or ranked genes, genetic heat and growing state are retained.
            string old=index<=9?"perennial":index==10?"hardy":"delicious";
            var traits=PlantGeneticsHelper.GetTraits(seed);
            if(PlantGeneticsHelper.GetHeat(seed)>0||traits.Count!=1||traits[0].trait.id!=old||traits[0].rank!=1)return;
            foreach(var pair in crop.Traits)PlantGeneticsHelper.SetTraitRank(seed,pair.Key,pair.Value);
        }
    }
}
