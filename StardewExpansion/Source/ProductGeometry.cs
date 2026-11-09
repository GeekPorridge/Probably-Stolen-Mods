using System;
using System.Collections.Generic;
using UnityEngine;

namespace PelicanCultivation
{
    public static class ProductGeometry
    {
        public const string Pending="PELICAN_PRODUCT_SPACE_PENDING";
        private static readonly HashSet<GameItem> pending=new HashSet<GameItem>(),updating=new HashSet<GameItem>();
        private static float next;
        public static bool IsBait(string key)=>key=="bait"||key=="premium_bait";
        public static bool Handles(string key)=>IsBait(key)||key=="cheese"||key=="goat_cheese"||key=="truffle_oil"||key=="poppy_perfume"||key=="fairy_perfume"||Array.IndexOf(Fishing.Kings,key)>=0;
        public static void Apply(GameItem item,string key)
        {
            if(!Handles(key)||!updating.Add(item))return;
            try
            {
                bool fit=CropGeometry.TryApplyShape(item,()=>Crops.Sprite(item,key,Expansion.ShapeWidth(key),Expansion.ShapeHeight(key)),Place);
                item.SetEnableDisableTag(Pending,!fit);if(fit)pending.Remove(item);else pending.Add(item);
            }finally{updating.Remove(item);}
        }
        private static bool Place(GameItem item)
        {
            var parent=item.parentInventory;if(parent==null)return true;
            if(!Progress.Ready)return false;
            if(item.identifier=="pelican_bait"||item.identifier=="pelican_premium_bait")
            {
                if(parent.CheckInventorySlotStillValid(item,item.modifiedShape,item.modifiedState))return true;
                // Resizing is a move of this exact stack, never a stack merge.
                // Keep it in its current container; a full old backpack waits
                // instead of sending bait to another area or losing its ID.
                var stacking=item.mayStackItemFunc;item.mayStackItemFunc=(a,b)=>false;
                try
                {
                    var space=parent.TryFindOneValidInventorySlot(item);
                    return space!=null&&space.inventory==parent&&space.targetItem==null&&space.numTransfer==item.unitCount&&space.TryAcceptOnce()==item.unitCount;
                }
                finally{item.mayStackItemFunc=stacking;}
            }
            if(GeneralHelper.IsItemOwned(item))return BottleGeometry.PlaceExpanded(item);
            if(parent.CheckInventorySlotStillValid(item,item.modifiedShape,item.modifiedState))return true;
            var slot=parent.TryFindOneValidInventorySlot(item);return slot!=null&&slot.targetItem==null&&slot.TryAcceptOnce()==item.unitCount;
        }
        public static void Retry()
        {
            if(pending.Count==0||Time.unscaledTime<next)return;next=Time.unscaledTime+3;
            foreach(var item in new List<GameItem>(pending))
            {
                if(item==null||item.unitCount<1||(item is GameItemElement element&&element.IsDestroyed())){pending.Remove(item);continue;}
                Apply(item,item.identifier.Substring(8));
            }
        }
    }
}
