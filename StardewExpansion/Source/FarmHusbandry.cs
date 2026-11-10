using System;
using System.Collections.Generic;

namespace PelicanCultivation
{
    // Species-specific production only. Feeding, cleaning, breeding-stock
    // protection and slaughter are provided by the game's own equipment.
    public static class FarmHusbandry
    {
        public static bool Device(GameItem item) => item!=null && (item.IsTag("CAGE_TAG") || item.IsTag("RAS_TAG"));
        public static GameItem Host(GraphNodeStorage node)
            => node==null?null:GraphUtils.FindParentsType<GameItem>(node).Find(Device);
        public static void AttachCage(GameItem cage)
        {
            var row=cage?.contentWindow?.childElement as MachineryPanelRow;
            if(row==null) return;
            var existing=Products(cage);
            if(existing!=null){ConfigureProducts(existing);return;}
            var products=new GameGridInventory(6,2);products.EnableTag("PELICAN_FARM_PRODUCT_INVENTORY");
            ConfigureProducts(products);
            row.Add(MachineHelper.Panel(ModEntry.L("farm_products"),products,"pelican_products"));
            ExpansionStock.AttachCage(cage,row);
            var order=new List<int>{1,0};for(int i=3;i<row.Panels.Count;i++) order.Add(i);order.Add(2);
            row.SetDisplayOrder(order.ToArray());row.SetColumns(2);cage.contentWindow.RelinkStorage();
        }
        public static void ConfigureProducts(GameGridInventory products)
        {
            products.mayInventoryAddItemFunc=MayAddProduct;
        }
        public static bool MayAddProduct(GameItem food,GameInventory inventory)
        {
            // Resolve the live owning cage. Captured factory objects can differ
            // from the object restored into the player's inventory.
            var host=Host(inventory);
            return host!=null && GeneralHelper.IsItemOwned(host) && GeneralHelper.IsItemOwned(food)
                && (food.identifier=="pelican_milk" || food.identifier=="pelican_truffle_small" || food.identifier=="pelican_truffle" || food.identifier=="pelican_egg" || food.identifier=="pelican_goat_milk");
        }
        public static GameGridInventory ProductStorage(MachineryPanel panel)
        {
            // MachineHelper.Panel wraps inventories in MachineryIntake. Use
            // the native storage collector rather than casting the UI wrapper.
            var stores=new List<GraphNodeStorage>();panel?.CollectStorage(stores);
            foreach(var node in stores)
            {
                if(node is GameGridInventory grid) return grid;
                var nested=GraphUtils.FindAllChildrenType<GameGridInventory>(node);
                if(nested.Count>0) return nested[0];
            }
            return null;
        }
        public static GameGridInventory Products(GameItem cage)
            => ProductStorage((cage?.contentWindow?.childElement as MachineryPanelRow)?.GetPanel("pelican_products"));
        public static void Produce(GameItem animal,GameInventory inventory)
        {
            if(!ModEntry.Enabled || !Progress.Ready) return;
            string species=animal.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();if(!FarmRules.Mammal(species)) return;
            int today=Progress.Day;if(animal.IsTag(FarmRules.LastNight) && animal.GetTagReadonly(FarmRules.LastNight).GetInt()==today) return;
            Crops.SetInt(animal,FarmRules.LastNight,today);
            bool cow=species==FarmRules.Cow;int adult=cow?FarmSettings.CowAdult:FarmSettings.PigAdult,elder=cow?FarmSettings.CowElder:FarmSettings.PigElder;
            bool healthy=!HusbandryHelper.IsAnimalDead(animal) && !FarmRules.Sick(animal) && animal.GetTagReadonly("ANIMAL_HEALTH_TAG").GetFloat()>0;
            bool fed=animal.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()>=HusbandryHelper.GetMaxHunger(animal)*0.5f;
            if(!FarmRules.Produces(species,animal.GetTagReadonly("ANIMAL_SEXE_TAG").GetString(),animal.GetTagReadonly("ANIMAL_AGE_TAG").GetInt(),adult,elder,fed,healthy)) return;
            var host=Host(inventory);if(host==null || !GeneralHelper.IsItemOwned(host)) return;
            int interval=cow?FarmSettings.MilkDays:FarmSettings.TruffleDays;
            int clock=Math.Min(interval,animal.GetTagReadonly(FarmRules.ProductClock).GetInt()+1);
            Crops.SetInt(animal,FarmRules.ProductClock,clock);if(clock<interval) return;
            float hunger=animal.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat();
            if(!FarmRules.CanPayProduction(cow,hunger,HusbandryHelper.GetMaxHunger(animal))) return;
            var product=DirectoryMaster.Item(cow?"pelican_milk":"pelican_truffle_small");GeneralHelper.SetItemOwned(product,true);
            var target=Products(host);
            if(target==null || !GraphUtils.CanAccept(target,product) || GraphUtils.TryAcceptAll(target,product)!=0 || product.parentInventory!=target)
            {if(product.parentInventory==null) product.Destroy();FarmNightReport.RecordFullProducts(host);return;}
            Crops.SetInt(animal,FarmRules.ProductClock,0);FarmNightReport.RecordProduct(cow);
            FarmSpecies.SetFloat(animal,"ANIMAL_HUNGER_TAG",hunger-(cow?350:125));
        }
        public static void SeaNight(GameItem machine,GameGridInventory tank,GameItem water,bool powered)
        {
            FarmNightReport.ObserveTank(machine,powered);
            if(!ModEntry.Enabled || !Progress.Ready || !powered || water==null) return;
            double cleanup=0;
            foreach(var item in new List<GameItem>(tank.childItems))
            {
                if(item.GetTagReadonly("FISH_SPECIES_TAG").GetString()!=FarmRules.Sea || !AquacultureHelper.IsFish(item) || AquacultureHelper.IsDead(item) || AquacultureHelper.GetHunger(item)<FarmSpecies.SeaCucumbers.maxHunger*0.5f || AquacultureHelper.GetHealth(item)<=0) continue;
                int today=Progress.Day;if(item.IsTag(FarmRules.LastNight) && item.GetTagReadonly(FarmRules.LastNight).GetInt()==today) continue;
                Crops.SetInt(item,FarmRules.LastNight,today);int nights=item.GetTagReadonly(FarmRules.FedNights).GetInt()+1;
                Crops.SetInt(item,FarmRules.FedNights,nights);cleanup+=FarmRules.SeaCleanupMl(nights);
            }
            if(cleanup<=0) return;
            int removed=WaterHelper.RemoveContaminantFromContainer(water,"organic_waste",(int)Math.Round(cleanup*1000),0);
            if(removed>0)
            {
                WaterHelper.AddLiquid(water,"water",removed);WaterFeatureHelper.UpdateWaterFeature(water,false);SpriteHelper.UpdateWaterContainerSprite(water);
            }
        }
    }
}
