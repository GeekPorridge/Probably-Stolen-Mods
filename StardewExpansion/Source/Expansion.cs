using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class Expansion
    {
        public const string KindTag="PELICAN_V2_PRODUCT",PriceTag="PELICAN_V2_FULL_PRICE",CaloriesTag="PELICAN_V2_FULL_KCAL";
        public static readonly string[] MeatKeys={"chicken_meat","goat_meat"};
        public static readonly string[] CookKeys={"chicken","goat","beef","pork","fish"};
        public static readonly string[] ProductKeys={"egg","goat_milk","mayonnaise","cheese","goat_cheese","truffle_oil","poppy_perfume","fairy_perfume","bait","premium_bait"};
        public static readonly int[] ProductPrices={18,60,30,70,100,150,100,160,5,12};
        public static readonly int[] ProductCalories={150,400,180,350,400,200,0,0,0,0};
        public static bool Flower(CropDefinition crop)=>crop!=null && (crop.Key=="poppy" || crop.Key=="fairy_rose");
        public static bool FlowerId(string id)=>id=="pelican_poppy" || id=="pelican_fairy_rose";
        public static string L(string key,params object[] args)=>ModEntry.L("v2_"+key,args);
        public static void Enable()
        {
            ExpansionStock.Register();
            ModHook.OnModItemDirectoryInit+=RegisterItems;
            ModHook.OnModItemDirectoryInit+=ExpansionStock.RegisterItems;
            Fishing.Enable();
        }
        public static void Disable()
        {
            ModHook.OnModItemDirectoryInit-=RegisterItems;
            ModHook.OnModItemDirectoryInit-=ExpansionStock.RegisterItems;
            Fishing.Disable();
            Books.CloseAll();
        }
        public static void RegisterItems(ModItemDirectory directory)
        {
            for(int i=1;i<Rules.WineKeys.Length;i++){int kind=i;directory.Add("pelican_"+Rules.WineKeys[kind],()=>Brewing.DebugFinished(kind));}
            for(int i=0;i<ProductKeys.Length;i++){int index=i;directory.Add("pelican_"+ProductKeys[i],()=>Product(index));}
            foreach(string key in MeatKeys)foreach(int kcal in new[]{100,400,900}){string k=key;int c=kcal;directory.Add("pelican_"+key+CutSuffix(c),()=>RawMeat(k,c));}
            foreach(string key in CookKeys){string k=key;directory.Add("pelican_cooked_"+k,()=>Cooked(k,900,k=="chicken"?120:k=="goat"?185:k=="beef"?165:k=="pork"?190:150,"plain"));}
            foreach(string key in new[]{"plant_manual","animal_manual","artisan_manual"}){string k=key;directory.Add("pelican_"+k,()=>Books.Item(k));}
            ArtisanMachines.RegisterItems(directory);
        }
        public static bool Shape(string key,out int width,out int height)
        {
            width=height=1;
            if(Array.IndexOf(SeafoodRecipes.Keys,key)>=0){width=3;height=2;return true;}
            if(Array.IndexOf(ProductKeys,key)>=0){if(key=="bait"||key=="premium_bait"||key=="cheese"||key=="goat_cheese"||key.EndsWith("oil")||key.EndsWith("perfume")){width=height=2;return true;}height=key.EndsWith("milk")?2:1;return true;}
            if(key.EndsWith("_manual")){width=2;height=3;return true;}
            if(key=="cheese_press" || key=="oil_press" || key=="barrel" || key=="grill"){width=height=3;return true;}
            if(key.StartsWith("chicken_") || key.StartsWith("goat_"))
            {
                if(key.StartsWith("chicken_meat") || key.StartsWith("goat_meat"))
                {if(key.EndsWith("large")){width=3;height=2;}else if(key.EndsWith("medium")){width=height=2;}return true;}
                if(key=="chicken_egg")return true;
                bool goat=key.StartsWith("goat");
                width=key.EndsWith("baby")?1:key.EndsWith("young")?2:goat?3:2;
                height=key.EndsWith("baby")||key.EndsWith("young")?1:2;return true;
            }
            if(key.StartsWith("cooked_")){width=3;height=2;return true;}
            foreach(string fish in ExpansionStock.FishKeys)
                if(key.StartsWith(fish+"_") || key==fish)
                {width=key.EndsWith("roe")||key.EndsWith("fry")?1:key.EndsWith("small")?2:key.EndsWith("medium")?3:4;height=key.EndsWith("roe")||key.EndsWith("fry")||key.EndsWith("small")?1:2;return true;}
            foreach(string fish in Fishing.Kings)if(key==fish){width=4;height=3;return true;}
            return false;
        }
        public static string CutSuffix(int kcal)=>kcal>=900?"_large":kcal>=400?"_medium":"_tiny";
        public static void RegisterFood(GameItem item,string key,int calories,long price)
        {
            item.SetGameItemType("FOOD");FoodItemHelper.InitFoodItem(item,Math.Max(0,calories),false);
            Crops.SetInt(item,KindTag,1);Crops.SetInt(item,CaloriesTag,Math.Max(1,calories));Crops.SetInt(item,PriceTag,(int)Math.Min(int.MaxValue,price));
            item.unitValue=price;item.mayStackItemFunc=(a,b)=>false;
            item.shortDescription=L(key.Contains("meat")?"raw_meat_desc":key.StartsWith("cooked")?"cooked_desc":"dairy_desc");
        }
        public static GameItem Product(int index)
        {
            string key=ProductKeys[index];var item=ItemDirectory.CreateEmptyItem("pelican_"+key);
            Crops.Sprite(item,key,ShapeWidth(key),ShapeHeight(key));item.name=L(key);item.unitValue=ProductPrices[index];
            if(ProductCalories[index]>0)RegisterFood(item,key,ProductCalories[index],ProductPrices[index]);
            else if(key.EndsWith("perfume"))Scent.Init(item,key=="fairy_perfume"?30:20);
            else
            {
                item.SetGameItemType("FARMING_SUPPLIES");item.shortDescription=L("bait_desc");
                item.mayStackItemFunc=(a,b)=>a.identifier==b.identifier && a.unitCount+b.unitCount<=20 && GeneralHelper.IsItemOwned(a)==GeneralHelper.IsItemOwned(b);
            }
            if(key=="goat_milk")item.SetGameItemType("BREVAGE");
            item.onLoaded+=Refresh;return item;
        }
        public static int ShapeWidth(string key){Shape(key,out int w,out _);return w;}
        public static int ShapeHeight(string key){Shape(key,out _,out int h);return h;}
        public static GameItem RawMeat(string key,int kcal)
        {
            string sprite=key+CutSuffix(kcal);var item=ItemDirectory.CreateEmptyItem("pelican_"+sprite);
            Crops.Sprite(item,sprite,ShapeWidth(sprite),ShapeHeight(sprite));
            RegisterFood(item,key,kcal,Math.Max(1,(long)Math.Round(kcal*(key=="chicken_meat"?95.0:145.0)/900)));
            FoodItemHelper.InitFoodItem(item,kcal,false,true);FarmSpecies.SetString(item,"MEAT_TYPE_TAG",key=="chicken_meat"?ExpansionStock.Chicken:ExpansionStock.Goat);
            item.name=L(key)+L(kcal>=900?"cut_standard":kcal>=400?"cut_small":"cut_bite");item.onLoaded+=Refresh;return item;
        }
        public static GameItem Cooked(string species,int kcal,long price,string style)
        {
            string key="cooked_"+species;var item=ItemDirectory.CreateEmptyItem("pelican_"+key);
            Crops.Sprite(item,key,3,2);RegisterFood(item,key,kcal,price);item.name=style=="plain"?L(key):L("seasoned_name",L("style_"+style),L(key));
            FoodItemHelper.InitFoodItem(item,kcal,false,true);
            FarmSpecies.SetString(item,"MEAT_TYPE_TAG",species=="chicken"?ExpansionStock.Chicken:species=="goat"?ExpansionStock.Goat:species=="beef"?FarmRules.Cow:species=="pork"?FarmRules.Pig:"FISH_MEAT");
            FarmSpecies.SetString(item,"PELICAN_COOK_STYLE",style);
            if(style!="plain")item.AddItemFeature(new ItemFeature("PELICAN_COOK_STYLE"){identifier="pelican_cook_"+style}.SetPublicDisplay(L("style_"+style)).SetActualDisplay(L("style_"+style)).SetValueModifier(0));
            item.onLoaded+=Refresh;return item;
        }
        public static void Refresh(GameItem item)
        {
            if(item==null)return;
            MachineTrade.Refresh(item);
            ExpansionStock.Refresh(item);
            if(!item.identifier.StartsWith("pelican_"))return;
            string key=item.identifier.Substring(8);
            ProductGeometry.Apply(item,key);
            if(Array.IndexOf(ProductKeys,key)>=0 || key.StartsWith("cooked_") || key.EndsWith("_manual"))item.name=L(key);
            if(key.StartsWith("cooked_")){string style=item.GetTagReadonly("PELICAN_COOK_STYLE").GetString();if(!string.IsNullOrEmpty(style)&&style!="plain")item.name=L("seasoned_name",L("style_"+style),L(key));}
            if(key.StartsWith("chicken_meat_") || key.StartsWith("goat_meat_"))item.name=L(key.StartsWith("chicken")?"chicken_meat":"goat_meat")+L(key.EndsWith("large")?"cut_standard":key.EndsWith("medium")?"cut_small":"cut_bite");
            if(key.EndsWith("perfume")){item.shortDescription=L(key+"_desc");item.flavorText="";}
            else if(item.IsTag(KindTag))item.shortDescription=L(key.Contains("meat")?"raw_meat_desc":key.StartsWith("cooked")?"cooked_desc":"dairy_desc");
            if(item.IsTag(KindTag))
            {
                int calories=Math.Max(0,item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt());
                item.unitValue=(long)Math.Round(item.GetTagReadonly(PriceTag).GetInt()*(double)calories/Math.Max(1,item.GetTagReadonly(CaloriesTag).GetInt()));
            }
        }
        public static IEnumerable<GameItem> OwnedItems()
        {if(!Progress.Ready)yield break;foreach(var item in EmporiumEntry.Instance.GetAllItems())if(GeneralHelper.IsItemOwned(item))yield return item;}
        public static void ConsumeUnits(GameItem item,int amount)
        {
            if(amount<1)return;
            if(amount>=item.unitCount){item.Destroy();return;}
            item.SetUnitCount(item.unitCount-amount);item.onChangeAmountItemFunc?.Invoke(item);item.Validate();
        }
    }
}
