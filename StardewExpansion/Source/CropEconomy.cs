using System;

namespace PelicanCultivation
{
    public static class CropEconomy
    {
        public const string FormatTag="PELICAN_CROP_ECONOMY_INT";
        public const int Format=1;
        private static readonly int[] previousPrices={8,9,8,8,10,20,72,36};
        public static int PreviousPrice(CropDefinition crop) => Rules.Index(crop)<previousPrices.Length?previousPrices[Rules.Index(crop)]:crop.Price;
        // Run after legacy size normalization, once per saved item. Scale the
        // saved amounts so edited genetics, taste and partially consumed food
        // retain their proportions. Never regenerate an already eaten portion.
        public static void Upgrade(GameItem item,CropDefinition crop)
        {
            if(item.GetTagReadonly(FormatTag).GetInt()>=Format) return;
            if(Rules.Index(crop)>=8){Crops.SetInt(item,FormatTag,Format);return;}
            int size=Rules.Size(crop,item.GetTagReadonly(Rules.SizeTag).GetInt());
            double multiplier=crop.Sized?Rules.SizePrices[size]:1;
            long oldValue=(long)Math.Round(PreviousPrice(crop)*multiplier,MidpointRounding.AwayFromZero);
            long newValue=(long)Math.Round(crop.Price*multiplier,MidpointRounding.AwayFromZero);
            item.unitValue=(long)Math.Round(item.unitValue*(double)newValue/Math.Max(1,oldValue),MidpointRounding.AwayFromZero);
            if(item.IsTag(FoodItemHelper.CALORIE_VALUE_TAG))
            {
                int remaining=item.GetTagReadonly(FoodItemHelper.CALORIE_VALUE_TAG).GetInt();
                int previous=size==0?50:size==3?300:100;
                Crops.SetInt(item,FoodItemHelper.CALORIE_VALUE_TAG,Math.Max(remaining>0?1:0,
                    (int)Math.Round(remaining*(double)Rules.CropCalories(crop,size)/previous,MidpointRounding.AwayFromZero)));
            }
            Crops.SetInt(item,FormatTag,Format);
        }
    }
}
