using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using PelicanCultivation;

// Real product factories, rollback helper and native SlotMarker.TryAcceptOnce;
// scene rendering and inventory storage callbacks form the headless boundary.
public static class BaitGeometryVerification
{
    private static int checks,nextId=980000;
    private static bool ready=true;
    private static void Check(bool value,string message){if(!value)throw new Exception("BAIT GEOMETRY FAIL: "+message);checks++;}
    private sealed class Item:GameItem
    {
        public Item(string id):base(id){uniqueId=++nextId;modifiedState=state;SetShape(new GridShapeBuilder().SetData(new byte[]{1},1));}
        public override void Validate(){modifiedState=state;}
        public override void Destroy(){throw new Exception("Migration must never destroy a bait stack");}
    }
    private sealed class Grid:GameGridInventory
    {
        public int Removed,Added,StackOffers;
        public Grid():base(1,1){}
        public override void Validate(){}
        public override bool CheckInventorySlotStillValid(GameItem item,GridShape shape,TagSystem state)
        {return shape.FullyIntersects(inventoryShape,(a,b)=>a!=1||b==0)&&childItems.All(other=>other==item||!shape.Intersects(other.modifiedShape));}
        public override SlotMarker TryFindOneValidInventorySlot(GameItem item)
        {
            foreach(var other in childItems)if(other!=item&&item.MayStack(other))
            {StackOffers++;return new SlotMarker{item=item,inventory=this,targetItem=other,numTransfer=item.unitCount,itemGridShape=item.modifiedShape};}
            for(int y=0;y<inventoryShape.height;y++)for(int x=0;x<inventoryShape.width;x++)
            {
                var candidate=new GridShapeBuilder(item.modifiedShape).SetTransform(x,y,false,0);
                if(CheckInventorySlotStillValid(item,candidate,item.modifiedState))return new SlotMarker{item=item,inventory=this,numTransfer=item.unitCount,itemGridShape=candidate};
            }
            return null;
        }
        public override bool Expel(GameItem item)
        {
            if(item.parentInventory!=this||!childItems.Contains(item))return false;
            item.onSlotRemoveItemFunc?.Invoke(item,this,GetIncompleteItemInventorySlot(item));
            ((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(this)).Remove(item);item.parentInventory=null;Removed++;return true;
        }
        public override bool UncheckedAccept(GameItem item)
        {
            if(item.parentInventory!=null||childItems.Contains(item))return false;
            ((List<GameItem>)Field(typeof(GameGridInventory),"items").GetValue(this)).Add(item);item.parentInventory=this;Added++;
            item.onSlotAddItemFunc?.Invoke(item,this,GetIncompleteItemInventorySlot(item));item.Validate();return true;
        }
        public override SlotMarker GetIncompleteItemInventorySlot(GameItem item)=>new SlotMarker{item=item,inventory=this,itemGridShape=item.modifiedShape,numTransfer=item.unitCount};
    }
    private static FieldInfo Field(Type type,string name)=>AccessTools.Field(type,name);
    private static Grid NewGrid(int width,int height,params GameItem[] items)
    {
        var grid=(Grid)FormatterServices.GetUninitializedObject(typeof(Grid));
        Field(typeof(GameGridInventory),"items").SetValue(grid,new List<GameItem>(items));
        Field(typeof(GameGridInventory),"<inventoryShape>k__BackingField").SetValue(grid,new GridShapeBuilder().SetData(new byte[width*height],width).SetDataOutside(1).Build());
        foreach(var item in items)item.parentInventory=grid;return grid;
    }
    private static void Position(GameItem item,int x,int y)=>item.modifiedShape=new GridShapeBuilder(item.modifiedShape).SetTransform(x,y,false,0);
    public static bool Empty(string identifier,ref GameItem __result){__result=new Item(identifier);return false;}
    public static bool Sprite(GameItem item,string key,int width,int height){Crops.BindSprite(item,key,width,height);return false;}
    public static bool Text(string key,object[] args,ref string __result){__result=key;return false;}
    public static bool Ready(ref bool __result){__result=ready;return false;}
    public static bool MaxRemove(GameItem __instance,ref int __result){__result=__instance.unitCount;return false;}
    private static void Patch(Harmony harmony,MethodBase target,string prefix)=>harmony.Patch(target,new HarmonyMethod(typeof(BaitGeometryVerification),prefix));
    public static string Run()
    {
        checks=0;var harmony=new Harmony("pelican_cultivation.bait_geometry_verification");
        try
        {
            Patch(harmony,AccessTools.Method(typeof(ItemDirectory),"CreateEmptyItem",new[]{typeof(string)}),"Empty");
            Patch(harmony,AccessTools.Method(typeof(Crops),"Sprite"),"Sprite");
            Patch(harmony,AccessTools.Method(typeof(ModEntry),"L"),"Text");
            Patch(harmony,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"Ready");
            Patch(harmony,AccessTools.Method(typeof(GameItem),"MaxNumRemove"),"MaxRemove");
            foreach(int index in new[]{8,9})
            {
                string key=Expansion.ProductKeys[index];var item=Expansion.Product(index);
                Check(item.shape.width==2&&item.shape.height==2&&item.GetItemSize()==4,"new bait occupies a complete 2x2 square: "+key);
                Check(ProductGeometry.Handles(key),"load/retry geometry handler includes bait: "+key);
                item.SetUnitCount(7);item.unitValue=73;item.EnableTag("TEST_BAIT_METADATA");GeneralHelper.SetItemOwned(item,true);int id=item.uniqueId;
                item.SetShape(new GridShapeBuilder().SetData(new byte[]{1},1));Position(item,0,0);
                var blocker=new Item("blocker");Position(blocker,1,0);var same=Expansion.Product(index);same.SetUnitCount(13);GeneralHelper.SetItemOwned(same,true);Position(same,2,0);
                // The other matching stack fits beside the old one, but there
                // is no spare 2x2 area for this legacy stack to grow into.
                var grid=NewGrid(4,2,item,blocker,same);ready=true;
                var stackRule=item.mayStackItemFunc;int removed=0,added=0;
                item.onSlotRemoveItemFunc+=(a,b,c)=>removed++;item.onSlotAddItemFunc+=(a,b,c)=>added++;
                Check(item.MayStack(same),"fixture contains a real merge-compatible same-kind stack");
                item.onLoaded?.Invoke(item);
                Check(item.GetItemSize()==1&&item.IsTag(ProductGeometry.Pending)&&item.parentInventory==grid,"full legacy bag waits at the exact old footprint (area="+item.GetItemSize()+", pending="+item.IsTag(ProductGeometry.Pending)+", moved="+grid.Removed+", stacks="+grid.StackOffers+")");
                Check(item.unitCount==7&&same.unitCount==13&&item.uniqueId==id&&item.unitValue==73&&item.IsTag("TEST_BAIT_METADATA")&&GeneralHelper.IsItemOwned(item)&&grid.childItems.Count==3,"failed resize preserves both stack counts, identity, value, ownership and metadata");
                Check(grid.StackOffers==0&&grid.Removed==0&&grid.Added==0&&removed==0&&added==0&&item.mayStackItemFunc==stackRule,"pending migration neither merges nor fires movement callbacks and restores stacking rule");
                // Free a whole 2x2 space below while keeping the old cell
                // blocked; the real native SlotMarker must relocate once.
                Field(typeof(GameGridInventory),"<inventoryShape>k__BackingField").SetValue(grid,new GridShapeBuilder().SetData(new byte[16],4).SetDataOutside(1).Build());
                Field(typeof(ProductGeometry),"next").SetValue(null,0f);Field(typeof(UnityEngine.Time),"CodexTime").SetValue(null,3f);ProductGeometry.Retry();
                Check(item.GetItemSize()==4&&!item.IsTag(ProductGeometry.Pending)&&item.parentInventory==grid,"legacy bait moves into a real free 2x2 slot when space is restored");
                Check(item.modifiedShape.minY>=1&&!item.modifiedShape.Intersects(blocker.modifiedShape)&&!item.modifiedShape.Intersects(same.modifiedShape),"relocated footprint overlaps neither neighboring item");
                Check(item.unitCount==7&&same.unitCount==13&&item.uniqueId==id&&item.unitValue==73&&grid.childItems.Count==3,"successful resize preserves the exact stacks and their quantities");
                Check(grid.Removed==1&&grid.Added==1&&removed==1&&added==1&&grid.StackOffers==0&&item.mayStackItemFunc==stackRule,"actual SlotMarker.TryAcceptOnce invokes removal/addition exactly once without merging");
                ProductGeometry.Apply(item,key);Check(grid.Removed==1&&grid.Added==1&&item.unitCount==7,"repeated refresh cannot move or duplicate an already valid stack");
                Check(item.MayStack(same),"ordinary gameplay stacking remains available after migration");
                item.SetShape(new GridShapeBuilder().SetData(new byte[]{1},1));Position(item,0,0);ready=false;ProductGeometry.Apply(item,key);
                Check(item.GetItemSize()==1&&item.IsTag(ProductGeometry.Pending)&&item.unitCount==7,"early load waits for the complete inventory graph");
                ready=true;ProductGeometry.Apply(item,key);Check(item.GetItemSize()==4&&!item.IsTag(ProductGeometry.Pending)&&item.unitCount==7,"late-ready retry safely completes early-load migration");
            }
        }
        finally{harmony.UnpatchAll(harmony.Id);ready=true;}
        return "Passed "+checks+" bait 2x2 geometry, full-backpack rollback, native slot relocation and stack-preservation checks.";
    }
}
