using HarmonyLib;

namespace PelicanCultivation
{
    public static class SeaFounderPair
    {
        public const string BatchTag=FarmRules.Prefix+"SEA_FOUNDER_PAIR";
        public const string PurchasedKey="sea_founder_pair_purchased",EstablishedKey="sea_founder_pair_established";
        public static void Purchased(GameItem item,int cost,StoreClient client)
        {
            if(!ModEntry.Enabled || !Progress.Ready || item==null || cost<=0 || client?.identifier!=Merchant.Id || item.identifier!="pelican_sea_cucumber_roe") return;
            Claim(item);
        }
        private static void Claim(GameItem item)
        {
            if(Progress.Get(PurchasedKey)!=0 || Progress.Get(EstablishedKey)!=0) return;
            Crops.SetInt(item,BatchTag,1);Progress.Set(PurchasedKey,1);
        }
        public static void RecoverPurchasedBatch(GameItem item,FishSpecies species)
        {
            // Older versions already kept the native purchase-price tag on roe
            // and carried it into grouped larvae. No player save is edited.
            if(!Progress.Ready || species?.speciesID!=FarmRules.Sea || !GeneralHelper.IsItemOwned(item) || (!item.IsTag("FISH_ROE_TAG") && !item.IsTag("FISH_FRY_TAG"))) return;
            if(item.IsTag("BOUGHT_PRICE_TAG") && item.GetTagReadonly("BOUGHT_PRICE_TAG").GetInt()>0) Claim(item);
        }
        public static bool Guarantees(GameItem source,FishSpecies species,int count,bool hatch)
            => !hatch && Progress.Ready && species?.speciesID==FarmRules.Sea && count>=2 && source.IsTag(BatchTag) && GeneralHelper.IsItemOwned(source) && Progress.Get(EstablishedKey)==0;
        public static void PrepareIndividual(GameItem child,int index,bool guarantee)
        {
            child.DisableTag(BatchTag);
            if(guarantee && index<2) FarmSpecies.SetString(child,"FISH_SEX_TAG",index==0?"MALE":"FEMALE");
        }
        public static void Commit()
        {Progress.Set(EstablishedKey,1);}
    }
    [HarmonyPatch(typeof(RunStats),nameof(RunStats.OnItemBought))]
    public static class SeaFounderPurchasePatch
    {
        public static void Postfix(GameItem item,int cost,StoreClient client) => SeaFounderPair.Purchased(item,cost,client);
    }
}
