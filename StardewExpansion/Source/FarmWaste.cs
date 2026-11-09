using System;
using System.Collections.Generic;
using System.Linq;

namespace PelicanCultivation
{
    public static class FarmWaste
    {
        public static int Moved {get;private set;}
        private static readonly Dictionary<GameItem,GameGridInventory> blockedItems=new Dictionary<GameItem,GameGridInventory>();
        public static bool TrashBlocked => blockedItems.Any(p=>p.Key.parentInventory==p.Value);
        private static readonly HashSet<GameItem> emptiedRobots=new HashSet<GameItem>();
        public static void Reset(){Moved=0;blockedItems.Clear();emptiedRobots.Clear();}
        private static void MarkBlocked(GameGridInventory source)
        {
            if(source==null) return;
            foreach(var item in source.childItems.Where(i=>i.IsTag("ANIMAL_FECES_TAG") && GeneralHelper.IsItemOwned(i))) blockedItems[item]=source;
        }
        public static bool Robot(GameItem item) => item!=null && item.identifier=="vacuum_robot" && GeneralHelper.IsItemOwned(item);
        public static GameGridInventory RobotStorage(GameItem robot) => MachineHelper.GetBodyStorage(robot,1) as GameGridInventory;
        public static GameGridInventory TrashStorage(GameItem trashcan)
        {
            // trashcanInvElement is a factory-populated cache: creating a
            // temporary/probe trashcan can redirect it away from the fixture.
            // Resolve only the live scene item's own attached storage instead.
            if(trashcan?.contentWindow==null) return null;
            return trashcan.contentWindow.child as GameGridInventory
                ?? GraphUtils.FindAllChildrenType<GameGridInventory>(trashcan.contentWindow).FirstOrDefault();
        }
        public static double FillRatio(GameGridInventory grid)
        {
            if(grid?.inventoryShape==null) return 0;
            int capacity=0;
            for(int y=0;y<grid.inventoryShape.height;y++) for(int x=0;x<grid.inventoryShape.width;x++)
                if(grid.inventoryShape.Get(x,y)==0) capacity++;
            return capacity<=0?1:Math.Min(1,grid.childItems.Sum(i=>(double)i.GetItemSize())/capacity);
        }
        public static bool ShouldEmpty(GameGridInventory storage,int threshold)
            => storage!=null && storage.childItems.Exists(i=>i.IsTag("ANIMAL_FECES_TAG")) && FillRatio(storage)*100>=Math.Max(1,Math.Min(100,threshold));
        public static int MoveFeces(GameGridInventory source,GameGridInventory trash)
        {
            if(source==null || trash==null || source==trash) return 0;
            int moved=0;
            foreach(var feces in source.childItems.Where(i=>i.IsTag("ANIMAL_FECES_TAG") && GeneralHelper.IsItemOwned(i)).ToList())
            {
                if(!feces.MayRemove() || !GraphUtils.CanAccept(trash,feces)){blockedItems[feces]=source;continue;}
                int units=Math.Max(1,feces.unitCount);
                // Use a normal transfer. Never destroy rejected items or push
                // them through a full bin using unchecked inventory methods.
                int remaining=GraphUtils.TryAcceptAll(trash,feces);
                moved+=Math.Max(0,units-Math.Max(0,remaining));
                if(remaining>0) blockedItems[feces]=source;else blockedItems.Remove(feces);
            }
            return moved;
        }
        public static void ServiceCage(GameGridInventory cage,GameGridInventory trash,int threshold)
        {
            if(cage==null || trash==null) return;
            var robots=cage.childItems.Where(Robot).ToList();
            if(robots.Count==0){Moved+=MoveFeces(cage,trash);return;}
            foreach(var robot in robots)
            {
                if(emptiedRobots.Contains(robot)) continue;
                var storage=RobotStorage(robot);
                if(!ShouldEmpty(storage,threshold)) continue;
                int count=MoveFeces(storage,trash);Moved+=count;
                if(count>0) emptiedRobots.Add(robot);
            }
        }
        public static void Service(IEnumerable<GameItem> items)
        {
            if(!ModEntry.Enabled || !Progress.Ready || !FarmSettings.AutoFeces) return;
            var cages=items.Where(i=>i!=null && i.IsTag("CAGE_TAG") && GeneralHelper.IsItemOwned(i)).Distinct().ToList();
            if(cages.Count==0) return;
            var trash=TrashStorage(EmporiumEntry.Instance.trashcan);
            foreach(var cage in cages)
            {
                try
                {
                    var grid=AnimalCage.GetCageInventory(cage);if(grid==null) continue;
                    if(trash==null)
                    {
                        var robots=grid.childItems.Where(Robot).ToList();
                        if(robots.Count==0) MarkBlocked(grid);
                        else foreach(var robot in robots)
                        {
                            var storage=RobotStorage(robot);
                            if(!emptiedRobots.Contains(robot) && ShouldEmpty(storage,FarmSettings.RobotTrashPercent)) MarkBlocked(storage);
                        }
                        continue;
                    }
                    ServiceCage(grid,trash,FarmSettings.RobotTrashPercent);
                }
                catch(Exception error){ModEntry.Error("Feces transfer to trash",error);}
            }
        }
    }
}
