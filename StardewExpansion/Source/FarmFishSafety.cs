using System;
using System.Collections.Generic;

namespace PelicanCultivation
{
    public static class FarmFishSafety
    {
        public static List<GridShape> Plan(GridShape inventory,IEnumerable<GameItem> existing,GameItem source,IList<GameItem> targets)
        {
            int width=inventory.width,height=inventory.height;var occupied=new bool[width,height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) occupied[x,y]=inventory.Get(x,y)!=0;
            foreach(var item in existing) if(item!=source)
                for(int y=item.modifiedShape.minY;y<=item.modifiedShape.maxY;y++) for(int x=item.modifiedShape.minX;x<=item.modifiedShape.maxX;x++)
                    if(x>=0 && y>=0 && x<width && y<height && item.modifiedShape.Get(x,y)==1) occupied[x,y]=true;
            var result=new List<GridShape>();
            foreach(var item in targets)
            {
                GridShape found=null;
                for(int orientation=0;orientation<4 && found==null;orientation++)
                {
                    var shape=new GridShapeBuilder(item.shape).SetTransform(0,0,false,orientation).Build();
                    for(int y=0;y<=height-shape.globalHeight && found==null;y++) for(int x=0;x<=width-shape.globalWidth;x++)
                    {
                        bool fits=true;
                        for(int cy=0;cy<shape.globalHeight && fits;cy++) for(int cx=0;cx<shape.globalWidth;cx++)
                            if(shape.Get(cx,cy)==1 && occupied[x+cx,y+cy]) {fits=false;break;}
                        if(!fits) continue;
                        found=new GridShapeBuilder(shape).SetPosition(x,y).Build();break;
                    }
                }
                if(found==null) return null;
                result.Add(found);
                for(int y=found.minY;y<=found.maxY;y++) for(int x=found.minX;x<=found.maxX;x++) if(found.Get(x,y)==1) occupied[x,y]=true;
            }
            return result;
        }
        public static void Convert(GameItem source,FishSpecies species,GameGridInventory tank,bool hatch,Dictionary<GameItem,float> gained=null)
        {
            SeaFounderPair.RecoverPurchasedBatch(source,species);
            int count=hatch?1:Math.Max(1,source.GetTagReadonly("FISH_GROUP_COUNT_TAG").GetInt());
            if(count>tank.inventoryShape.width*tank.inventoryShape.height){Pause(source,tank);return;}
            bool founderPair=SeaFounderPair.Guarantees(source,species,count,hatch);
            var targets=new List<GameItem>();var originalShape=source.modifiedShape.Clone();
            try
            {
                for(int i=0;i<count;i++)
                {
                    var child=DirectoryMaster.Item(hatch?species.fryIdentifier:species.fishIdentifier);
                    targets.Add(child);Prepare(source,child,species,count,hatch);
                    if(!hatch) SeaFounderPair.PrepareIndividual(child,i,founderPair);
                    GeneralHelper.SetItemOwned(child,GeneralHelper.IsItemOwned(source));
                    AquacultureHelper.UpdateNameAndSprite(child);FarmSpecies.RefreshFish(child);
                }
                var plan=Plan(tank.inventoryShape,tank.childItems,source,targets);
                if(plan==null){Pause(source,tank);return;}
                // The original is expelled, never destroyed, until every child
                // has been placed. Rollback keeps its full tag/genetic state.
                if(!tank.Expel(source)){Pause(source,tank);return;}
                bool committed=false;
                try
                {
                    for(int i=0;i<targets.Count;i++)
                    {
                        var marker=tank.TryInventorySlot(targets[i],plan[i]);
                        if(marker==null || marker.targetItem!=null || marker.TryAcceptOnce()!=1 || targets[i].parentInventory!=tank) return;
                    }
                    source.Destroy();committed=true;
                    if(founderPair) SeaFounderPair.Commit();
                    foreach(var child in targets) {child.DisableTag("PELICAN_FARM_TRANSITION_PENDING_BOOL");if(gained!=null) gained[child]=FarmRules.FryTransitionGrams(species,count);}
                    targets.Clear();
                }
                finally
                {
                    if(!committed)
                    {
                        foreach(var child in targets) child.Destroy();targets.Clear();
                        source.modifiedShape=new GridShapeBuilder(originalShape);tank.UncheckedAccept(source);
                        source.Validate();Pause(source,tank);
                    }
                }
            }
            catch(Exception e){ModEntry.Error("Safe aquatic stage conversion",e);Pause(source,tank);}
            finally {foreach(var child in targets) child.Destroy();}
        }
        public static void Prepare(GameItem source,GameItem child,FishSpecies species,int count,bool hatch)
        {
            string sex=child.GetTagReadonly("FISH_SEX_TAG").GetString();
            child.state=new TagSystem();foreach(var pair in source.state.dict) child.state.dict[pair.Key]=pair.Value.Clone();
            child.modifiedState=child.state.Clone();
            child.DisableTag("FISH_ROE_TAG");child.DisableTag("FISH_FERTILIZED_TAG");child.DisableTag("PELICAN_FARM_TRANSITION_PENDING_BOOL");
            child.DisableTag("CALORIE_VALUE_TAG");child.DisableTag("MEAT_FOOD_TAG");child.DisableTag("MEAT_TYPE_TAG");
            if(hatch)
            {
                child.EnableTag("FISH_GROUP_TAG");child.EnableTag("FISH_FRY_TAG");
                FarmSpecies.SetFloat(child,"FISH_HUNGER_TAG",Math.Min(species.maxHunger,300));
            }
            else
            {
                child.DisableTag("FISH_GROUP_TAG");child.DisableTag("FISH_FRY_TAG");child.DisableTag("FISH_GROUP_COUNT_TAG");child.EnableTag("FISH_ITEM_TAG");
                FarmSpecies.SetString(child,"FISH_SEX_TAG",string.IsNullOrEmpty(sex)?"FEMALE":sex);
                foreach(string tag in new[]{"FISH_HUNGER_TAG","FISH_KCAL_EATEN_TAG","FISH_KCAL_GROWN_TAG","FISH_KCAL_PELLET_TAG","FISH_KCAL_INDUSTRIAL_TAG","FISH_RECENT_KCAL_TAG","FISH_RECENT_INDUSTRIAL_TAG","FISH_RECENT_MEAT_TAG"})
                    FarmSpecies.SetFloat(child,tag,source.GetTagReadonly(tag).GetFloat()/Math.Max(1,count));
            }
        }
        private static void Pause(GameItem source,GameInventory tank)
        {
            source.EnableTag("PELICAN_FARM_TRANSITION_PENDING_BOOL");
        }
    }
}
