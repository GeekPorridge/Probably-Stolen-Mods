using System;
using System.Collections.Generic;

namespace PelicanCultivation
{
    public sealed class CropDefinition
    {
        public readonly string Key;
        public readonly int Work, Regrowth, Quantity, Water, SeedPrice, Price, Stock, WineKind;
        public readonly bool Advanced, Sized;
        public readonly Dictionary<string, int> Traits;
        public string SeedId => "pelican_" + Key + "_seed";
        public string ProduceId => "pelican_" + Key;
        public CropDefinition(string key, int work, int regrowth, int quantity, int water, int seedPrice,
            int price, int stock, int wineKind, bool advanced, bool sized, params object[] traits)
        {
            Key=key; Work=work; Regrowth=regrowth; Quantity=quantity; Water=water; SeedPrice=seedPrice;
            Price=price; Stock=stock; WineKind=wineKind; Advanced=advanced; Sized=sized;
            Traits=new Dictionary<string,int>();
            for(int i=0;i<traits.Length;i+=2) Traits[(string)traits[i]]=(int)traits[i+1];
        }
    }

    public static class Rules
    {
        public const string CropTag="PELICAN_CROP_INT", WineTag="PELICAN_WINE_INT", SizeTag="PELICAN_SIZE_INT";
        public const string BreedTag="PELICAN_BREEDING_BOOL", OriginTag="PELICAN_MERCHANT_DAY_INT";
        public const string CareTag="PELICAN_HARVEST_CARE_INT";
        public const string CropFormatTag="PELICAN_CROP_FORMAT_INT", SizeScoreTag="PELICAN_WINE_SIZE_SCORE_INT";
        public const string GiantMissPrefix="PELICAN_GIANT_MISSES_", LastGiantTag="PELICAN_LAST_GIANT_INT";
        public const int CropFormat=18, GiantChancePercent=10, GiantPityMisses=10;
        public const int CareWork=20, PremiumCare=95, PremiumQuality=50;
        public const int AncientIndex=7, MinimumSeedDay=15, GuaranteeVisit=3;
        public const int NativePixelsPerCell=16, TexturePixelsPerCell=128;
        public const float InventoryPixelsPerUnit=100f*TexturePixelsPerCell/NativePixelsPerCell, PortraitScale=0.8f, PortraitDownwardFraction=0.16f;
        public static readonly CropDefinition[] Crops = {
            new CropDefinition("parsnip",20,0,3,80,12,12,3,0,false,false,"fast_grower",1,"hardy",1,"simple_genome",1),
            new CropDefinition("potato",24,0,6,120,24,14,6,0,false,true,"bountiful",2,"nutritious",1,"thirsty",1),
            new CropDefinition("corn",24,12,10,120,36,16,3,0,false,true,"perennial",1,"hardy",1,"thirsty",1),
            new CropDefinition("wheat",30,0,6,120,18,12,6,1,false,false,"fast_grower",1,"drought_resistant",1),
            new CropDefinition("hops",36,12,8,140,60,14,2,2,true,false,"perennial",1,"bountiful",1,"thirsty",2),
            new CropDefinition("strawberry",32,16,8,140,80,24,2,3,true,true,"perennial",1,"delicious",1,"frail",1),
            new CropDefinition("starfruit",40,0,4,180,100,72,2,4,true,true,"delicious",2,"slow_grower",1,"frail",2,"complex_genome",1),
            new CropDefinition("ancient_fruit",56,16,8,200,0,40,0,5,true,true,"perennial",1,"slow_grower",1,"complex_genome",2),
            new CropDefinition("chili",24,12,5,120,40,18,3,6,false,false,"perennial",1,"hardy",1,"drought_resistant",1),
            new CropDefinition("blueberry",44,16,7,150,80,20,2,7,true,true,"perennial",1,"bountiful",1,"delicious",1,"frail",1),
            new CropDefinition("poppy",40,0,4,120,50,28,3,0,false,false,"hardy",1,"fast_grower",1,"simple_genome",1),
            new CropDefinition("fairy_rose",48,0,4,180,100,48,2,0,true,false,"delicious",1,"slow_grower",1,"frail",1,"complex_genome",1)
        };
        public static readonly int[] CropNutrition={240,320,320,300,180,200,240,300,120,160,0,0};
        public static readonly int[] WineWork={0,4,6,6,8,10,6,8};
        public static readonly int[] WinePrices={0,60,100,120,320,200,100,150};
        public static readonly string[] WineKeys={"","beer","pale_ale","strawberry_wine","starfruit_wine","ancient_wine","chili_spirit","blueberry_wine"};
        public static readonly double[] SizePrices={0.5,1,1,3};
        public static readonly int[] ActiveSizes={0,1,3};
        public static readonly string[] SizeSuffixes={"_small","","_large","_huge"};
        public static int Size(CropDefinition crop,int size) => !crop.Sized?1:size==0?0:size==3?3:1;
        public static int CropWidth(CropDefinition crop,int size) => !crop.Sized?(crop.Key=="wheat"||Expansion.Flower(crop)?2:1):Size(crop,size)==0?1:Size(crop,size)==3?3:2;
        public static int CropHeight(CropDefinition crop,int size) => !crop.Sized?2:Size(crop,size)==0?1:Size(crop,size)==3?3:2;
        public static int CropCalories(CropDefinition crop,int size)
        {int normalized=Size(crop,size),basis=CropNutrition[Index(crop)];return normalized==0?basis/2:normalized==3?basis*3:basis;}
        public static int SizeScore(CropDefinition crop,int size) => crop.Sized && Size(crop,size)==3?2:0;
        public static string ProduceId(CropDefinition crop, int size) => crop.ProduceId + (crop.Sized ? SizeSuffixes[Size(crop,size)] : "");
        public static CropDefinition BySeed(string id) { foreach(var c in Crops) if(c.SeedId==id) return c; return null; }
        public static int Index(CropDefinition crop) => Array.IndexOf(Crops,crop);
        public static int IngredientScore(int raw) => raw<0 ? raw+1 : raw;
        public static int WineTier(int score) => score>=6 ? 5 : score==5 ? 4 : score>=3 ? 3 : score==2 ? 2 : score==1 ? 1 : 0;
        public static int Days(int work,int power) => (work+Math.Max(1,power)-1)/Math.Max(1,power);
        public static bool IsFruitWine(int kind) => (kind>=3 && kind<=5) || kind==7;
        public static bool WideBottle(int kind) => kind==1 || kind==2 || kind==6;
        public static int WineWidth(int kind) => WideBottle(kind) ? 2 : 1;
        public static int WineHeight(int kind) => WideBottle(kind) ? 3 : 4;
        public static float PortraitPixelsPerUnit(float sourceWidth,float sourceHeight,float referenceWidth,float referenceHeight,float referencePpu)
            => Math.Max(sourceWidth/Math.Max(1,referenceWidth),sourceHeight/Math.Max(1,referenceHeight))*Math.Max(1,referencePpu)/PortraitScale;
        public static int HalfFruit(int count) => Math.Max(0,count)/2;
        public static int RollCondition(int health,int roll)
        {
            if(health<=20) return roll<50 ? -2 : roll<85 ? -1 : 1;
            if(health<50) return roll<15 ? -1 : 1;
            if(health<85) return roll<75 ? 1 : 2;
            return roll<40 ? 1 : 2;
        }
        public static int CareScore(int startingHealth,int rawHealth,int requiredWork)
        {
            // Native health is stored in hundredths and loses health per work
            // point. Compare care across complete crop cycles, including long
            // first harvests and short perennial regrowth, on a 20-work basis.
            double loss=(startingHealth*100.0-rawHealth)/100.0;
            double score=startingHealth-loss*CareWork/Math.Max(1,requiredWork);
            return (int)Math.Round(Math.Max(0,Math.Min(100,score)),MidpointRounding.AwayFromZero);
        }
        public static PlantHarvest.SizeSplit SplitCrop(int count,int care,int quality)
        {
            var split=new PlantHarvest.SizeSplit();
            if(count<=0) return split;
            if(care>=PremiumCare && quality>=PremiumQuality) {split.normal=count;return split;}
                double smallRatio=care<=20?0.7:care<50?0.3:0.2;
                // Keep normal produce as the main harvest; the retired large
                // size is never generated. Giants are resolved separately.
                int small=Math.Max(1,(int)(count*smallRatio));
                if(care>20) small=Math.Min(2,small);
                split.small=Math.Min(Math.Max(0,count-1),small);
            split.normal=count-split.small;
            return split;
        }
        public static bool GiantEligible(int count,int care,int quality) => count>0 && care>=PremiumCare && quality>=PremiumQuality;
        public static bool RollGiant(int misses,int roll) => misses>=GiantPityMisses || roll<GiantChancePercent;
        public static PlantHarvest.SizeSplit WithGiant(PlantHarvest.SizeSplit split,bool giant)
        {
            if(!giant || split.Total<=0 || split.huge>0) return split;
            if(split.normal>0) split.normal--; else if(split.large>0) split.large--; else split.small--;
            split.huge=1;
            return split;
        }
        public static bool EligibleForDiscovery(int day) => day>=MinimumSeedDay;
        public static bool EligibleForGift(int visits,bool everObtained,bool delivered) => visits>=GuaranteeVisit && !everObtained && !delivered;
        public static bool NewVisit(int day,int previousDay) => day!=previousDay;
        public static int BatchSeeds(double multiplier,double roll)
        {
            double value=Math.Max(0,2*multiplier);
            int whole=(int)Math.Floor(value);
            return whole+(roll<value-whole ? 1 : 0);
        }
    }
}
