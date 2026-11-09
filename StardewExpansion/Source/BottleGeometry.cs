using UnityEngine;
using System.Collections.Generic;

namespace PelicanCultivation
{
    public static class BottleGeometry
    {
        public const string CompactEmptyTag="PELICAN_COMPACT_EMPTY_BOOL", CompactSprite="compact_empty_bottle";
        public const string PendingBeerTag="PELICAN_BEER_SPACE_PENDING_BOOL";
        private static readonly HashSet<GameItem> pending=new HashSet<GameItem>(),updating=new HashSet<GameItem>();
        private static float nextRetry;
        public static bool IsBeer(GameItem item)
        { int kind=item.GetTagReadonly(Rules.WineTag).GetInt(); return kind==1 || kind==2; }
        public static void ApplyFinished(GameItem item,int kind)
        {
            if(!updating.Add(item)) return;
            var original=item.shape.Clone();
            var placed=item.modifiedShape.Clone();
            int xOrigin=item.modifiedXOrigin,yOrigin=item.modifiedYOrigin;
            try
            {
                Crops.Sprite(item,"bottle_"+Rules.WineKeys[kind],Rules.WineWidth(kind),Rules.WineHeight(kind));
                // Per-item load callbacks can run before the entire inventory
                // graph exists. Capacity changes wait for GameLoadedLate.
                if(!Rules.WideBottle(kind) || (Progress.Ready && PlaceExpanded(item)))
                {
                    item.DisableTag(PendingBeerTag);
                    pending.Remove(item);
                    return;
                }
                // Preserve the existing item and location if every inventory is
                // full. The finished wine keeps its value, quality and aging.
                item.SetShape(original);
                item.modifiedShape=new GridShapeBuilder(item.modifiedShape).SetTransform(placed);
                item.modifiedXOrigin=xOrigin; item.modifiedYOrigin=yOrigin;
                item.EnableTag(PendingBeerTag);
                pending.Add(item);
                item.Validate();
            }
            finally { updating.Remove(item); }
        }
        public static void RetryPending()
        {
            if(pending.Count==0 || Time.unscaledTime<nextRetry) return;
            nextRetry=Time.unscaledTime+3;
            foreach(var item in new List<GameItem>(pending))
            {
                if(item==null || item.unitCount<1 || (item is GameItemElement element && element.IsDestroyed()) || !WineHelper.IsFinishedWine(item) || !Rules.WideBottle(item.GetTagReadonly(Rules.WineTag).GetInt()))
                { pending.Remove(item); continue; }
                ApplyFinished(item,item.GetTagReadonly(Rules.WineTag).GetInt());
            }
        }
        public static bool EnsureEmptyRoom(GameItem item,bool notify)
        {
            if(!item.IsTag(CompactEmptyTag)) return true;
            if(!Progress.Ready) { KeepCompact(item); return false; }
            item.useSpriteFromMod=false;
            item.SetSpriteAndShape("Items/items_winemaking","wine_bottle");
            if(PlaceExpanded(item)) { item.DisableTag(CompactEmptyTag); return true; }
            KeepCompact(item);
            if(notify) ModEntry.Message("bottle_space");
            return false;
        }
        public static void AfterEmpty(GameItem item,bool wasBeer)
        {
            if(!wasBeer) return;
            pending.Remove(item);
            item.DisableTag(PendingBeerTag);
            if(PlaceExpanded(item)) { item.DisableTag(CompactEmptyTag); return; }
            // No fitting four-cell slot: retain a compact empty bottle inside
            // the former beer footprint, without discarding other items.
            KeepCompact(item);
        }
        public static bool PlaceExpanded(GameItem item)
        {
            var parent=item.parentInventory;
            if(parent==null || parent.CheckInventorySlotStillValid(item,item.modifiedShape,item.modifiedState)) return true;
            var slot=parent.TryFindOneValidInventorySlot(item);
            if(slot!=null && slot.targetItem==null && slot.TryAcceptOnce()==item.unitCount) return true;
            if(EmporiumEntry.Instance==null) return false;
            foreach(var target in new GraphNodeStorage[]{EmporiumEntry.Instance.invElement,EmporiumEntry.Instance.backInvinvElement,EmporiumEntry.Instance.frontInvinvElement})
            {
                if(target==parent || !GraphUtils.CanAccept(target,item)) continue;
                if(GraphUtils.TryAcceptAll(target,item)==0 && item.parentInventory!=null) return true;
            }
            return false;
        }
        private static void KeepCompact(GameItem item)
        {
            var mod=ModLoader.Mods.Find(m=>m.Manifest.ID==ModEntry.ModId);
            if(!mod.Sprites.ContainsKey(CompactSprite))
            {
                var native=RenderHandler.LoadFromAtlas("Items/items_winemaking","wine_bottle");
                var pivot=new Vector2(native.pivot.x/native.rect.width,native.pivot.y/native.rect.height);
                mod.Sprites[CompactSprite]=UnityEngine.Sprite.Create(native.texture,native.rect,pivot,native.pixelsPerUnit*2);
            }
            item.EnableTag(CompactEmptyTag);
            Crops.BindSprite(item,CompactSprite,1,2);
        }
    }
}
