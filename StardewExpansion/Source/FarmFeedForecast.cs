using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PelicanCultivation
{
    public static class FarmFeedForecast
    {
        public const string NumberTag=FarmRules.Prefix+"MACHINE_NUMBER";
        public sealed class Row
        {
            public int Number,Stored,Usable;
            public string Name;
            public double Nights,NextDemand;
            public bool Missing,LowTarget,EggsOnly;
        }
        public sealed class Pool
        {
            public double Meat,Other,FishFeed,Plant;
            public Pool Copy() => new Pool{Meat=Meat,Other=Other,FishFeed=FishFeed,Plant=Plant};
            public double Available(int mask) => ((mask&1)!=0?Meat:0)+((mask&2)!=0?Other:0)+((mask&4)!=0?FishFeed:0)+((mask&8)!=0?Plant:0);
            public double Draw(double amount,int mask)
            {
                double available=Available(mask),used=Math.Min(Math.Max(0,amount),available);
                if(available<=0) return 0;
                double ratio=used/available;
                if((mask&1)!=0) Meat=Math.Max(0,Meat-Meat*ratio);
                if((mask&2)!=0) Other=Math.Max(0,Other-Other*ratio);
                if((mask&4)!=0) FishFeed=Math.Max(0,FishFeed-FishFeed*ratio);
                if((mask&8)!=0) Plant=Math.Max(0,Plant-Plant*ratio);
                return used;
            }
        }
        private sealed class Consumer
        {
            public int Mask,Count,Age,Clock,Adult,Elder,Interval,Production;
            public double Base,Speed,Progress;
            public FishSpecies Fish;
            public bool Group,Roe,Fertilized;
            public double Next()
            {
                if(Fish==null)
                {
                    Age++;
                    if(Production>0 && Age>=Adult && Age<Elder && ++Clock>=Interval){Clock=0;return Base+Production;}
                    return Base;
                }
                if(Roe)
                {
                    if(!Fertilized || ++Age<Fish.stageStartDays[1]) return 0;
                    Roe=false;Group=true;Age=Fish.stageStartDays[1];
                }
                int previous=Age;
                Progress+=Speed;int steps=(int)Math.Floor(Progress+0.001);Progress=Math.Max(0,Progress-steps);Age+=steps;
                if(Group && Age<Fish.stageStartDays[2])
                    return Math.Max(5,Count*FarmRules.FryTransitionGrams(Fish,Count)*Fish.growthKcalPerGram*steps);
                if(Group)
                {
                    Group=false;Age=Math.Max(Age,Fish.stageStartDays[2]);
                    return Count*Math.Max(5,AquacultureHelper.GetNightlyDrain(Fish,Age,FarmRules.FryTransitionGrams(Fish,Count)));
                }
                float growth=0;
                for(int age=previous+1;age<=Age;age++)
                    growth+=Math.Max(0,WeightAtAge(Fish,age)-WeightAtAge(Fish,age-1));
                return Count*Math.Max(5,AquacultureHelper.GetNightlyDrain(Fish,Age,growth));
            }
        }
        public static int WeightAtAge(FishSpecies species,int age)
        {
            int stage=(int)species.GetStage(age);
            if(stage>=species.stageWeightGrams.Length-1) return species.stageWeightGrams[stage];
            int duration=species.stageStartDays[stage+1]-species.stageStartDays[stage];
            if(duration<=0) return species.stageWeightGrams[stage];
            float fraction=UnityEngine.Mathf.Clamp01((float)(age-species.stageStartDays[stage])/duration);
            return UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Lerp(species.stageWeightGrams[stage],species.stageWeightGrams[stage+1],fraction));
        }
        private static Consumer Demand(GameItem item)
        {
            if(item.IsTag("ANIMAL_ITEM_TAG"))
            {
                if(HusbandryHelper.IsAnimalDead(item)) return null;
                string id=item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();
                var c=new Consumer{Mask=PlantDiet.Herbivore(item)?8:11,Count=1,Age=item.GetTagReadonly("ANIMAL_AGE_TAG").GetInt(),Base=Math.Max(0,GeneticHelper.GetCurrentStat(item,"ANIMAL_HUNGER_RATE_TAG"))};
                bool cow=id==FarmRules.Cow,pig=id==FarmRules.Pig;
                if((cow || pig) && !FarmRules.Sick(item) && item.GetTagReadonly("ANIMAL_HEALTH_TAG").GetFloat()>0 && item.GetTagReadonly("ANIMAL_SEXE_TAG").GetString()==(cow?"FEMALE":"MALE"))
                {
                    c.Production=cow?350:125;c.Adult=cow?FarmSettings.CowAdult:FarmSettings.PigAdult;c.Elder=cow?FarmSettings.CowElder:FarmSettings.PigElder;
                    c.Interval=cow?FarmSettings.MilkDays:FarmSettings.TruffleDays;c.Clock=item.GetTagReadonly(FarmRules.ProductClock).GetInt();
                }
                if(ExpansionStock.NewAnimal(id)&&!FarmRules.Sick(item)&&item.GetTagReadonly("ANIMAL_SEXE_TAG").GetString()=="FEMALE")
                {bool chicken=id==ExpansionStock.Chicken;c.Production=chicken?150:400;c.Adult=chicken?4:8;c.Elder=chicken?18:28;c.Interval=chicken?1:3;c.Clock=item.GetTagReadonly(FarmRules.ProductClock).GetInt();}
                return c;
            }
            if(!AquacultureHelper.IsLiveStock(item)) return null;
            var species=AquacultureHelper.GetSpecies(item);if(species==null) return null;
            double speed=FarmNightReport.ProjectedGrowthSpeed(item,species);
            return new Consumer{Fish=species,Mask=species.diet==FishDiet.Carnivore?1:species.diet==FishDiet.Herbivore?14:15,
                Count=AquacultureHelper.IsGroup(item)?Math.Max(1,item.GetTagReadonly("FISH_GROUP_COUNT_TAG").GetInt()):1,
                Age=item.GetTagReadonly("FISH_AGE_TAG").GetInt(),Group=AquacultureHelper.IsGroup(item),Roe=item.IsTag("FISH_ROE_TAG"),Fertilized=item.IsTag("FISH_FERTILIZED_TAG"),
                Speed=Math.Max(0.01,speed),Progress=item.GetTagReadonly("FISH_GROWTH_PROGRESS_TAG").GetFloat()};
        }
        public static double Estimate(Pool feed,IEnumerable<GameItem> stock,out double nextDemand)
        {
            var consumers=stock.Select(Demand).Where(c=>c!=null).ToList();var pool=feed.Copy();nextDemand=0;
            // Project the existing group only. Births, mortality, changed diets
            // and hopper deliveries are deliberately not invented as stock.
            for(int night=0;night<365;night++)
            {
                double ratio=1;
                foreach(var consumer in consumers)
                {
                    double demand=consumer.Next();if(night==0) nextDemand+=demand;
                    if(demand<=0) continue;
                    ratio=Math.Min(ratio,pool.Draw(demand,consumer.Mask)/demand);
                }
                if(ratio<1-0.000001) return night+Math.Max(0,ratio);
            }
            return 365;
        }
        public static GameGridInventory StockGrid(GameItem machine)
            => machine.IsTag("CAGE_TAG")?AnimalCage.GetCageInventory(machine):MachineRAS.GetTankGrid(machine);
        public static List<GameItem> Feeders(GameItem machine,GameGridInventory grid)
        {
            var result=new List<GameItem>();
            var slotted=machine.IsTag("CAGE_TAG")?AnimalCage.GetDispenserSlot(machine)?.childItem:MachineRAS.GetDispenserSlot(machine)?.childItem;
            if(slotted!=null && slotted.IsTag("FEED_DISPENSER_TAG") && GeneralHelper.IsItemOwned(slotted)) result.Add(slotted);
            // Native mammals can also reach a dispenser standing in their grid.
            if(machine.IsTag("CAGE_TAG")) result.AddRange(grid.childItems.Where(i=>i.IsTag("FEED_DISPENSER_TAG") && GeneralHelper.IsItemOwned(i)));
            return result.Distinct().ToList();
        }
        public static Pool Supply(IEnumerable<GameItem> feeders)
        {
            var pool=new Pool();foreach(var feeder in feeders.Where(f=>f!=null && f.IsTag("FEED_DISPENSER_TAG") && GeneralHelper.IsItemOwned(f)).Distinct())
            {
                pool.Meat+=Math.Max(0,MachineFeedDispenser.GetMeatKcal(feeder));
                int plant=PlantDiet.Available(feeder);pool.Plant+=plant;pool.Other+=Math.Max(0,feeder.GetTagReadonly("STORED_OTHER").GetInt()-plant);
                pool.FishFeed+=Math.Max(0,MachineFeedDispenser.GetFishFeedKcal(feeder));
            }
            return pool;
        }
        public static void AssignNumbers(IEnumerable<GameItem> machines)
        {
            var list=machines.ToList();int next=Math.Max(1,Progress.Get("farm_next_machine_number",1));
            foreach(var machine in list) next=Math.Max(next,machine.GetTagReadonly(NumberTag).GetInt()+1);
            var used=new HashSet<int>();
            foreach(var machine in list.OrderBy(i=>i.uniqueId))
            {
                int number=machine.GetTagReadonly(NumberTag).GetInt();
                if(number<=0 || !used.Add(number)){number=next++;Crops.SetInt(machine,NumberTag,number);used.Add(number);}
            }
            Progress.Set("farm_next_machine_number",next);
        }
        public static Row InspectMachine(GameItem machine,GameGridInventory grid,IEnumerable<GameItem> feeders)
        {
            bool cage=machine.IsTag("CAGE_TAG");
            var stock=grid.childItems.Where(i=>GeneralHelper.IsItemOwned(i) && (cage?(i.IsTag("ANIMAL_ITEM_TAG") && !HusbandryHelper.IsAnimalDead(i)):AquacultureHelper.IsLiveStock(i))).ToList();
            if(stock.Count==0) return null;
            var installed=feeders.Distinct().ToList();var pool=Supply(installed);
            int mask=0;foreach(var item in stock){var consumer=Demand(item);if(consumer!=null) mask|=consumer.Mask;}
            double nextDemand,nights=Estimate(pool,stock,out nextDemand);
            int number=machine.GetTagReadonly(NumberTag).GetInt();
            return new Row{Number=number,Name=GeneralHelper.HasCustomName(machine)?GeneralHelper.GetCustomName(machine):ModEntry.L(machine.IsTag("CAGE_TAG")?"farm_feed_cage":"farm_feed_tank",number),
                Stored=(int)Math.Min(int.MaxValue,pool.Available(15)),Usable=(int)Math.Min(int.MaxValue,pool.Available(mask)),Nights=nights,NextDemand=nextDemand,
                Missing=installed.Count==0,LowTarget=installed.Any(f=>MachineFeedDispenser.GetTargetPercent(f)<75),EggsOnly=stock.All(i=>i.IsTag("FISH_ROE_TAG"))};
        }
        public static List<Row> Inspect(IEnumerable<GameItem> items)
        {
            var machines=items.Where(i=>i!=null && FarmHusbandry.Device(i) && GeneralHelper.IsItemOwned(i)).Distinct().ToList();AssignNumbers(machines);
            var rows=new List<Row>();foreach(var machine in machines)
            {
                try{var grid=StockGrid(machine);if(grid==null) continue;var row=InspectMachine(machine,grid,Feeders(machine,grid));if(row!=null) rows.Add(row);}
                catch(Exception error){ModEntry.Error("Machine feed forecast",error);}
            }
            return Sort(rows);
        }
        public static List<Row> Sort(IEnumerable<Row> rows)
            => rows.OrderBy(r=>r.Missing || r.LowTarget || r.Usable<=0?0:r.Nights).ThenBy(r=>r.Number).ToList();
        public static IEnumerable<string> Lines(IEnumerable<Row> rows)
        {
            foreach(var row in rows)
            {
                bool low=row.Missing || row.LowTarget || (!row.EggsOnly && row.Nights<FarmSettings.FeedWarningNights);
                if(FarmSettings.FeedOnlyLow && !low) continue;
                string status;
                if(row.Missing) status=ModEntry.L("farm_feed_missing");
                else if(row.LowTarget) status=ModEntry.L("farm_feed_target");
                else if(row.Usable<=0) status=ModEntry.L(row.Stored>0?"farm_feed_wrong_diet":"farm_feed_empty");
                else if(row.EggsOnly) status=ModEntry.L("farm_feed_eggs");
                else status=ModEntry.L("farm_feed_days",row.Usable,row.Nights>=365?"365+":(Math.Floor(row.Nights*10)/10).ToString("0.#",CultureInfo.InvariantCulture),low?ModEntry.L("farm_feed_low"):"");
                yield return ModEntry.L("farm_feed_row",row.Name,status);
            }
        }
    }
}
