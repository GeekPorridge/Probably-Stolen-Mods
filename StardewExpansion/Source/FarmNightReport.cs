using System;
using System.Collections.Generic;
using System.Linq;

namespace PelicanCultivation
{
    // All report writers append through the native log. Scrolling is handled
    // independently by NightReportScroll without replacing any log entries.
    public static class FarmNightReport
    {
        public sealed class Forecast
        {
            public string Species,Stage;
            public int Count,Nights;
        }
        public sealed class Snapshot
        {
            public int Alive,EggClutches,Hungry,Corpses,Ill,Prion,LowImmunity,DirtyCages,CrowdedCages,WaterTanks,UnpoweredTanks,Blocked,FullProducts;
            public readonly List<Forecast> Growth=new List<Forecast>();
            public bool HasStock => Alive+EggClutches+Corpses>0;
        }
        public static bool Collecting {get;private set;}
        private static bool hadStock;
        private static int nightDay,milk,truffles,treatments;
        private static readonly Dictionary<GameItem,bool> tankPower=new Dictionary<GameItem,bool>();
        private static readonly HashSet<GameItem> fullProducts=new HashSet<GameItem>();
        public static void Reset()
        {Collecting=false;hadStock=false;milk=truffles=treatments=0;tankPower.Clear();fullProducts.Clear();FarmWaste.Reset();}
        private static List<GameItem> OwnedItems()
            => new List<GameItem>(EmporiumEntry.Instance.GetAllItems()).Where(i=>i!=null && GeneralHelper.IsItemOwned(i)).Distinct().ToList();
        public static void BeginNight()
        {
            Progress.EnsureReady();
            if(Collecting && Progress.Ready && Progress.Day==nightDay) return;
            Reset();
            if(!ModEntry.Enabled || !Progress.Ready || Progress.Get("husbandry_native_report_day",-1)==Progress.Day) return;
            try
            {
                FarmSpecies.Register();nightDay=Progress.Day;
                var items=OwnedItems();
                hadStock=items.Any(i=>i.IsTag("ANIMAL_ITEM_TAG") || AquacultureHelper.IsFish(i) || AquacultureHelper.IsGroup(i));
                Collecting=true;
                FarmWaste.Service(items);
                // Protect every at-risk animal before native disease rolls and
                // health damage; active doses are never stacked.
                foreach(var animal in items.OrderByDescending(i=>i.IsTag("ANIMAL_DISEASED_TAG"))) FarmTreatment.TryTreatAtNight(animal);
            }
            catch(Exception error){Reset();ModEntry.Error("Begin husbandry night report",error);}
        }
        public static void RecordProduct(bool cow){if(Collecting){if(cow) milk++;else truffles++;}}
        public static void RecordTreatment(){if(Collecting) treatments++;}
        public static void RecordFullProducts(GameItem host){if(Collecting && host!=null) fullProducts.Add(host);}
        public static void ObserveTank(GameItem machine,bool powered){if(Collecting && machine!=null) tankPower[machine]=powered;}
        public static void EndNight()
        {
            if(!Collecting) return;
            try
            {
                if(!ModEntry.Enabled || !Progress.Ready || Progress.Day!=nightDay) return;
                var items=OwnedItems();FarmWaste.Service(items);
                var snapshot=Inspect(items);
                snapshot.FullProducts=fullProducts.Count;
                if(hadStock || snapshot.HasStock)
                {
                    Append(PlayerStore.Instance.AddNightLog,snapshot,milk,truffles,treatments);
                    if(FarmSettings.FeedReport)
                    {
                        var feed=FarmFeedForecast.Lines(FarmFeedForecast.Inspect(items)).ToList();
                        foreach(string line in feed) PlayerStore.Instance.AddNightLog(line);
                    }
                }
                Progress.Set("husbandry_native_report_day",nightDay);
            }
            catch(Exception error){ModEntry.Error("Complete husbandry night report",error);}
            finally {Reset();}
        }
        public static int CountStock(GameItem item)
            => AquacultureHelper.IsGroup(item)?Math.Max(1,item.GetTagReadonly("FISH_GROUP_COUNT_TAG").GetInt()):Math.Max(1,item.unitCount);
        public static int ForecastNights(int remaining,float progress,float speed)
            => Math.Max(1,(int)Math.Ceiling((Math.Max(0,remaining)-Math.Max(0,progress)-0.001f)/Math.Max(0.01f,speed)));
        public static float ProjectedGrowthSpeed(GameItem item,FishSpecies species)
        {
            float speed=AquacultureHelper.GetGrowthSpeed(item),recent=item.GetTagReadonly("FISH_RECENT_KCAL_TAG").GetFloat();
            if(recent<=0) return speed;
            float oldIndustrial=Math.Max(0,Math.Min(1,item.GetTagReadonly("FISH_INDUSTRIAL_DIET_TAG").GetFloat()));
            float oldMeat=Math.Max(0,Math.Min(1,item.GetTagReadonly("FISH_MEAT_DIET_TAG").GetFloat()));
            float industrial=Math.Max(0,Math.Min(1,item.GetTagReadonly("FISH_RECENT_INDUSTRIAL_TAG").GetFloat()/recent));
            float meat=Math.Max(0,Math.Min(1,item.GetTagReadonly("FISH_RECENT_MEAT_TAG").GetFloat()/recent));
            // Project the pending native diet update without dropping external
            // multipliers applied by GetGrowthSpeed (Neon genetics/traits).
            return speed*(1-industrial*0.25f+meat*species.meatGrowthBoost)/Math.Max(0.01f,1-oldIndustrial*0.25f+oldMeat*species.meatGrowthBoost);
        }
        public static void AddGrowth(Snapshot snapshot,GameItem item)
        {
            bool mammal=item.IsTag("ANIMAL_ITEM_TAG");
            string id=item.GetTagReadonly(mammal?"ANIMAL_SPECIES_TAG":"FISH_SPECIES_TAG").GetString();
            int age=item.GetTagReadonly(mammal?"ANIMAL_AGE_TAG":"FISH_AGE_TAG").GetInt(),target;
            string stage;float speed=1,progress=0;
            if(mammal)
            {
                var species=HusbandryHelper.GetSpecies(item);if(species==null || species.growthStages==null || species.growthStages.Length<5) return;
                target=species.growthStages[3];stage="adult";
                if(age>=target){target=species.growthStages[4];stage="elder";}
            }
            else
            {
                var species=AquacultureHelper.GetSpecies(item);if(species==null || species.stageStartDays==null || species.stageStartDays.Length<6) return;
                if(item.IsTag("FISH_ROE_TAG"))
                {
                    if(!item.IsTag("FISH_FERTILIZED_TAG")) return;
                    target=species.stageStartDays[1];stage="hatch";
                }
                else
                {
                    // Native growth pauses before feeding when reserves are low.
                    if(AquacultureHelper.GetHunger(item)<species.maxHunger*0.375f) return;
                    speed=ProjectedGrowthSpeed(item,species);
                    progress=item.GetTagReadonly("FISH_GROWTH_PROGRESS_TAG").GetFloat();
                    if(item.IsTag("FISH_FRY_TAG")){target=species.stageStartDays[2];stage="juvenile";}
                    else{target=species.stageStartDays[4];stage="adult";if(age>=target){target=species.stageStartDays[5];stage="elder";}}
                }
            }
            if(age>=target) return;
            int nights=ForecastNights(target-age,progress,speed),count=CountStock(item);
            var existing=snapshot.Growth.Find(f=>f.Species==id && f.Stage==stage && f.Nights==nights);
            if(existing!=null) existing.Count+=count;
            else snapshot.Growth.Add(new Forecast{Species=id,Stage=stage,Count=count,Nights=nights});
        }
        public static bool Dirty(GameGridInventory grid)
        {
            if(grid==null) return false;
            int cells=grid.inventoryShape.width*grid.inventoryShape.height;if(cells<=0) return false;
            int waste=0;foreach(var item in grid.childItems)
            {
                // Match both independent native hygiene checks, including a
                // doubly-tagged item if another mod creates one.
                if(item.IsTag("ANIMAL_FECES_TAG")) waste+=item.GetItemSize();
                if(item.IsTag("ANIMAL_IS_DEAD_TAG")) waste+=item.GetItemSize();
            }
            return waste/(double)cells>=0.25;
        }
        public static bool UnsafeWater(int actual,int needed,float waste,float damageThreshold,bool ignoresWaste)
            => actual<needed || (!ignoresWaste && waste>damageThreshold);
        public static Snapshot Inspect(IEnumerable<GameItem> items)
        {
            var snapshot=new Snapshot();
            var animalAreas=new HashSet<GameGridInventory>();
            foreach(var item in items.Where(i=>i!=null && GeneralHelper.IsItemOwned(i)).Distinct())
            {
                bool mammal=item.IsTag("ANIMAL_ITEM_TAG"),aquatic=AquacultureHelper.IsFish(item) || AquacultureHelper.IsGroup(item);
                if(mammal || aquatic)
                {
                    int count=CountStock(item);bool dead=mammal?HusbandryHelper.IsAnimalDead(item):AquacultureHelper.IsDead(item);
                    if(dead){snapshot.Corpses+=count;continue;}
                    if(item.IsTag("FISH_ROE_TAG")) snapshot.EggClutches+=Math.Max(1,item.unitCount);else snapshot.Alive+=count;
                    if(mammal)
                    {
                        if(item.parentInventory is GameGridInventory area) animalAreas.Add(area);
                        if(item.IsTag("ANIMAL_DISEASED_TAG")) snapshot.Ill+=count;
                        if(item.IsTag("ANIMAL_PRION_TAG")) snapshot.Prion+=count;
                        var species=HusbandryHelper.GetSpecies(item);
                        if(species!=null && GeneticHelper.GetCurrentStat(item,"ANIMAL_IMMUNITY_TAG")<species.immunityThreshold) snapshot.LowImmunity+=count;
                        if(item.IsTag("ANIMAL_IS_STARVING_TAG") || item.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()<HusbandryHelper.GetMaxHunger(item)*0.25f) snapshot.Hungry+=count;
                    }
                    else
                    {
                        var species=AquacultureHelper.GetSpecies(item);
                        if(!item.IsTag("FISH_ROE_TAG") && species!=null && (item.IsTag("FISH_IS_STARVING_TAG") || AquacultureHelper.GetHunger(item)<species.maxHunger*0.25f)) snapshot.Hungry+=count;
                    }
                    if(item.IsTag("PELICAN_FARM_TRANSITION_PENDING_BOOL")) snapshot.Blocked++;
                    else AddGrowth(snapshot,item);
                }
                if(item.IsTag("RAS_TAG"))
                {
                    var tank=MachineRAS.GetTankGrid(item);if(tank==null || !tank.childItems.Exists(AquacultureHelper.IsLiveStock)) continue;
                    var water=MachineRAS.GetWaterSlot(item)?.childItem;
                    int actual=AquacultureHelper.GetWaterMl(water),needed=AquacultureHelper.GetTotalWaterNeedMl(tank);
                    float waste=AquacultureHelper.GetWastePercent(water);
                    if(tank.childItems.Any(a=>AquacultureHelper.IsLiveStock(a) && AquacultureHelper.GetSpecies(a)!=null && UnsafeWater(actual,needed,waste,AquacultureHelper.GetWasteDamagePercent(AquacultureHelper.GetSpecies(a)),AquacultureHelper.GetSpecies(a).ignoresWaste))) snapshot.WaterTanks++;
                    if(tankPower.TryGetValue(item,out bool powered) && !powered) snapshot.UnpoweredTanks++;
                }
            }
            // Native hygiene also applies to uncaged animals in ordinary
            // inventories, not only to grids installed in an Animal Cage.
            foreach(var area in animalAreas)
            {
                if(Dirty(area)) snapshot.DirtyCages++;
                if(HusbandryHelper.GetAnimalOvercrowdingLevel(area)>0) snapshot.CrowdedCages++;
            }
            return snapshot;
        }
        private static string SpeciesName(string id)
        {
            if(AnimalSpecies.All.TryGetValue(id,out var animal)) return animal.GetDisplayName();
            if(FishSpecies.All.TryGetValue(id,out var fish)) return fish.GetDisplayName();
            return ModEntry.L("farm_night_unknown");
        }
        public static string[] Lines(Snapshot snapshot,int milkCount,int truffleCount,int treated)
        {
            string summary=ModEntry.L("farm_night_status",snapshot.Alive,snapshot.EggClutches,snapshot.Hungry,snapshot.Corpses);
            if(milkCount+truffleCount>0) summary+=ModEntry.L("farm_night_products",milkCount,truffleCount);
            if(FarmWaste.Moved>0) summary+=ModEntry.L("farm_waste_moved",FarmWaste.Moved);
            if(FarmWaste.TrashBlocked) summary+=ModEntry.L("farm_waste_blocked");
            var growth=snapshot.Growth.OrderBy(g=>g.Nights).ThenBy(g=>g.Species,StringComparer.Ordinal).ThenBy(g=>g.Stage,StringComparer.Ordinal).ToList();
            var parts=growth.Select(g=>ModEntry.L("farm_night_forecast",SpeciesName(g.Species),g.Count,g.Nights,ModEntry.L("farm_night_stage_"+g.Stage))).ToList();
            string forecast=parts.Count==0?ModEntry.L("farm_night_growth_none"):ModEntry.L("farm_night_growth",string.Join(ModEntry.L("farm_night_separator"),parts));
            if(snapshot.Blocked>0) forecast+=ModEntry.L("farm_night_blocked",snapshot.Blocked);
            if(snapshot.FullProducts>0) forecast+=ModEntry.L("farm_night_product_full",snapshot.FullProducts);
            var warnings=new List<string>();
            if(snapshot.Ill>0) warnings.Add(ModEntry.L("farm_night_ill",snapshot.Ill));
            if(snapshot.Prion>0) warnings.Add(ModEntry.L("farm_night_prion",snapshot.Prion));
            if(snapshot.DirtyCages>0) warnings.Add(ModEntry.L("farm_night_dirty",snapshot.DirtyCages));
            if(snapshot.CrowdedCages>0) warnings.Add(ModEntry.L("farm_night_crowded",snapshot.CrowdedCages));
            if(snapshot.WaterTanks>0) warnings.Add(ModEntry.L("farm_night_water",snapshot.WaterTanks));
            if(snapshot.UnpoweredTanks>0) warnings.Add(ModEntry.L("farm_night_power",snapshot.UnpoweredTanks));
            if(snapshot.LowImmunity>0) warnings.Add(ModEntry.L("farm_night_immunity",snapshot.LowImmunity));
            string warning=warnings.Count==0?ModEntry.L("farm_night_safe"):ModEntry.L("farm_night_warning",string.Join(ModEntry.L("farm_night_separator"),warnings));
            if(treated>0) warning+=ModEntry.L("farm_night_treated",treated);
            return new[]{summary,forecast,warning};
        }
        public static void Append(Action<string> addNightLog,Snapshot snapshot,int milkCount=0,int truffleCount=0,int treated=0)
        {if(addNightLog!=null) foreach(string line in Lines(snapshot,milkCount,truffleCount,treated)) addNightLog(line);}
    }
}
