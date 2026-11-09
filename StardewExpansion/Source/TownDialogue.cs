using System;
using System.Collections.Generic;

namespace PelicanCultivation
{
    public static class TownDialogue
    {
        public const int Farming=1,PlantDna=2,Mammals=4,Aquaculture=8,AdvancedAquaculture=16;
        private const string Prefix="farmer_dialogue_";
        public static int Capabilities => (FarmStock.Farming?Farming:0)|(NetworkUpgrade.IsPlantDnaUnlocked()?PlantDna:0)|(FarmStock.Mammals?Mammals:0)|(FarmStock.Aquaculture?Aquaculture:0)|(FarmStock.Advanced?AdvancedAquaculture:0);
        public static long Revenue(RunStats stats) => stats==null?0:Math.Max(0L,stats.totalSales)+Math.Max(0L,stats.vendingIncome)+Math.Max(0L,stats.fountainIncome);
        public static void Initialize()
        {
            if(!Progress.Ready || Progress.Get(Prefix+"schema")!=0) return;
            // Existing saves need no backlog of already-known ability lessons.
            Progress.Set(Prefix+"abilities",Progress.Get("farmer_visits")>0?Capabilities:0);
            Progress.Set(Prefix+"schema",1);
        }
        public static int Priority(int visits,bool gift,int capabilities,int announced,long revenue,int congratulated)
        {
            if(visits<=1) return 1;
            if(gift) return 2;
            int fresh=capabilities&~announced;
            if((fresh&AdvancedAquaculture)!=0) return 14;
            if((fresh&PlantDna)!=0) return 11;
            if((fresh&Mammals)!=0) return 12;
            if((fresh&Aquaculture)!=0) return 13;
            if((fresh&Farming)!=0) return 10;
            if(revenue>=5000 && congratulated<3) return 22;
            if(revenue>=2000 && congratulated<2) return 21;
            if(revenue>=1000 && congratulated<1) return 20;
            return 0;
        }
        public static int AbilityAcknowledgement(int code)
        {
            switch(code)
            {
                case 10:return Farming;
                case 11:return Farming|PlantDna;
                case 12:return Mammals;
                case 13:return Aquaculture;
                case 14:return Aquaculture|AdvancedAquaculture;
                default:return 0;
            }
        }
        public static int[] CasualPool(int day,int capabilities)
        {
            var result=new List<int>();
            if(day%7==3){for(int i=0;i<((capabilities&PlantDna)!=0?8:6);i++) result.Add(100+i);}
            else
            {
                if((capabilities&Mammals)!=0) for(int i=0;i<6;i++) result.Add(200+i);
                if((capabilities&Aquaculture)!=0) for(int i=0;i<((capabilities&AdvancedAquaculture)!=0?6:4);i++) result.Add(300+i);
            }
            return result.ToArray();
        }
        public static int ChooseCasual(int[] pool,int previous,int roll)
        {
            var candidates=new List<int>();foreach(int code in pool) if(code!=previous || pool.Length==1) candidates.Add(code);
            return candidates.Count==0?100:candidates[Math.Max(0,roll)%candidates.Count];
        }
        public static string Key(int code)
        {
            switch(code)
            {
                case 1:return "farmer_story_intro";
                case 2:return "farmer_gift";
                case 10:return "farmer_unlock_farming";
                case 11:return "farmer_unlock_dna";
                case 12:return "farmer_unlock_mammals";
                case 13:return "farmer_unlock_aquaculture";
                case 14:return "farmer_unlock_aquaculture_ii";
                case 20:return "farmer_income_1000";
                case 21:return "farmer_income_2000";
                case 22:return "farmer_income_5000";
                default:return code>=400?"farmer_chat_artisan_"+(code-400):code>=300?"farmer_chat_aqua_"+(code-300):code>=200?"farmer_chat_ranch_"+(code-200):"farmer_chat_seed_"+(code-100);
            }
        }
        public static void PrepareVisit()
        {
            Initialize();int day=Progress.Get("stock_day",-1);
            if(!Progress.Ready || day<0 || Progress.Get(Prefix+"day",-2)==day) return;
            bool gift=FarmStock.Farming && Rules.EligibleForGift(Progress.Get("farmer_visits"),Progress.EverObtained,Progress.Get("gift_delivered")!=0);
            int capabilities=Capabilities;
            int code=day%7==5?400+Math.Max(0,Progress.Get("artisan_visits")-1)%4:Priority(Progress.Get("farmer_visits"),gift,capabilities,Progress.Get(Prefix+"abilities"),Revenue(RunStats.Current),Progress.Get(Prefix+"income_level"));
            if(code==0)
            {
                int[] pool=CasualPool(day,capabilities);
                int previous=Progress.Get(Prefix+(day%7==3?"last_seed":"last_ranch"),-1);
                int count=pool.Length-(pool.Length>1 && Array.IndexOf(pool,previous)>=0?1:0);
                code=ChooseCasual(pool,previous,RNG.GetRandomInt(0,Math.Max(0,count-1)));
            }
            Progress.Set(Prefix+"code",code);Progress.Set(Prefix+"day",day);
        }
        public static int CurrentCode => Progress.Get(Prefix+"code",1);
        public static string Text(int code,int day)
            => ModEntry.L(Key(code))+(code==2?"":"\n\n"+ModEntry.L(day%7==5?"farmer_goods_artisan":day%7==6?"farmer_goods_ranch":"farmer_goods_seed"));
        public static void Acknowledge(int code,int day)
        {
            if(Progress.Get(Prefix+"day",-2)!=day || CurrentCode!=code) return;
            int abilities=Progress.Get(Prefix+"abilities")|AbilityAcknowledgement(code);
            Progress.Set(Prefix+"abilities",abilities);
            if(code>=20 && code<=22) Progress.Set(Prefix+"income_level",Math.Max(Progress.Get(Prefix+"income_level"),code-19));
            if(code>=100) Progress.Set(Prefix+(day%7==5?"last_artisan":day%7==3?"last_seed":"last_ranch"),code);
        }
    }
}
