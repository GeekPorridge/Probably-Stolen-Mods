using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace PelicanCultivation
{
    public sealed class ArtisanPlan
    {
        public readonly Dictionary<GameItem,int> Units=new Dictionary<GameItem,int>();
        public readonly Dictionary<GameItem,int> Calories=new Dictionary<GameItem,int>();
        public readonly Dictionary<GameItem,int> Water=new Dictionary<GameItem,int>();
        public readonly List<GameItem> Products=new List<GameItem>();
        public int Nights=1;
        public int Recipe=-1;
        public bool Valid;
    }
    public static class ArtisanMachines
    {
        public const string MachineTag="PELICAN_ARTISAN_KIND",RecipeTag="PELICAN_ARTISAN_RECIPE",WorkTag="PELICAN_ARTISAN_WORK",LastNight="PELICAN_ARTISAN_LAST_NIGHT";
        public static readonly string[] Keys={"cheese_press","oil_press","barrel","grill"};
        public static readonly int[] Prices={450,600,450,500};
        public static int InputWidth(int kind)=>kind==1?10:8;
        public static readonly string[][] Recipes={new[]{"mayonnaise","cheese","goat_cheese"},new[]{"truffle_oil","poppy_perfume","fairy_perfume"},new[]{"barrel"},new[]{"plain","spicy","truffle","sauce","bait","premium_bait","baked_lobster","scallion_sea_cucumber","sashimi_platter"}};
        public static void RegisterItems(ModItemDirectory dir){for(int i=0;i<Keys.Length;i++){int k=i;dir.Add("pelican_"+Keys[i],()=>Create(k));}SeafoodRecipes.RegisterItems(dir);}
        public static GameItem Create(int kind)
        {
            if(kind==2)return Barrel.Create();
            var battery=new GameSlotInventory();var modules=new GameGridInventory(MachineHelper.DEFAULT_MODULE_BAY_CONFIG,MachineHelper.DEFAULT_MODULE_BAY_WIDTH);
            var input=new GameGridInventory(InputWidth(kind),4);var output=new GameGridInventory(8,4);
            var window=MachineHelper.CreateMachineryWindow(true,MachineHelper.PowerPanel(battery),MachineHelper.Panel(Expansion.L("modules"),modules,"module"),MachineHelper.Panel(Expansion.L("input"),input,"input"),MachineHelper.Panel(Expansion.L("output"),output,"output"));
            var item=ItemDirectory.CreateEmptyItem("pelican_"+Keys[kind]).SetContentWindow(window);Crops.Sprite(item,Keys[kind],3,3);
            item.name=Expansion.L(Keys[kind]);item.shortDescription=Expansion.L(Keys[kind]+"_desc");item.unitValue=Prices[kind];item.SetGameItemType("MACHINE");item.EnableTag("CONTAINER_TAG");item.EnableTag("manufacture");item.EnableTag("STANDARD_MACHINE_TAG");Crops.SetInt(item,MachineTag,kind+1);
            MachineHelper.SetupBatterySlot(battery,item);MachineryHelper.InitMachinery(item,8);MachineProgressHelper.InitProgressTypeMachine(item,8,false);
            Crops.SetInt(item,"MACHINE_PERFORMANCE_RATIO_INT",1);Crops.SetInt(item,"MACHINE_QUALITY_RATIO_INT",1);
            MachineHelper.SetupModuleBay(modules,item,null,new[]{"MODULE_TYPE_UNIVERSAL"});
            input.mayInventoryAddItemFunc=(food,inv)=>GeneralHelper.IsItemOwned(item)&&GeneralHelper.IsItemOwned(food)&&item.GetTagReadonly(WorkTag).GetInt()==0&&AcceptsInput(kind,food);
            input.mayInventoryRemoveItemFunc=(food,inv)=>item.GetTagReadonly(WorkTag).GetInt()==0;
            output.mayInventoryAddItemFunc=(food,inv)=>false;
            item.onCycleEndSlotItemFunc=(self,parent,slot)=>{if(ModEntry.Enabled)Tick(self,kind,input,output,battery,modules);};
            item.mayStackItemFunc=(a,b)=>false;MachineTrade.Refresh(item);
            item.onLoaded+=self=>{if(kind==1&&input.inventoryShape.width<InputWidth(kind)){input.SetShape(InputWidth(kind),4);self.contentWindow?.childElement?.Validate();}RouteEquipment(self,input,battery,modules);MachineTrade.Refresh(self);};return item;
        }
        public static bool EmptyBottle(GameItem item)=>item.IsTag(WineHelper.WINE_CONTAINER_TAG)&&item.GetTagReadonly(WineHelper.WINE_CONTAINER_STATE).GetInt()==0&&WaterHelper.GetCurrentCapacityML(item)==0&&GraphUtils.FindAllChildrenType<GameItem>(item).Count==0;
        public static bool AcceptsInput(int kind,GameItem item)
        {
            if(kind==3&&SeafoodRecipes.Accepts(item))return true;
            if(item==null||item.IsTag("power_source_item")||item.IsTag("MODULE_TAG")||item.IsTag(ExpansionStock.Egg)||item.IsTag("ANIMAL_ITEM_TAG")||item.IsTag("FISH_ITEM_TAG")||item.IsTag("FISH_ROE_TAG"))return false;
            string id=item.identifier;
            if(kind==0)return id=="pelican_egg"||id=="pelican_milk"||id=="pelican_goat_milk";
            if(kind==1)return id=="pelican_truffle"||id=="pelican_truffle_small"||Expansion.FlowerId(id)||EmptyBottle(item)||(WineHelper.IsFinishedWine(item)&&item.GetTagReadonly(Rules.WineTag).GetInt()==6);
            if(kind==3)return MeatKind(item)!=null||id=="pelican_chili"||id=="pelican_truffle_oil"||id=="pelican_mayonnaise"||id=="pelican_wheat"||id=="pelican_bait"||CleanWater(item);
            return false;
        }
        public static void RouteEquipment(GameItem machine,GameGridInventory input,GameSlotInventory battery,GameGridInventory modules)
        {
            if(!GeneralHelper.IsItemOwned(machine))return;
            var remove=input.mayInventoryRemoveItemFunc;input.mayInventoryRemoveItemFunc=(i,inv)=>true;
            try{foreach(var item in new List<GameItem>(input.childItems))
            {
                if(!GeneralHelper.IsItemOwned(item))continue;
                GraphNodeStorage target=item.IsTag("power_source_item")?(GraphNodeStorage)battery:item.IsTag("MODULE_TAG")?modules:null;
                if(target!=null&&GraphUtils.CanAccept(target,item))GraphUtils.TryAcceptAll(target,item);
            }}finally{input.mayInventoryRemoveItemFunc=remove;}
        }
        public static ArtisanPlan AutoPlan(int kind,IList<GameItem> items,int workingRecipe=-1)
        {
            if(workingRecipe>=0)return Plan(kind,workingRecipe,items);
            // Complete compound ingredients take precedence over their simpler
            // alternatives; an in-progress batch keeps its selected recipe.
            int[] order=kind==0?new[]{0,1,2}:kind==1?new[]{1,2,0}:new[]{8,6,7,5,4,1,2,3,0};
            foreach(int recipe in order){var p=Plan(kind,recipe,items);if(p.Valid)return p;DestroyPreview(p);}
            return new ArtisanPlan();
        }
        public static ArtisanPlan Plan(int kind,int recipe,IList<GameItem> items)
        {
            var p=new ArtisanPlan();recipe=Math.Max(0,Math.Min(Recipes[kind].Length-1,recipe));p.Recipe=recipe;
            if(kind==0)
            {
                string id=recipe==0?"pelican_egg":recipe==1?"pelican_milk":"pelican_goat_milk";int minKcal=recipe==0?150:recipe==1?350:400;
                int count=0;foreach(var item in items.Where(i=>i.identifier==id&&i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()>=minKcal)){if(count>=4)break;p.Units[item]=1;count++;}
                for(int i=0;i<count;i++)p.Products.Add(DirectoryMaster.Item("pelican_"+Recipes[kind][recipe]));p.Nights=recipe==0?1:2;p.Valid=count>0;
            }
            else if(kind==1 && recipe==0)
            {
                var big=items.Where(i=>i.identifier=="pelican_truffle"&&i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()>=300).Take(2).ToList();
                foreach(var item in big)p.Units[item]=1;int count=big.Count;
                var small=items.Where(i=>i.identifier=="pelican_truffle_small"&&i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()>=125).ToList();int pairs=Math.Min(2-count,small.Count/2);
                for(int i=0;i<pairs*2;i++)p.Units[small[i]]=1;count+=pairs;
                for(int i=0;i<count;i++)p.Products.Add(DirectoryMaster.Item("pelican_truffle_oil"));p.Valid=count>0;p.Nights=2;
            }
            else if(kind==1)
            {
                var spirit=items.FirstOrDefault(i=>WineHelper.IsFinishedWine(i)&&i.GetTagReadonly(Rules.WineTag).GetInt()==6&&Barrel.IsWholeBottle(i));
                var flowers=items.Where(i=>i.identifier==(recipe==1?"pelican_poppy":"pelican_fairy_rose")).Take(4).ToList();
                var empty=items.Where(EmptyBottle).Take(4).ToList();
                if(spirit!=null&&flowers.Count==4&&empty.Count==4)
                {
                    p.Units[spirit]=1;foreach(var i in flowers.Concat(empty))p.Units[i]=1;
                    for(int i=0;i<4;i++)p.Products.Add(DirectoryMaster.Item(recipe==1?"pelican_poppy_perfume":"pelican_fairy_perfume"));
                    p.Products.Add(DirectoryMaster.Item("wine_bottle"));p.Valid=true;
                }
                p.Nights=recipe==1?2:4;
            }
            else if(kind==3 && recipe>=6)
            {
                SeafoodRecipes.Plan(p,recipe,items);
            }
            else if(kind==3 && recipe>=4)
            {
                if(recipe==4)
                {
                    var wheat=items.Where(i=>i.identifier=="pelican_wheat"&&i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()>=Rules.CropNutrition[3]).Take(4).ToList();
                    int water=items.Where(CleanWater).Sum(WaterHelper.GetCurrentCapacityML);int groups=Math.Min(wheat.Count,water/50);
                    for(int i=0;i<groups;i++)p.Units[wheat[i]]=1;int left=groups*50;
                    foreach(var item in items.Where(CleanWater)){int take=Math.Min(left,WaterHelper.GetCurrentCapacityML(item));if(take>0)p.Water[item]=take;left-=take;}
                    for(int i=0;i<groups;i++){var bait=DirectoryMaster.Item("pelican_bait");bait.SetUnitCount(6);p.Products.Add(bait);}p.Valid=groups>0;
                }
                else
                {
                    var mayo=items.Where(i=>i.identifier=="pelican_mayonnaise"&&i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()>=180).Take(4).ToList();
                    int available=items.Where(i=>i.identifier=="pelican_bait").Sum(i=>i.unitCount);int groups=Math.Min(mayo.Count,available/6);
                    for(int i=0;i<groups;i++)p.Units[mayo[i]]=1;TakeUnits(p,items.Where(i=>i.identifier=="pelican_bait"),groups*6);
                    for(int i=0;i<groups;i++){var bait=DirectoryMaster.Item("pelican_premium_bait");bait.SetUnitCount(6);p.Products.Add(bait);}p.Valid=groups>0;
                }
            }
            else if(kind==3)
            {
                string meat=items.Select(MeatKind).FirstOrDefault(s=>s!=null&&(recipe!=3||s=="fish"));if(meat==null)return p;
                var raw=items.Where(i=>MeatKind(i)==meat).ToList();int calories=raw.Sum(i=>i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt());if(calories<1)return p;
                int batch=recipe==0?Math.Min(1800,calories):1800;if(calories<batch)return p;
                int extra=0;
                if(recipe>0)
                {
                    string extraId=recipe==1?"pelican_chili":recipe==2?"pelican_truffle_oil":"pelican_mayonnaise";int needed=recipe==1?2:1;
                    int full=recipe==1?Rules.CropNutrition[8]:recipe==2?Expansion.ProductCalories[5]:Expansion.ProductCalories[2];
                    var extras=items.Where(i=>i.identifier==extraId&&i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()>=full).Take(needed).ToList();if(extras.Count!=needed)return p;
                    foreach(var i in extras){p.Units[i]=1;extra+=i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();}
                }
                int remaining=batch;double rawPrice=0;
                foreach(var i in raw){int kcal=i.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();int take=Math.Min(remaining,kcal);if(take>0){p.Calories[i]=take;rawPrice+=i.unitValue*take/(double)Math.Max(1,kcal);}remaining-=take;}
                double basic=meat=="chicken"?120:meat=="goat"?185:meat=="beef"?165:meat=="pork"?190:rawPrice*1.25*900/batch;
                int count=(batch+899)/900;int total=batch+extra;
                for(int i=0;i<count;i++){int part=i==count-1?total:total/count;total-=part;double price=basic*batch/900/count+(recipe==1?30:recipe==2?85:recipe==3?25:0);p.Products.Add(Expansion.Cooked(meat,part,(long)Math.Round(price),Recipes[kind][recipe]));}
                p.Valid=p.Products.Count>0;
            }
            if(p.Valid)
            {
                var foodSources=p.Units.Keys.Concat(p.Calories.Keys).Where(i=>i.IsTag("CALORIE_VALUE_TAG")||i.IsTag(Rules.CareTag)||AquacultureHelper.IsFish(i)).Distinct().ToList();
                int quality=foodSources.Count==0?0:(int)Math.Round(foodSources.Average(i=>i.itemFeatures.Where(f=>f.featureType==ItemFeature.FeatureType.Normale&&!f.isDisabled).Sum(f=>f.GetCurrentValueMod())));quality=Math.Max(-25,Math.Min(25,quality));
                foreach(var product in p.Products.Where(i=>i.IsTag("CALORIE_VALUE_TAG")||i.identifier.EndsWith("perfume")))
                {
                    if(quality!=0)product.AddItemFeature(new ItemFeature("PELICAN_ARTISAN_QUALITY"){identifier="pelican_artisan_quality",isFeatureMatch=true}.SetPublicDisplay(Expansion.L("artisan_quality")).SetActualDisplay(Expansion.L("artisan_quality")).SetValueModifier(quality).SetPreExposedValueMod(quality));
                    if(foodSources.Any(i=>i.IsTag("ANIMAL_PRION_TAG")))product.EnableTag("ANIMAL_PRION_TAG");
                }
            }
            return p;
        }
        public static void TakeUnits(ArtisanPlan p,IEnumerable<GameItem> items,int needed)
        {foreach(var item in items){int count=Math.Min(item.unitCount,needed);if(count>0)p.Units[item]=count;needed-=count;if(needed==0)break;}}
        public static bool CleanWater(GameItem item)=>item.IsTag("LIQUID_CONTAINER_TAG")&&!WineHelper.IsFinishedWine(item)&&WaterHelper.GetContaminantPercentage(item,"water")>=9700&&new[]{"heavy_metal","chemical_contaminant","microbe","organic_waste","acid","base"}.All(c=>WaterHelper.GetContaminantPercentage(item,c)<=10);
        public static string MeatKind(GameItem item)
        {
            if(item==null||!FoodItemHelper.IsMeatFood(item)||item.IsTag("PELICAN_COOK_STYLE")||item.identifier.StartsWith("pelican_cooked_"))return null;
            string s=item.GetTagReadonly("MEAT_TYPE_TAG").GetString();if(s==FarmRules.Cow)return "beef";if(s==FarmRules.Pig)return "pork";if(s==ExpansionStock.Chicken)return "chicken";if(s==ExpansionStock.Goat)return "goat";
            // Shellfish and sea cucumbers have dedicated recipes and never
            // become the generic grilled finfish product.
            if(s==FishSpecies.Tilapia.speciesID||s==FishSpecies.GraftTilapia.speciesID||s==FishSpecies.GutterTilapia.speciesID||s==FishSpecies.Eel.speciesID||s==FishSpecies.ElectricEel.speciesID||s==FishSpecies.Sturgeon.speciesID||s=="FISH_MEAT"||ExpansionStock.NewFish(s))return "fish";
            if(item.identifier.StartsWith("tilapia_meat")||item.identifier.StartsWith("eel_meat")||item.identifier.StartsWith("sturgeon_meat"))return "fish";
            return null;
        }
        public static bool Commit(ArtisanPlan plan,GameGridInventory output)
        {
            if(!plan.Valid)return false;
            var native=output.mayInventoryAddItemFunc;output.mayInventoryAddItemFunc=(i,inv)=>true;bool accepted=false;
            var stackRules=plan.Products.ToDictionary(i=>i,i=>i.mayStackItemFunc);
            // Stage into separate slots. A native merge destroys the temporary
            // stack, making rollback unable to distinguish old/new quantities.
            foreach(var product in plan.Products)product.mayStackItemFunc=(a,b)=>false;
            try
            {
                foreach(var product in plan.Products.OrderByDescending(i=>i.shape.height).ThenByDescending(i=>i.GetItemSize())){GeneralHelper.SetItemOwned(product,true);if(GraphUtils.TryAcceptAll(output,product)!=0||product.parentInventory!=output)return false;}
                foreach(var unit in plan.Units)if(unit.Key.unitCount<unit.Value)return false;
                foreach(var c in plan.Calories)if(c.Key.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()<c.Value)return false;
                foreach(var w in plan.Water)if(WaterHelper.GetCurrentCapacityML(w.Key)<w.Value)return false;
                foreach(var unit in plan.Units)Expansion.ConsumeUnits(unit.Key,unit.Value);
                foreach(var c in plan.Calories){int old=c.Key.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();if(old==c.Value)c.Key.Destroy();else{c.Key.unitValue=(long)Math.Round(c.Key.unitValue*(old-c.Value)/(double)old);Crops.SetInt(c.Key,"CALORIE_VALUE_TAG",old-c.Value);c.Key.Validate();}}
                foreach(var w in plan.Water)WaterHelper.RemoveMl(w.Key,w.Value);
                accepted=true;return true;
            }
            finally{output.mayInventoryAddItemFunc=native;foreach(var product in plan.Products)product.mayStackItemFunc=stackRules[product];if(!accepted)foreach(var product in plan.Products)product.Destroy();}
        }
        public static void DestroyPreview(ArtisanPlan plan){foreach(var item in plan.Products)if(item.parentInventory==null)item.Destroy();}
        public static void Tick(GameItem machine,int kind,GameGridInventory input,GameGridInventory output,GameSlotInventory battery,GameGridInventory modules)
        {
            if(!Progress.Ready||!GeneralHelper.IsItemOwned(machine)||machine.GetTagReadonly(LastNight).GetInt()==Progress.Day)return;Crops.SetInt(machine,LastNight,Progress.Day);
            RouteEquipment(machine,input,battery,modules);
            if(!MachineHelper.CanPower(machine,battery))return;
            int prior=machine.GetTagReadonly(WorkTag).GetInt();
            var plan=AutoPlan(kind,new List<GameItem>(input.childItems),prior>0?machine.GetTagReadonly(RecipeTag).GetInt():-1);if(!plan.Valid){DestroyPreview(plan);Crops.SetInt(machine,WorkTag,0);return;}
            Crops.SetInt(machine,RecipeTag,plan.Recipe);
            int quality=Math.Max(-50,Math.Min(50,ModuleHelper.GetAjustedQuality(machine)));
            if(quality!=0)foreach(var product in plan.Products.Where(i=>i.IsTag("CALORIE_VALUE_TAG")||i.identifier.EndsWith("perfume")))product.AddItemFeature(new ItemFeature("PELICAN_ARTISAN_MODULE_QUALITY"){identifier="pelican_artisan_module_quality",isFeatureMatch=true}.SetPublicDisplay(Expansion.L("artisan_quality")).SetActualDisplay(Expansion.L("artisan_quality")).SetValueModifier(quality).SetPreExposedValueMod(quality));
            int left=machine.GetTagReadonly(WorkTag).GetInt();if(left<=0){left=plan.Nights*8;Crops.SetInt(machine,"PELICAN_ARTISAN_TOTAL_WORK",left);}
            int speed=Math.Max(1,machine.GetTagReadonly(MachineProgressHelper.CURRENT_PROCESSING_SPEED_TAG).GetInt());
            if(left<=speed)
            {
                if(Commit(plan,output)){MachineHelper.TryDrawCyclePower(machine,battery);MachineHelper.OnMachineActioned(machine,modules);Crops.SetInt(machine,WorkTag,0);}
                else Crops.SetInt(machine,WorkTag,1);
            }
            else{DestroyPreview(plan);MachineHelper.TryDrawCyclePower(machine,battery);MachineHelper.OnMachineActioned(machine,modules);Crops.SetInt(machine,WorkTag,left-speed);}
            machine.Validate();
        }
    }
}
