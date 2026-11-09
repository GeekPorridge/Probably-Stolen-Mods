using System;
using System.Collections.Generic;
using UnityEngine;

namespace PelicanCultivation
{
    public static class CropGeometry
    {
        public const string PendingTag="PELICAN_CROP_SPACE_PENDING_BOOL";
        private static readonly HashSet<GameItem> pending=new HashSet<GameItem>(),updating=new HashSet<GameItem>();
        private static float nextRetry;
        public static void PrepareLegacy(GameItem item,CropDefinition crop)
        {
            int oldSize=Math.Max(0,Math.Min(3,item.GetTagReadonly(Rules.SizeTag).GetInt()));
            int size=Rules.Size(crop,item.GetTagReadonly(Rules.SizeTag).GetInt());
            int oldFormat=item.GetTagReadonly(Rules.CropFormatTag).GetInt();
            if(oldFormat<Rules.CropFormat)
            {
                bool oldGeometry=item.shape.width!=Rules.CropWidth(crop,size) || item.shape.height!=Rules.CropHeight(crop,size);
                if(oldGeometry || oldSize!=size)
                {
                    double oldMultiplier=(oldFormat>=17?new[]{0.5,1,2,3}:new[]{0.5,1,2.5,3})[oldSize];
                    int price=CropEconomy.PreviousPrice(crop);
                    long oldBase=Math.Max(1,(long)Math.Round(price*oldMultiplier,MidpointRounding.AwayFromZero));
                    long newBase=(long)Math.Round(price*(crop.Sized?Rules.SizePrices[size]:1),MidpointRounding.AwayFromZero);
                    item.unitValue=(long)Math.Round((double)item.unitValue*newBase/oldBase,MidpointRounding.AwayFromZero);
                    int oldCalories=oldSize==0?50:oldSize==3?300:oldSize==2?(oldFormat>=17?200:250):100;
                    if(item.IsTag("CALORIE_VALUE_TAG"))
                        Crops.SetInt(item,"CALORIE_VALUE_TAG",Math.Max(item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()>0?1:0,(int)Math.Round(item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()*(double)(size==0?50:size==3?300:100)/oldCalories,MidpointRounding.AwayFromZero)));
                }
                Crops.SetInt(item,Rules.CropFormatTag,Rules.CropFormat);
            }
            Crops.SetInt(item,Rules.SizeTag,size);
            CropEconomy.Upgrade(item,crop);
        }
        public static void Apply(GameItem item,CropDefinition crop,int size)
        {
            if(!updating.Add(item)) return;
            try
            {
                bool applied=TryApplyShape(item,()=>Crops.Sprite(item,crop.Key+(crop.Sized?Rules.SizeSuffixes[size]:""),Rules.CropWidth(crop,size),Rules.CropHeight(crop,size)),candidate=>Progress.Ready && BottleGeometry.PlaceExpanded(candidate));
                if(applied) {item.DisableTag(PendingTag);pending.Remove(item);}
                else {item.EnableTag(PendingTag);pending.Add(item);}
            }
            finally {updating.Remove(item);}
        }
        public static bool TryApplyShape(GameItem item,Action applyCandidate,Func<GameItem,bool> place)
        {
            var original=item.shape.Clone();
            var placed=item.modifiedShape.Clone();
            int xOrigin=item.modifiedXOrigin,yOrigin=item.modifiedYOrigin;
            Action restore=()=>{
                item.SetShape(original);
                item.modifiedShape=new GridShapeBuilder(item.modifiedShape).SetTransform(placed);
                item.modifiedXOrigin=xOrigin;item.modifiedYOrigin=yOrigin;
                item.Validate();
            };
            try
            {
                applyCandidate();
                bool same=original.width==item.shape.width && original.height==item.shape.height;
                if(same || place(item)) return true;
                restore();return false;
            }
            catch {restore();throw;}
        }
        public static void RetryPending()
        {
            if(pending.Count==0 || Time.unscaledTime<nextRetry) return;
            nextRetry=Time.unscaledTime+3;
            foreach(var item in new List<GameItem>(pending))
            {
                int index=Crops.Index(item);
                if(item==null || item.unitCount<1 || (item is GameItemElement element && element.IsDestroyed()) || index<0 || index>=Rules.Crops.Length)
                {pending.Remove(item);continue;}
                var crop=Rules.Crops[index];
                Apply(item,crop,Rules.Size(crop,item.GetTagReadonly(Rules.SizeTag).GetInt()));
            }
        }
    }
}
