namespace PelicanCultivation
{
    public static class BeverageStorage
    {
        public static bool IsBeer(GameItem item)
        {
            if(item==null || !item.IsTag(Rules.WineTag) || !WineHelper.IsFinishedWine(item)) return false;
            int kind=item.GetTagReadonly(Rules.WineTag).GetInt();
            return kind==1 || kind==2;
        }
        public static bool CanChill(GameItem item)
            => IsBeer(item) && !item.IsTag(WineHelper.WINE_COUNTERFEIT_TAG);
        public static bool CanEnterFridge(GameItem item)
            => CanChill(item) && GeneralHelper.IsItemOwned(item)
                && !item.IsGameItemType("MACHINE") && !item.IsGameItemType("STORAGE");
        public static void ClearChilled(GameItem item)
        {
            // The native empty-bottle reset predates chilled wine bottles and
            // does not remove this feature. Do so before clearing our kind tag.
            item.DisableTag(MachineFridge.CHILLED_KEPT_COLD_TAG);
            item.RemoveItemFeatureByID(MachineFridge.CHILLED_FEATURE_ID);
        }
    }
}
