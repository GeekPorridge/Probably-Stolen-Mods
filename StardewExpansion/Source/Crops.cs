using System;
using System.Collections.Generic;
using UnityEngine;

namespace PelicanCultivation
{
    public static class Crops
    {
        public static void RegisterPlants()
        {
            foreach(var crop in Rules.Crops)
            {
                PlantRegistry.Add(DefinePlant(crop,ModEntry.L("crop_"+crop.Key)));
            }
        }
        public static PlantDefinition DefinePlant(CropDefinition crop,string name)
        {
            var plant=new PlantDefinition(crop.SeedId,name,crop.ProduceId,crop.Work,crop.Quantity)
                .Nutriment(crop.Advanced?200:150,100,50).Water(crop.Water).Contaminant(1).OnHarvest(ctx=>Harvest(crop,ctx));
            if(crop.Regrowth>0) plant.Refruit(crop.Regrowth);
            if(crop.Key=="parsnip" || crop.Key=="ancient_fruit") plant.ReturnsSeeds();
            foreach(var trait in crop.Traits) plant.Innate(trait.Key,trait.Value);
            return plant;
        }
        public static void RegisterItems(ModItemDirectory directory)
        {
            foreach(var crop in Rules.Crops)
            {
                var current=crop;
                directory.Add(crop.SeedId,()=>CreateSeed(current));
                for(int grade=0;grade<(crop.Sized?Rules.ActiveSizes.Length:1);grade++)
                {
                    int size=crop.Sized?Rules.ActiveSizes[grade]:1;
                    directory.Add(Rules.ProduceId(crop,size),()=>CreateProduce(current,size));
                }
            }
        }
        public static void Sprite(GameItem item,string key,int width,int height)
        {
            var mod=ModLoader.Mods.Find(m=>m.Manifest.ID==ModEntry.ModId);
            if(mod!=null && Assets.InventorySprite(mod,key)!=null) BindSprite(item,key,width,height);
            else throw new InvalidOperationException("Missing mod sprite '"+key+"'. Expected Textures/Items/"+key+".png after asset loading.");
        }
        public static void BindSprite(GameItem item,string key,int width,int height)
        {
            // Texture resolution is independent of inventory geometry. The native
            // SetSpriteAndShapeFromMod would infer a huge shape from the HD pixels.
            item.useSpriteFromMod=true;
            item.SetSprite(ModEntry.ModId,key);
            byte[] cells=new byte[width*height];
            for(int i=0;i<cells.Length;i++) cells[i]=1;
            item.SetShape(new GridShapeBuilder().SetData(cells,width));
        }
        public static void EnsureSeedClassification(GameItem item) => item.SetGameItemType("SEED");
        public static void RepairHitSurface(GameItemElement item)
        {
            if(item==null || !item.useSpriteFromMod || item.spriteAtlasPath!=ModEntry.ModId || item.background?.image==null) return;
            // Our items reserve explicit rectangular cells. Match pointer coverage
            // to those cells rather than the texture's alpha/PPU conversion.
            var image=item.background.image;
            image.type=UnityEngine.UI.Image.Type.Simple;
            image.alphaHitTestMinimumThreshold=0;
            image.raycastTarget=true;
            if((item.IsTag(BottleGeometry.PendingBeerTag) || item.IsTag(CropGeometry.PendingTag) || item.IsTag(ProductGeometry.Pending)) && image.sprite!=null)
            {
                // While waiting for six free cells, keep the new art inside the
                // old, collision-free footprint saved by the previous version.
                float maxW=item.shape.width*UISettings.current.gridSize,maxH=item.shape.height*UISettings.current.gridSize;
                var rect=image.sprite.rect;
                float scale=Math.Min(maxW/rect.width,maxH/rect.height);
                var size=new Vector2(rect.width*scale,rect.height*scale);
                image.rectTransform.sizeDelta=size;
                if(item.fakeBackground?.image!=null) item.fakeBackground.image.rectTransform.sizeDelta=size;
            }
        }
        public static GameItem CreateSeed(CropDefinition crop)
        {
            GameItem item=ItemDirectory.CreateEmptyItem(crop.SeedId);
            Sprite(item,crop.Key+"_seed",1,1);
            item.name=ModEntry.L("seed_"+crop.Key);
            item.unitValue=crop.SeedPrice>0 ? crop.SeedPrice : 150;
            item.shortDescription=ModEntry.L("seed_desc_"+crop.Key);
            item.flavorText="";
            item.mayStackItemFunc=(a,b)=>false;
            HydroponicHelper.InitSeedItem(item,crop.SeedId);
            CropCare.RemoveRedundantTrait(item);
            EnsureSeedClassification(item);
            item.onLoaded+=RefreshLoaded;
            if(crop.Key=="ancient_fruit")
            {
                item.onSlotAddItemFunc+=(self,inv,slot)=>Progress.ObserveSeed(self);
                item.onLoaded+=self=>Progress.ObserveSeed(self);
            }
            return item;
        }
        public static GameItem CreateProduce(CropDefinition crop,int size)
        {
            size=Rules.Size(crop,size);
            GameItem item=ItemDirectory.CreateEmptyItem(Rules.ProduceId(crop,size));
            Sprite(item,crop.Key+(crop.Sized?Rules.SizeSuffixes[size]:""),Rules.CropWidth(crop,size),Rules.CropHeight(crop,size));
            item.name=Name(crop,size);
            item.unitValue=(long)Math.Round(crop.Price*(crop.Sized?Rules.SizePrices[size]:1),MidpointRounding.AwayFromZero);
            item.shortDescription=ModEntry.L("produce_desc_"+crop.Key);
            item.mayStackItemFunc=(a,b)=>false;
            if(!Expansion.Flower(crop))
            {item.SetGameItemType("FOOD");FoodItemHelper.InitFoodItem(item,Rules.CropCalories(crop,size),false);}
            else item.SetGameItemType("LUXURY_ITEM");
            AudioHelper.InitProduce(item);
            EnsureProduceClassification(item);
            SetInt(item,Rules.CropTag,Rules.Index(crop)+1);
            SetInt(item,Rules.SizeTag,size);
            SetInt(item,Rules.CropFormatTag,Rules.CropFormat);
            SetInt(item,CropEconomy.FormatTag,CropEconomy.Format);
            if(crop.WineKind>0)
            {
                item.EnableTag(WineHelper.WINE_INGREDIENT_TAG);
                SetInt(item,WineHelper.BERRY_CONDITION_INT,1);
            }
            PlantGeneticsHelper.ApplyFactoryGenetics(item,crop.SeedId);
            item.onLoaded+=RefreshLoaded;
            return item;
        }
        public static string Name(CropDefinition crop,int size) => crop.Sized && size!=1 ? ModEntry.L("produce_size",ModEntry.L("size_"+size),ModEntry.L("crop_"+crop.Key)) : ModEntry.L("crop_"+crop.Key);
        public static void EnsureProduceClassification(GameItem item)
        {
            int index=Index(item);
            if(Expansion.FlowerId(item.identifier) || (index>=0 && index<Rules.Crops.Length && Expansion.Flower(Rules.Crops[index])))
            {item.RemoveGameItemType("FOOD");item.DisableTag("CALORIE_VALUE_TAG");item.DisableTag(InspectableHelper.INSPECTABLE_TAG);item.SetGameItemType("LUXURY_ITEM");return;}
            item.SetGameItemType("FOOD");
            item.DisableTag(InspectableHelper.INSPECTABLE_TAG);
        }
        public static void RefreshLoaded(GameItem item)
        {
            if(item==null) return;
            var seed=Rules.BySeed(item.identifier);
            if(seed!=null)
            {
                // Saved itemTypes/state can overwrite the factory's classification.
                // Do not reinitialize genetics or growing progress during repair.
                EnsureSeedClassification(item);
                CropCare.RemoveRedundantTrait(item);
                CropTraitRevision.Migrate(item,seed);
                Sprite(item,seed.Key+"_seed",1,1);
                string name=ModEntry.L("seed_"+seed.Key);
                item.name=HydroponicHelper.IsGrowingSeed(item)?LocHelper.GetLocalizedMechanic("mech_hydroponic_growing_prefix",name):name;
                item.shortDescription=ModEntry.L("seed_desc_"+seed.Key);
                item.flavorText="";
                item.Validate();
                return;
            }
            int index=Index(item);
            if(index>=0 && index<Rules.Crops.Length)
            {
                var crop=Rules.Crops[index];
                CropGeometry.PrepareLegacy(item,crop);
                int size=Rules.Size(crop,item.GetTagReadonly(Rules.SizeTag).GetInt());
                item.name=Name(crop,size);
                item.shortDescription=ModEntry.L("produce_desc_"+crop.Key);
                item.flavorText="";
                EnsureProduceClassification(item);
                CropGeometry.Apply(item,crop,size);
                item.Validate();
            }
        }
        public static void SetInt(GameItem item,string tag,int value) => item.ModifyTag(tag,s=>s.Enable().SetInt(value));
        public static int Index(GameItem item) => item==null ? -1 : item.GetTagReadonly(Rules.CropTag).GetInt()-1;
        private static void Harvest(CropDefinition crop,PlantHarvestContext ctx)
        {
            int count=Math.Max(0,ctx.harvestCount);
            int care=CropCare.Score(ctx.seed,ctx.health);
            ctx.hydroponic.DisableTag(Rules.LastGiantTag);
            bool breed=crop.Key=="ancient_fruit" && ctx.seed.IsTag(Rules.BreedTag) && ctx.seed.GetTagReadonly(Rules.BreedTag).GetBool();
            if(breed) count=Rules.HalfFruit(count);
            if(crop.Sized)
            {
                var split=GiantHarvest.Resolve(ctx.hydroponic,crop,count,care,ModuleHelper.GetAjustedQuality(ctx.hydroponic),RNG.GetRandomInt(0,99));
                OutputProduce(crop,ctx,split.small,0,care);
                OutputProduce(crop,ctx,split.normal,1,care);
                OutputProduce(crop,ctx,split.huge,3,care);
            }
            else OutputProduce(crop,ctx,count,1,care);
            if(crop.Key=="parsnip") ctx.OutputById(crop.SeedId);
            if(breed)
            {
                int seeds=Rules.BatchSeeds(PlantGeneticsHelper.GetStatMultiplier(ctx.seed,"SEED_YIELD"),UnityEngine.Random.value);
                for(int i=0;i<seeds;i++)
                {
                    var seed=DirectoryMaster.Item(crop.SeedId);
                    PlantGeneticsHelper.InheritTraits(ctx.seed,seed);
                    // Already multiplied once for this entire batch. Do not call ctx.Output(seed),
                    // which would apply the native per-item seed multiplier a second time.
                    if(GraphUtils.TryAcceptAll(ctx.output,seed)>0) GraphUtils.EmporiumTryAdd(seed);
                }
            }
        }
        private static void OutputProduce(CropDefinition crop,PlantHarvestContext ctx,int count,int size,int care)
        {
            for(int i=0;i<count;i++)
            {
                GameItem item=DirectoryMaster.Item(Rules.ProduceId(crop,size));
                SetInt(item,Rules.CropFormatTag,Rules.CropFormat);
                SetInt(item,Rules.CareTag,care);
                if(crop.WineKind>0) SetInt(item,WineHelper.BERRY_CONDITION_INT,Rules.RollCondition(care,RNG.GetRandomInt(0,99)));
                ctx.Output(item);
            }
        }
        public static void AttachHydroponic(GameItem machine)
        {
            AutoPlant.Attach(machine);
            HydroponicControl.Attach(machine);
            var row=machine.contentWindow?.childElement as MachineryPanelRow;
            if(row==null || row.GetPanel("pelican_harvest_mode")!=null) return;
            var input=MachineHydroponic.GetInputSlot(machine);
            var mode=new MachineryButton(ModEntry.L("harvest_normal"));
            var modePanel=MachineHelper.Panel(ModEntry.L("harvest_mode"),mode,"pelican_harvest_mode").SetHidden(true);
            Action<GameItem> refresh=seed=>{
                bool ancient=seed!=null && HydroponicHelper.GetPlantId(seed)==Rules.Crops[Rules.AncientIndex].SeedId;
                bool breeding=ancient && seed.GetTagReadonly(Rules.BreedTag).GetBool();
                mode.SetText(ModEntry.L(breeding?"harvest_breeding":"harvest_normal"));
                bool hidden=!ancient;
                if(modePanel.isHidden!=hidden) modePanel.SetHidden(hidden);
            };
            mode.SetCallback(()=>{
                var seed=input.childItem;
                if(seed==null || HydroponicHelper.GetPlantId(seed)!=Rules.Crops[Rules.AncientIndex].SeedId)
                { ModEntry.Message("breeding_requires_ancient"); return; }
                seed.ModifyTag(Rules.BreedTag,s=>s.Enable().SetBool(!s.GetBool()));
                refresh(seed); seed.Validate();
            });
            // Slot callbacks can run before childItem reflects the mutation.
            // Use the added item directly, and treat a removal as an empty slot.
            input.onSlotAddItemFunc+=(seed,inv,slot)=>refresh(seed);
            input.onSlotRemoveItemFunc+=(seed,inv,slot)=>refresh(null);
            machine.onLoaded+=self=>refresh(input.childItem);
            // Extend the native row in place. Existing storage indices, live
            // inventories, the machine window and other mods' panels stay intact.
            row.Add(modePanel);
            refresh(input.childItem);
            var nativeSprites=machine.getItemSpriteModifierForItemFunc;
            machine.getItemSpriteModifierForItemFunc=self=>{
                var seed=MachineHydroponic.GetInputSlot(self)?.childItem;
                var crop=seed==null?null:Rules.BySeed(HydroponicHelper.GetPlantId(seed));
                if(crop==null) return nativeSprites(self);
                var result=new List<ItemSpriteModifier>();
                if(MachineProgressHelper.GetMachineState(self)=="STATE_WORKING" || HydroponicControl.RequiresHatchet(seed,self))
                {
                    // Like the native hydroponic, render a complete mature
                    // plant overlay throughout growth, including paused parents.
                    var sprite=Assets.Sprite("growth_"+crop.Key+"_2");
                    if(sprite!=null) result.Add(new ItemSpriteModifier(sprite,Vector2.zero,0,false,0,false,Color.white));
                }
                return result;
            };
        }
    }
}
