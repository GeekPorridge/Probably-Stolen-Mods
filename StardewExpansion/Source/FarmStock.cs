using System;
using System.Collections.Generic;

namespace PelicanCultivation
{
    public sealed class FarmStockEntry
    {
        public string Key,Id,Sex;
        public int Seed=-1;
        public FarmStockEntry(string key,string id,string sex=null){Key=key;Id=id;Sex=sex;}
        public string RemainingKey => "stock_left_"+Key;
    }
    public static class FarmStock
    {
        public const int StockSchema=2;
        public static bool Farming => Progress.Ready && NetworkUpgrade.IsRetiredFarmerUnlocked();
        public static bool Mammals => Progress.Ready && NetworkUpgrade.IsRetiredRancherUnlocked();
        public static bool Aquaculture => Progress.Ready && (NetworkUpgrade.IsAquacultureUnlocked() || NetworkUpgrade.IsAquacultureIIUnlocked());
        public static bool Advanced => Progress.Ready && NetworkUpgrade.IsAquacultureIIUnlocked();
        public static readonly FarmStockEntry[] Entries=Build();
        private static FarmStockEntry[] Build()
        {
            var list=new List<FarmStockEntry>();
            for(int i=0;i<Rules.Crops.Length;i++) if(i!=Rules.AncientIndex) list.Add(new FarmStockEntry(i.ToString(),Rules.Crops[i].SeedId){Seed=i});
            foreach(string animal in new[]{"rat","pelican_cow","pelican_pig","pelican_chicken","pelican_goat"}) foreach(string sex in new[]{"MALE","FEMALE"}) list.Add(new FarmStockEntry(animal+"_"+sex,animal,sex));
            foreach(string id in new[]{"cheese_press","oil_press","barrel","grill","plant_manual","animal_manual","artisan_manual"})list.Add(new FarmStockEntry("v2_"+id,"pelican_"+id));
            foreach(string fish in new[]{"tilapia","graft_tilapia","gutter_tilapia","pelican_sea_cucumber","pelican_lobster"}) list.Add(new FarmStockEntry(fish+"_roe",fish+"_roe"));
            foreach(string fish in new[]{"eel","electric_eel","sturgeon"}) list.Add(new FarmStockEntry(fish,fish));
            foreach(string food in new[]{"gutterflow_pellet","industrial_fish_feed","meat_scrap","nutrifruit_small"}) list.Add(new FarmStockEntry(food,food));
            foreach(string bait in new[]{"bait","premium_bait"})list.Add(new FarmStockEntry(bait,"pelican_"+bait));
            return list.ToArray();
        }
        public static Dictionary<string,int> Quantities(int day,bool farming,bool mammals,bool aquaculture,bool advanced,bool plantDna,bool bonus)
        {
            var quantities=new Dictionary<string,int>();foreach(var entry in Entries) quantities[entry.Key]=0;
            if(day%7==3 && farming)
            {
                quantities["v2_plant_manual"]=1;quantities["v2_animal_manual"]=1;
                foreach(var entry in Entries) if(entry.Seed>=0) {var crop=Rules.Crops[entry.Seed];quantities[entry.Key]=crop.Advanced&&!plantDna?0:crop.Stock;}
                if(bonus){quantities["nutrifruit_small"]=3;if(aquaculture){quantities["gutterflow_pellet"]=advanced?2:1;quantities["industrial_fish_feed"]=advanced?2:1;}}
            }
            if(day%7==5&&farming)foreach(string key in new[]{"cheese_press","oil_press","barrel","grill","artisan_manual"})quantities["v2_"+key]=1;
            if(day%7==6 && (mammals||aquaculture))
            {
                quantities["bait"]=5;quantities["premium_bait"]=advanced?3:0;
                foreach(var entry in Entries) if(entry.Sex!=null && mammals) quantities[entry.Key]=1;
                if(aquaculture)
                {
                    foreach(string fish in new[]{"tilapia","graft_tilapia","gutter_tilapia","pelican_sea_cucumber"}) quantities[fish+"_roe"]=1;
                    quantities["gutterflow_pellet"]=advanced?2:1;quantities["industrial_fish_feed"]=advanced?2:1;quantities["meat_scrap"]=2;
                    if(advanced){quantities["pelican_lobster_roe"]=1;foreach(string fish in new[]{"eel","electric_eel","sturgeon"}) quantities[fish]=1;}
                }
            }
            return quantities;
        }
        public static void Capture()
        {
            bool bonus=Progress.Day%7==3 && Farming && RNG.Roll(50);
            Progress.Set("stock_bonus_bundle",bonus?1:0);
            var quantities=Quantities(Progress.Day,Farming,Mammals,Aquaculture,Advanced,NetworkUpgrade.IsPlantDnaUnlocked(),bonus);
            foreach(var entry in Entries) Progress.Set(entry.RemainingKey,quantities[entry.Key]);
            Progress.Set("stock_schema",StockSchema);
        }
        public static void RefreshPending()
        {
            // Add the new bait offer once to a saved old-schema visit. Never
            // refill already purchased bait when restoring or filling again.
            if(Progress.Get("stock_schema")<StockSchema)
            {
                if(Progress.Get("stock_day",-1)%7==6)
                {Progress.Set("stock_left_bait",5);Progress.Set("stock_left_premium_bait",Advanced?3:0);}
                Progress.Set("stock_schema",StockSchema);
            }
            // Discard only an unsent Saturday offer from an older stock schema.
            // Already displayed or purchased items remain untouched.
            if(Progress.Get("stock_day",-1)%7==6) Progress.Set("stock_left_nutrifruit_small",0);
            if(Progress.Get("stock_day",-1)%7==3)foreach(string key in new[]{"cheese_press","oil_press","barrel","grill","artisan_manual"})Progress.Set("stock_left_v2_"+key,0);
        }
        public static GameItem Spawn(FarmStockEntry entry)
        {
            GameItem item;
            if(entry.Id=="pelican_chicken" || entry.Id=="pelican_goat")item=ExpansionStock.SpawnYoung(entry.Id,entry.Sex);
            else if(entry.Id=="pelican_cow" || entry.Id=="pelican_pig") item=FarmSpecies.SpawnYoung(entry.Id=="pelican_cow",entry.Sex);
            else if(entry.Id=="rat") item=PreBuiltItemHelper.CreateRat(entry.Sex,1,true);
            else if(entry.Id=="nutrifruit_small") item=PreBuiltItemHelper.CreateNutrifruitSmall();
            else if(entry.Id.EndsWith("_roe"))
            {
                FishSpecies species=entry.Id=="pelican_lobster_roe"?FarmSpecies.Lobsters:entry.Id=="pelican_sea_cucumber_roe"?FarmSpecies.SeaCucumbers:entry.Id=="graft_tilapia_roe"?FishSpecies.GraftTilapia:entry.Id=="gutter_tilapia_roe"?FishSpecies.GutterTilapia:FishSpecies.Tilapia;
                item=AquacultureItems.SpawnFertilizedRoe(species);
            }
            else item=DirectoryMaster.Item(entry.Id,false);
            GeneralHelper.SetItemOwned(item,false);
            if(entry.Seed>=0) Crops.SetInt(item,Rules.OriginTag,Progress.Day);
            return item;
        }
    }
}
