using System;
using System.Collections.Generic;

namespace PelicanCultivation
{
    // Balance defaults are intentionally independent of the native rat records.
    public static class FarmRules
    {
        public const string Cow="PELICAN_COW",Pig="PELICAN_PIG",Lobster="PELICAN_LOBSTER",Sea="PELICAN_SEA_CUCUMBER";
        public const string Prefix="PELICAN_FARM_",FedNights=Prefix+"FED_NIGHTS",LastNight=Prefix+"LAST_NIGHT",ProductClock=Prefix+"PRODUCT_CLOCK";
        public static bool Mammal(string id) => id==Cow || id==Pig;
        public static bool Aquatic(string id) => id==Lobster || id==Sea;
        public static int[] Stages(int adult,int elder) => new[]{0,1,Math.Max(2,adult-2),adult,Math.Max(adult+2,elder),Math.Max(adult+9,elder+9)};
        public static bool Produces(string species,string sex,int age,int adult,int elder,bool fed,bool healthy)
            => fed && healthy && age>=adult && age<elder && ((species==Cow && sex=="FEMALE") || (species==Pig && sex=="MALE"));
        public static int SeaCalories(int nights) => 60+(int)Math.Floor(900*Math.Log(1+Math.Max(0,nights)/20.0));
        public static double SeaCleanupMl(int nights) => 1+6*Math.Log(1+Math.Max(0,nights)/20.0);
        public static bool CanPayProduction(bool cow,float hunger,float maximum) => hunger-(cow?350:125)>=maximum*0.25f;
        public static float FryTransitionGrams(FishSpecies species,int count)
        {count=Math.Max(1,count);return (float)(count*species.stageWeightGrams[2])/Math.Max(1,species.stageStartDays[2]-species.stageStartDays[1])/count;}
        public static bool VisitDay(int day,bool farming,bool mammals,bool aquaculture)
            => day>0 && (((day%7==3||day%7==5) && farming) || (day%7==6 && (mammals||aquaculture)));
        public static int[] CutCalories(string species) => species==Lobster?new[]{900,400,200,100}:new[]{900,400,100};
        public static List<int> SplitCalories(string species,int total)
        {
            var result=new List<int>();int remaining=Math.Max(0,total);
            foreach(int size in CutCalories(species)) while(remaining>=size) {result.Add(size);remaining-=size;}
            if(remaining>0) result.Add(remaining);
            return result;
        }
        public static long MeatPrice(string species,int kcal) => Math.Max(1,(long)Math.Round(kcal*(double)(species==Lobster?180:species==Pig?150:130)/900));
        public static int TrufflePrice(bool small) => small?45:90;
        public static bool Sick(GameItem item) => item.IsTag("ANIMAL_DISEASED_TAG") || item.IsTag("ANIMAL_PRION_TAG") || item.IsTag("FISH_IS_STARVING_TAG");
    }

    public static class FarmSettings
    {
        private static ConfigEntry<int> milk,truffle,cowAdult,cowElder,pigAdult,pigElder,robotTrashPercent,feedWarningNights;
        private static ConfigEntry<bool> autoImmunity,autoFeces,feedReport,feedOnlyLow;
        public static int MilkDays => milk?.Value??2;
        public static int TruffleDays => truffle?.Value??3;
        public static int CowAdult => cowAdult?.Value??6;
        public static int CowElder => Math.Max(CowAdult+2,cowElder?.Value??26);
        public static int PigAdult => pigAdult?.Value??10;
        public static int PigElder => Math.Max(PigAdult+2,pigElder?.Value??22);
        public static bool AutoImmunity => autoImmunity?.Value??false;
        public static bool AutoFeces => autoFeces?.Value??true;
        public static int RobotTrashPercent => robotTrashPercent?.Value??80;
        public static bool FeedReport => feedReport?.Value??true;
        public static bool FeedOnlyLow => feedOnlyLow?.Value??false;
        public static int FeedWarningNights => feedWarningNights?.Value??3;
        public static void Define(ModConfig config)
        {
            const string section="Husbandry / 养殖";
            milk=config.Define("milk_interval_days",2,section,()=>ModEntry.L("farm_config_milk"),min:1,max:30);
            truffle=config.Define("truffle_interval_days",3,section,()=>ModEntry.L("farm_config_truffle"),min:1,max:30);
            cowAdult=config.Define("cow_adult_age",6,section,()=>ModEntry.L("farm_config_cow_adult"),min:4,max:30);
            cowElder=config.Define("cow_elder_age",26,section,()=>ModEntry.L("farm_config_cow_elder"),min:8,max:120);
            pigAdult=config.Define("pig_adult_age",10,section,()=>ModEntry.L("farm_config_pig_adult"),min:4,max:30);
            pigElder=config.Define("pig_elder_age",22,section,()=>ModEntry.L("farm_config_pig_elder"),min:8,max:120);
            autoImmunity=config.Define("auto_immunity_serum",false,section,()=>ModEntry.L("farm_config_auto_immunity"),tooltip:()=>ModEntry.L("farm_config_auto_immunity_hint"));
            autoFeces=config.Define("auto_feces_to_trash",true,section,()=>ModEntry.L("farm_config_auto_feces"),tooltip:()=>ModEntry.L("farm_config_auto_feces_hint"));
            robotTrashPercent=config.Define("robot_trash_percent",80,section,()=>ModEntry.L("farm_config_robot_trash"),min:50,max:100,tooltip:()=>ModEntry.L("farm_config_robot_trash_hint"));
            feedReport=config.Define("feed_supply_report",true,section,()=>ModEntry.L("farm_config_feed_report"),tooltip:()=>ModEntry.L("farm_config_feed_report_hint"));
            feedOnlyLow=config.Define("feed_report_only_low",false,section,()=>ModEntry.L("farm_config_feed_only_low"),tooltip:()=>ModEntry.L("farm_config_feed_only_low_hint"));
            feedWarningNights=config.Define("feed_warning_nights",3,section,()=>ModEntry.L("farm_config_feed_warning"),min:1,max:30);
        }
    }
}
