using System;

namespace PelicanCultivation
{
    public static class FarmPresentation
    {
        public static bool TryMeat(string identifier,out string species,out int tierCalories)
        {
            foreach(string candidate in new[]{FarmRules.Cow,FarmRules.Pig,FarmRules.Lobster})
                foreach(int amount in FarmRules.CutCalories(candidate))
                    if(FarmSpecies.MeatId(candidate,amount)==identifier)
                    {species=candidate;tierCalories=amount;return true;}
            species=null;tierCalories=0;return false;
        }
        public static void Text(GameItem item,string key)
        {
            item.shortDescription=ModEntry.L("farm_"+key+"_desc");
            item.flavorText=ModEntry.L("farm_"+key+"_flavor");
        }
        public static void RefreshFood(GameItem item)
        {
            if(item==null) return;
            if(TryMeat(item.identifier,out string species,out int tier))
            {
                // The saved cut identifier fixes its footprint/name, even when
                // another system has reduced its remaining edible calories.
                item.name=ModEntry.L("farm_"+item.identifier.Substring(8));
                item.SetGameItemType("FOOD");
                if(tier==900) item.SetGameItemType("LUXURY_ITEM");
                else item.RemoveGameItemType("LUXURY_ITEM");
                if(tier==100) {item.shortDescription="";item.flavorText="";}
                else Text(item,species==FarmRules.Cow?"beef":species==FarmRules.Pig?"pork":"lobster_meat");
                int calories=Math.Max(0,item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt());
                item.unitValue=calories==0?0:FarmRules.MeatPrice(species,calories);
            }
            else
            {
                string key;
                switch(item.identifier)
                {
                    case "pelican_milk":key="milk";break;
                    case "pelican_truffle_small":key="truffle_small";break;
                    case "pelican_truffle":key="truffle";break;
                    case "pelican_sea_cucumber_food":key="sea_cucumber_food";break;
                    default:return;
                }
                item.name=ModEntry.L("farm_"+key);Text(item,key);
                if(key=="truffle_small" || key=="truffle")
                {
                    int calories=Math.Max(0,item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt());
                    int fullCalories=key=="truffle_small"?125:300;
                    item.unitValue=calories==0?0:Math.Max(1,(long)Math.Round(FarmRules.TrufflePrice(key=="truffle_small")*(double)calories/fullCalories));
                }
            }
            // Save data may override factory names, types and values. Refresh
            // only these fields; nutrition, ownership and disease stay intact.
            item.Validate();
        }
    }
}
