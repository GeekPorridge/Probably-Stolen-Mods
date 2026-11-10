using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class PlantDiet
    {
        public const string PlantKcal="PELICAN_PLANT_KCAL";
        public const string FeedSchema="PELICAN_FEED_DIET_SCHEMA";
        public static void RestoreLegacyFeed(GameItem item)
        {
            if(item==null || !item.IsTag("FEED_DISPENSER_TAG") || item.IsTag(FeedSchema)) return;
            // Older saves did not record plant provenance. Retain their existing
            // non-meat feed once; never increase the total stored calories.
            if(!item.IsTag(PlantKcal)) Crops.SetInt(item,PlantKcal,Math.Max(0,item.GetTagReadonly(MachineFeedDispenser.STORED_OTHER_KEY).GetInt()));
            Crops.SetInt(item,FeedSchema,1);
        }
        public static bool Herbivore(GameItem item)
        {string s=item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();return s==FarmRules.Cow||s==ExpansionStock.Chicken||s==ExpansionStock.Goat;}
        public static bool PlantFood(GameItem item)
        {
            if(item==null||!item.IsTag("CALORIE_VALUE_TAG")||FoodItemHelper.IsMeatFood(item)||GutterflowPelletHelper.IsPellet(item)||IndustrialFishFeedHelper.IsFeed(item))return false;
            int index=Crops.Index(item);if(index>=0&&index<Rules.Crops.Length)return !Expansion.Flower(Rules.Crops[index]);
            string id=item.identifier;return id=="fillerweed"||id.StartsWith("nutrifruit")||id=="wine_berry"||id=="bloomberry";
        }
        public static int Available(GameItem dispenser)=>Math.Max(0,Math.Min(dispenser.GetTagReadonly(PlantKcal).GetInt(),dispenser.GetTagReadonly(MachineFeedDispenser.STORED_OTHER_KEY).GetInt()));
        public static int Draw(GameItem dispenser,int need)
        {
            int amount=Math.Min(Math.Max(0,need),Available(dispenser));
            foreach(string tag in new[]{PlantKcal,MachineFeedDispenser.STORED_OTHER_KEY,MachineFeedDispenser.STORED_KCAL_TAG})Crops.SetInt(dispenser,tag,Math.Max(0,dispenser.GetTagReadonly(tag).GetInt()-amount));
            dispenser.Validate();return amount;
        }
        public static void Feed(GameItem animal,GameInventory inventory)
        {
            float hunger=animal.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat(),maximum=HusbandryHelper.GetMaxHunger(animal);if(hunger>=maximum*0.75f)return;
            var sources=(IEnumerable<GameItem>)AccessTools.Method(typeof(HusbandryHelper),"GetReachableDispensers").Invoke(null,new object[]{inventory});
            foreach(var dispenser in sources)
            {
                RestoreLegacyFeed(dispenser);
                float target=maximum*MachineFeedDispenser.GetTargetRatio(dispenser);int need=(int)Math.Ceiling(target-hunger);if(need<=0)continue;
                hunger+=Draw(dispenser,need);FarmSpecies.SetFloat(animal,"ANIMAL_HUNGER_TAG",Math.Min(maximum,hunger));if(hunger>=maximum*0.75f)return;
            }
            foreach(var food in new List<GameItem>(inventory.childItems))
            {
                if(!PlantFood(food)||!GeneralHelper.IsItemOwned(food))continue;int calories=food.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();int amount=Math.Min(calories,(int)Math.Ceiling(maximum-hunger));if(amount<=0)continue;
                hunger+=amount;FarmSpecies.SetFloat(animal,"ANIMAL_HUNGER_TAG",Math.Min(maximum,hunger));
                if(amount==calories)food.Destroy();else{long price=food.unitValue;Crops.SetInt(food,"CALORIE_VALUE_TAG",calories-amount);food.unitValue=(long)Math.Round(price*(calories-amount)/(double)calories);food.Validate();}
                if(hunger>=maximum*0.75f)return;
            }
        }
    }
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.TryFeed))]
    public static class PlantAnimalFeedingPatch
    {public static bool Prefix(GameItem item,GameInventory inventory){if(!ModEntry.Enabled||!PlantDiet.Herbivore(item))return true;PlantDiet.Feed(item,inventory);return false;}}
    [HarmonyPatch(typeof(MachineFeedDispenser),nameof(MachineFeedDispenser.TryBlendItem))]
    public static class PlantBlendProvenancePatch
    {
        public static void Prefix(GameItem machine,GameItem foodItem,out int __state){if(ModEntry.Enabled)PlantDiet.RestoreLegacyFeed(machine);__state=ModEntry.Enabled&&PlantDiet.PlantFood(foodItem)?Math.Max(0,foodItem.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()):0;}
        public static void Postfix(GameItem machine,bool __result,int __state){if(__result&&__state>0)Crops.SetInt(machine,PlantDiet.PlantKcal,PlantDiet.Available(machine)+__state);}
    }
    [HarmonyPatch(typeof(MachineFeedDispenser),nameof(MachineFeedDispenser.DrawKcal),new[]{typeof(GameItem),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(int),typeof(int),typeof(int)},new[]{ArgumentType.Normal,ArgumentType.Normal,ArgumentType.Normal,ArgumentType.Normal,ArgumentType.Normal,ArgumentType.Out,ArgumentType.Out,ArgumentType.Out})]
    public static class PlantDrawProvenancePatch
    {
        public static void Prefix(GameItem dispenser,out int[] __state){__state=ModEntry.Enabled?new[]{dispenser.GetTagReadonly(MachineFeedDispenser.STORED_OTHER_KEY).GetInt(),PlantDiet.Available(dispenser)}:null;}
        public static void Postfix(GameItem dispenser,int[] __state){if(__state==null||__state[0]<=0)return;int remaining=Math.Max(0,dispenser.GetTagReadonly(MachineFeedDispenser.STORED_OTHER_KEY).GetInt());Crops.SetInt(dispenser,PlantDiet.PlantKcal,(int)Math.Min(remaining,(long)__state[1]*remaining/__state[0]));}
    }
    [HarmonyPatch(typeof(MachineFeedDispenser),nameof(MachineFeedDispenser.EmptyStorage))]
    public static class PlantEmptyProvenancePatch
    {public static void Postfix(GameItem machine){if(ModEntry.Enabled)Crops.SetInt(machine,PlantDiet.PlantKcal,0);}}
}
