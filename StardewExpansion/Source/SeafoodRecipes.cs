using System;
using System.Collections.Generic;
using System.Linq;

namespace PelicanCultivation
{
    public static class SeafoodRecipes
    {
        public static readonly string[] Keys={"baked_lobster","scallion_sea_cucumber","sashimi_platter"};
        public const string BalanceTag="PELICAN_SEAFOOD_BALANCE";
        public const int BalanceVersion=6;
        public const double EnergyFactor=1.2;
        public static long BasePrice(string key)=>key=="baked_lobster"?350:key=="scallion_sea_cucumber"?200:key=="sashimi_platter"?600:0;
        public static int CookedCalories(int rawCalories)=>rawCalories<=0?0:(int)Math.Min(int.MaxValue,Math.Max(1,Math.Round(rawCalories*EnergyFactor)));
        private static readonly string[] PlatterFish={"tuna","lava_eel","midnight_carp","largemouth_bass"};
        public static void RegisterItems(ModItemDirectory directory)
        {foreach(string name in Keys){string key=name;directory.Add("pelican_"+key,()=>Create(key,900));}}
        public static bool IsDish(GameItem item)=>item!=null&&Array.IndexOf(Keys,item.identifier?.StartsWith("pelican_")==true?item.identifier.Substring(8):"")>=0;
        private static bool RawFood(GameItem item,string species)=>item!=null&&item.unitCount>0&&!item.IsTag("PELICAN_COOK_STYLE")&&!IsDish(item)&&FoodItemHelper.IsMeatFood(item)&&item.GetTagReadonly("MEAT_TYPE_TAG").GetString()==species&&item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()>0;
        public static bool WholeFish(GameItem item,string key)=>item!=null&&item.unitCount>0&&item.identifier=="pelican_"+key&&AquacultureHelper.IsFish(item)&&!item.IsTag("FISH_ROE_TAG")&&!item.IsTag("FISH_FRY_TAG")&&FishCalories(item)>0;
        public static int FishCalories(GameItem fish)=>Math.Max(0,AquacultureHelper.IsDead(fish)?fish.GetTagReadonly("FISH_CARCASS_KCAL_TAG").GetInt():AquacultureHelper.GetMeatKcal(fish));
        public static bool Accepts(GameItem item)=>RawFood(item,FarmRules.Lobster)||RawFood(item,FarmRules.Sea)||WholeFish(item,"sea_cucumber")||PlatterFish.Any(key=>WholeFish(item,key));
        public static void Plan(ArtisanPlan plan,int recipe,IList<GameItem> items)
        {
            plan.Nights=1;
            if(recipe==8)
            {
                var fish=PlatterFish.Select(key=>items.FirstOrDefault(item=>WholeFish(item,key))).ToList();
                if(fish.Any(item=>item==null))return;
                foreach(var item in fish)plan.Units[item]=1;
                int calories=fish.Sum(FishCalories);
                var dish=Create("sashimi_platter",calories);if(fish.Any(AquacultureHelper.IsPelletTainted))GutterflowPelletHelper.ApplyTrashGrade(dish);plan.Products.Add(dish);
            }
            else
            {
                bool lobster=recipe==6;string species=lobster?FarmRules.Lobster:FarmRules.Sea;
                foreach(var raw in items.Where(item=>RawFood(item,species)||(!lobster&&WholeFish(item,"sea_cucumber"))).Take(4))
                {
                    bool whole=AquacultureHelper.IsFish(raw);int calories=whole?FishCalories(raw):raw.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();
                    plan.Units[raw]=1;var dish=Create(lobster?"baked_lobster":"scallion_sea_cucumber",calories);if(whole&&AquacultureHelper.IsPelletTainted(raw))GutterflowPelletHelper.ApplyTrashGrade(dish);plan.Products.Add(dish);
                }
            }
            plan.Valid=plan.Products.Count>0;
        }
        public static GameItem Create(string key,int rawCalories)
        {
            int calories=CookedCalories(rawCalories);long price=BasePrice(key);
            var item=ItemDirectory.CreateEmptyItem("pelican_"+key);Crops.Sprite(item,key,3,2);
            Expansion.RegisterFood(item,key,calories,price);FoodItemHelper.InitFoodItem(item,calories,false,true);
            Crops.SetInt(item,BalanceTag,BalanceVersion);
            FarmSpecies.SetString(item,"MEAT_TYPE_TAG",key=="baked_lobster"?FarmRules.Lobster:key=="scallion_sea_cucumber"?FarmRules.Sea:"FISH_MEAT");
            FarmSpecies.SetString(item,"PELICAN_COOK_STYLE",key);item.onLoaded+=Refresh;Refresh(item);return item;
        }
        public static void Refresh(GameItem item)
        {
            if(!IsDish(item))return;string key=item.identifier.Substring(8);
            if(!item.IsTag(BalanceTag)||item.GetTagReadonly(BalanceTag).GetInt()<BalanceVersion)
            {
                // Apply the cooking bonus to r5's saved ingredient energy once.
                int calories=CookedCalories(item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt());
                Crops.SetInt(item,Expansion.CaloriesTag,calories);
                Crops.SetInt(item,"CALORIE_VALUE_TAG",calories);
                Crops.SetInt(item,BalanceTag,BalanceVersion);
            }
            Crops.SetInt(item,Expansion.PriceTag,(int)BasePrice(key));
            item.name=Expansion.L(key);item.shortDescription=Expansion.L(key+"_desc");item.flavorText="";
            item.SetGameItemType("FOOD");if(key!="scallion_sea_cucumber")item.SetGameItemType("LUXURY_ITEM");else item.RemoveGameItemType("LUXURY_ITEM");
            item.unitValue=BasePrice(key);
        }
    }
}
