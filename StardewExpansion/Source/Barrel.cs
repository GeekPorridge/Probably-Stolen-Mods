using System;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class Barrel
    {
        public const string Nights="PELICAN_BARREL_NIGHTS",OriginTier="PELICAN_BARREL_ORIGIN_TIER",Bonus="PELICAN_BARREL_BONUS",LastNight="PELICAN_BARREL_LAST_NIGHT";
        public const string Star="PELICAN_BARREL_STAR";
        public const int MaxBottles=10;
        public static bool IsWholeBottle(GameItem item)=>WineHelper.IsFinishedWine(item)&&(!item.IsTag(UseCountHelper.USE_COUNT_ITEM_TAG)||item.GetTagReadonly(UseCountHelper.CURRENT_USE_COUNT_INT).GetInt()==item.GetTagReadonly(UseCountHelper.MAX_USE_COUNT_INT).GetInt());
        public static bool Accepts(GameItem item)=>item!=null&&IsWholeBottle(item)&&!item.IsTag(WineHelper.WINE_COUNTERFEIT_TAG)&&!BeverageStorage.IsBeer(item)&&item.GetTagReadonly(Rules.WineTag).GetInt()!=6;
        public static int NativeCompletion(int tier)=>tier>=5?0:tier>=4?8:14;
        public static int Completion(int tier)=>NativeCompletion(tier)+32;
        public static int StarAt(int initial,int nights)
        {
            int elapsed=nights-NativeCompletion(initial);
            return elapsed>=32?3:elapsed>=17?2:elapsed>=7?1:0;
        }
        public static int StarBonus(int star)=>star>=3?50:star==2?35:star==1?25:0;
        public static int TargetTier(int initial,int nights)=>initial>=5?5:initial>=4?(nights>=8?5:initial):nights>=14?5:nights>=6?4:initial;
        public static GameItem Create()
        {
            var grid=new GameGridInventory(MaxBottles,4);
            var item=ItemDirectory.CreateEmptyItem("pelican_barrel").SetContentWindow(MachineHelper.CreateMachineryWindow(true,MachineHelper.Panel(Expansion.L("barrel_storage"),grid,"barrel_storage")));
            Crops.Sprite(item,"barrel",3,3);item.name=Expansion.L("barrel");item.shortDescription=Expansion.L("barrel_desc");item.unitValue=450;item.SetGameItemType("MACHINE");item.EnableTag("CONTAINER_TAG");
            grid.mayInventoryAddItemFunc=(bottle,inv)=>CanAdd(item,grid,bottle);
            item.mayStackItemFunc=(a,b)=>false;MachineTrade.Refresh(item);item.onLoaded+=self=>{Resize(self);MachineTrade.Refresh(self);};
            item.onCycleEndSlotItemFunc=(self,parent,slot)=>{if(!ModEntry.Enabled||!Progress.Ready||!GeneralHelper.IsItemOwned(self))return;foreach(var bottle in new System.Collections.Generic.List<GameItem>(grid.childItems))TickBottle(bottle,Progress.Day);};
            return item;
        }
        public static bool CanAdd(GameItem barrel,GameGridInventory grid,GameItem bottle)=>GeneralHelper.IsItemOwned(barrel)&&GeneralHelper.IsItemOwned(bottle)&&Accepts(bottle)&&bottle.unitCount==1&&grid.childItems.Count<MaxBottles;
        public static void Resize(GameItem item)
        {
            var grid=MachineHelper.GetBodyStorage(item,0) as GameGridInventory;
            if(grid!=null&&(grid.inventoryShape.width<MaxBottles||grid.inventoryShape.height<4)){grid.SetShape(MaxBottles,4);item.contentWindow?.childElement?.Validate();}
        }
        public static void TickBottle(GameItem bottle,int day)
        {
            if(!Accepts(bottle)||bottle.GetTagReadonly(Star).GetInt()>=3||bottle.GetTagReadonly(LastNight).GetInt()==day)return;
            Crops.SetInt(bottle,LastNight,day);
            int initial=bottle.IsTag(OriginTier)?bottle.GetTagReadonly(OriginTier).GetInt():WineHelper.GetWineQualityTier(bottle);
            Crops.SetInt(bottle,OriginTier,initial);int nights=bottle.GetTagReadonly(Nights).GetInt()+1;Crops.SetInt(bottle,Nights,nights);
            int target=TargetTier(initial,nights);
            if(target>WineHelper.GetWineQualityTier(bottle))
            {
                int score=target>=5?6:5;Crops.SetInt(bottle,WineHelper.WINE_SCORE_INT,score);bottle.RemoveFeatureByCategory("CATEGORY_WINE_QUALITY");bottle.AddItemFeature(ItemFeatureList.CreateWineQualityFeature(score));
                if(bottle.IsTag(Rules.WineTag))Brewing.RefreshFinished(bottle,false);
                else
                {
                    WineHelper.InitDrinkableWine(bottle,4+target);
                    bottle.SetGameItemType("SUBSTANCE");bottle.SetGameItemType("ALCOHOL");if(target>=4)bottle.SetGameItemType("LUXURY_ITEM");
                    CropGeometry.TryApplyShape(bottle,()=>bottle.SetSpriteAndShape("Items/items_winemaking","bottle"+target),i=>i.parentInventory==null||i.parentInventory.CheckInventorySlotStillValid(i,i.modifiedShape,i.modifiedState));
                }
            }
            int star=StarAt(initial,nights);if(star>bottle.GetTagReadonly(Star).GetInt())Crops.SetInt(bottle,Star,star);
            RefreshQuality(bottle);
            bottle.Validate();
        }
        public static void RefreshQuality(GameItem bottle)
        {
            int star=bottle.GetTagReadonly(Star).GetInt();
            if(star<=0||!Accepts(bottle))return;
            star=Math.Min(3,star);int bonus=StarBonus(star);
            // Replace the previous star (or r4's +10% barrel character), never compound it.
            bottle.RemoveFeatureByCategory(Bonus);bottle.EnableTag(Bonus);
            bottle.AddItemFeature(new ItemFeature(Bonus){identifier="pelican_barrel_bonus",isFeatureMatch=true,valueStage=ItemFeature.ValueStage.Final}.SetPublicDisplay(Expansion.L("barrel_star_"+star)).SetActualDisplay(Expansion.L("barrel_star_"+star)).SetValueModifier(bonus).SetPreExposedValueMod(bonus));
        }
        public static void Clear(GameItem bottle)
        {
            foreach(string tag in new[]{Nights,OriginTier,Bonus,LastNight,Star})bottle.DisableTag(tag);
            bottle.RemoveFeatureByCategory(Bonus);
        }
    }
}
