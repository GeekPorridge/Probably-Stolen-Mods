using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using PelicanCultivation;

public static class ReservoirAccessVerification
{
    private static int checks,messages;
    private static bool aquaculture=true,closed;
    private static EmporiumEntry entry;
    private static MapUIManager map;
    private static void Check(bool value,string message){if(!value)throw new Exception("RESERVOIR ACCESS FAIL: "+message);checks++;}
    private sealed class Item:GameItem
    {
        public List<GraphNodeStorage> Edges;
        public Item(string id,int count=1):base(id){modifiedState=state;unitCount=count;GeneralHelper.SetItemOwned(this,true);}
        public override List<GraphNodeStorage> children=>Edges??base.children;
        public override void Validate(){modifiedState=state;}
        public override void Destroy(){throw new Exception("Departure inspection must not consume or destroy inventory");}
    }
    private sealed class Grid:GameGridInventory{public Grid():base(1,1){}public override void Validate(){}}
    private sealed class Slot:GameSlotInventory{public override void Validate(){}}
    private static FieldInfo Field(Type type,string name)=>AccessTools.Field(type,name);
    private static GameGridInventory GridOf(params GameItem[] items)
    {
        var grid=(Grid)FormatterServices.GetUninitializedObject(typeof(Grid));Field(typeof(GameGridInventory),"items").SetValue(grid,new List<GameItem>(items));
        foreach(var item in items)item.parentInventory=grid;return grid;
    }
    private static GameSlotInventory SlotOf(GameItem item=null)
    {
        var slot=(Slot)FormatterServices.GetUninitializedObject(typeof(Slot));Field(typeof(GameSlotInventory),"currentItem").SetValue(slot,item);if(item!=null)item.parentInventory=slot;return slot;
    }
    private static Item Bag(params GameItem[] contents)
    {var bag=new Item("travel_backpack"){Edges=new List<GraphNodeStorage>{GridOf(contents)}};bag.EnableTag("BACKPACK_TAG");return bag;}
    private static void Outside(AfterhourLocation location)=>Field(typeof(MapUIManager),"<Current>k__BackingField").SetValue(map,location);
    public static bool Ready(ref bool __result){__result=true;return false;}
    public static bool Aquaculture(ref bool __result){__result=aquaculture;return false;}
    public static bool Emporium(ref EmporiumEntry __result){__result=entry;return false;}
    public static bool Map(ref MapUIManager __result){__result=map;return false;}
    public static bool Closed(ref bool __result){__result=closed;return false;}
    public static bool Message(string key){Check(key=="v2_reservoir_bait_required_hint","blocked departure uses its precise localized explanation");messages++;return false;}
    private static void Patch(Harmony harmony,MethodBase target,string prefix)=>harmony.Patch(target,new HarmonyMethod(typeof(ReservoirAccessVerification),prefix));
    public static string Run()
    {
        checks=messages=0;bool enabled=ModEntry.Enabled;ModEntry.Enabled=true;
        var harmony=new Harmony("pelican_cultivation.reservoir_access_verification");
        try
        {
            Patch(harmony,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"Ready");Patch(harmony,AccessTools.PropertyGetter(typeof(FarmStock),"Aquaculture"),"Aquaculture");
            Patch(harmony,AccessTools.PropertyGetter(typeof(EmporiumEntry),"Instance"),"Emporium");Patch(harmony,AccessTools.PropertyGetter(typeof(MapUIManager),"Instance"),"Map");
            Patch(harmony,AccessTools.Method(typeof(AfterhourLocation),"IsClosedByEvent"),"Closed");Patch(harmony,AccessTools.Method(typeof(ModEntry),"Message"),"Message");
            entry=(EmporiumEntry)FormatterServices.GetUninitializedObject(typeof(EmporiumEntry));map=(MapUIManager)FormatterServices.GetUninitializedObject(typeof(MapUIManager));
            var location=new ReservoirLocation();Outside(null);aquaculture=true;closed=false;
            var ordinary=new Item("pelican_bait",5);var premium=new Item("pelican_premium_bait",3);var inner=Bag(premium);var unownedInner=Bag(new Item("pelican_bait",20));GeneralHelper.SetItemOwned(unownedInner,false);
            var backpack=Bag(ordinary,inner,unownedInner,new Item("not_bait",20));GridOf(backpack);entry.selectedBackpack=backpack;
            entry.afterhourPocketSlotInvBackpack=SlotOf();entry.afterhourInventory=GridOf(new Item("pelican_bait",20));entry.afterhourPocketSlotInvLeft=SlotOf(new Item("pelican_bait",20));entry.afterhourPocketSlotInvRight=SlotOf(new Item("pelican_premium_bait",20));
            Check(ReservoirAccess.CountInside(backpack)==8,"actual graph traversal includes owned inner bags and excludes unowned inner bags and unrelated goods");
            Check(ReservoirAccess.CountForDeparture(entry,false)==8&&ReservoirAccess.CountForDeparture(entry,true)==0,"before departure uses selectedBackpack, not the still-empty afterhour slot");
            Check(location.IsDiscovered()&&location.IsAvailable(),"a carried-bait reservoir remains discovered and available before native bag transfer");
            Check(ReservoirDeparturePatch.Prefix(map,location)&&messages==0,"departure boundary admits a selected backpack with bait without a false warning");
            entry.selectedBackpack=Bag(new Item("pelican_bait",10));
            Check(!location.IsAvailable(),"an unplaced stale selected bag cannot count as a backpack that native transfer will carry");
            entry.selectedBackpack=backpack;ordinary.unitCount=0;premium.unitCount=0;
            Check(location.IsDiscovered()&&!location.IsAvailable(),"no-bait reservoir stays visible but unavailable while ground and side-pocket bait are ignored");
            Check(!ReservoirDeparturePatch.Prefix(map,location)&&messages==1,"removing bait after menu availability is checked blocks the departure boundary and explains why");
            var travel=AccessTools.Method(typeof(MapUIManager),"Travel");var departurePrefix=AccessTools.Method(typeof(ReservoirDeparturePatch),"Prefix");
            if(!(Harmony.GetPatchInfo(travel)?.Prefixes.Any(patch=>patch.PatchMethod==departurePrefix)??false))harmony.Patch(travel,new HarmonyMethod(departurePrefix));
            map.Travel(location);Check(messages==2&&!map.IsInTransit,"actual patched native Travel rejects an empty backpack before entering its transition");
            Check(ReservoirDeparturePatch.Prefix(map,new DelegateAfterhourLocation{id="other_location"})&&messages==2,"bait gate does not affect other destinations");
            ordinary.unitCount=1;Check(location.IsAvailable(),"restoring a single ordinary bait immediately satisfies departure availability");ordinary.unitCount=0;premium.unitCount=1;
            Check(location.IsAvailable(),"a single premium bait also satisfies departure availability");
            GeneralHelper.SetItemOwned(backpack,false);Check(!location.IsAvailable(),"an unowned selected backpack is not eligible for the owned-only native travel slot");GeneralHelper.SetItemOwned(backpack,true);
            backpack.DisableTag("BACKPACK_TAG");Check(!location.IsAvailable(),"a selected container that native backpack transfer rejects cannot bypass the bait gate");backpack.EnableTag("BACKPACK_TAG");
            var outsideBag=Bag(new Item("pelican_bait",2));entry.afterhourPocketSlotInvBackpack=SlotOf(outsideBag);entry.selectedBackpack=backpack;Outside(new DelegateAfterhourLocation{id="other_location"});
            Check(ReservoirAccess.CountForDeparture(entry,true)==2&&ReservoirAccess.HasBaitForDeparture(),"while already outside the equipped afterhour backpack is the authoritative root");
            ((Item)((GameGridInventory)outsideBag.Edges[0]).childItems[0]).unitCount=0;
            Check(!ReservoirAccess.HasBaitForDeparture(),"a stale bait-filled store selection does not override an empty actual backpack while outside");
            Outside(null);closed=true;Check(!location.IsAvailable(),"native event closure remains authoritative even with bait");closed=false;
            aquaculture=false;Check(!location.IsDiscovered()&&!location.IsAvailable(),"base aquaculture discovery gate is retained");aquaculture=true;
            Check(location.MaxConcurrentEvents==1&&location.Source==ModEntry.ModId&&location.Id==Fishing.LocationId,"custom location retains native saved-event support and one concurrent event");
            var a=new DelegateAfterhourLocation{id="dumping_ground"};var doctor=new DelegateAfterhourLocation{id=AfterhourLocation.DR_JACKSON};var upper=new DelegateAfterhourLocation{id="upper_district"};var lower=new DelegateAfterhourLocation{id="lower_district"};
            var list=new List<AfterhourLocation>{a,doctor,upper,lower,location};ReservoirLocation.PlaceAfterDoctor(list);
            Check(list.SequenceEqual(new AfterhourLocation[]{a,doctor,location,upper,lower}),"reservoir moves immediately after the native doctor and before upper district without reordering other destinations");
            ReservoirLocation.PlaceAfterDoctor(list);Check(list.Count==5&&list[2]==location,"repeat ordering is idempotent and creates no duplicate destination");
            var missing=new List<AfterhourLocation>{upper,location,lower};ReservoirLocation.PlaceAfterDoctor(missing);Check(missing.SequenceEqual(new AfterhourLocation[]{upper,lower,location}),"missing doctor fallback preserves every other destination");
            var registry=AfterhourLocation.GetAll();var original=registry.ToArray();
            try
            {
                ReservoirLocation.Register();var registered=AfterhourLocation.Get(Fishing.LocationId);
                Check(registered is ReservoirLocation&&registered.MaxConcurrentEvents==1&&registered.Source==ModEntry.ModId,"actual native registration installs the event-capable subclass under the existing ID/source");
                Check(registry.FindIndex(item=>item.Id==Fishing.LocationId)==registry.FindIndex(item=>item.Id==AfterhourLocation.DR_JACKSON)+1,"actual native registry places reservoir directly after Dr. Jackson");
                ReservoirLocation.Register();Check(registry.Count(item=>item.Id==Fishing.LocationId)==1,"registering again replaces the same destination instead of adding a duplicate");
            }
            finally{registry.Clear();registry.AddRange(original);}
        }
        finally{harmony.UnpatchAll(harmony.Id);ModEntry.Enabled=enabled;entry=null;map=null;aquaculture=true;closed=false;}
        return "Passed "+checks+" reservoir ordering, selected-versus-equipped backpack, owned nesting, no-bait availability and departure-boundary checks.";
    }
}
