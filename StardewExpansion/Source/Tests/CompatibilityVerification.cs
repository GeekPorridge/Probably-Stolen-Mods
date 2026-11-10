using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using PelicanCultivation;

// Use native inventory search, ownership, transfers and harvest methods. Only
// Unity rendering, singleton access, text and the clock are fixture boundaries.
public static class CompatibilityVerification
{
    private static int checks,day=1,initializations;
    private sealed class Item:GameItem
    {
        public Item(string id):base(id){modifiedState=state;}
        public override void Validate(){modifiedState=state;}
        public override void Destroy(){parentInventory?.Expel(this);unitCount=0;}
        public void Bind(PixelWindow window){contentWindow=window;}
    }
    private sealed class Grid:GameGridInventory
    {public Grid():base(1,1){} public override void Validate(){}}
    private sealed class Slot:GameSlotInventory {public override void Validate(){}}
    private static void Check(bool value,string why){if(!value)throw new Exception("COMPATIBILITY FAIL: "+why);checks++;}
    private static void Patch(Harmony h,MethodBase method,string prefix){h.Patch(method,new HarmonyMethod(typeof(CompatibilityVerification).GetMethod(prefix)));}
    public static bool Empty(string identifier,ref GameItem __result){__result=new Item(identifier);return false;}
    public static bool Factory(string identifier,ref GameItem __result){__result=new Item(identifier);return false;}
    public static bool Sprite(GameItem __instance,ref GameItem __result){__result=__instance;return false;}
    public static bool CropSprite(GameItem item,string key,int width,int height){Crops.BindSprite(item,key,width,height);return false;}
    public static bool Rooted(ref bool __result){__result=false;return false;}
    public static bool Ready(ref bool __result){__result=true;return false;}
    public static bool Day(ref int __result){__result=day;return false;}
    public static bool Text(string key,object[] args,ref string __result){__result=key;return false;}
    public static bool Initialize(){initializations++;ModEntry.GameplayReady=true;return false;}
    public static bool NoOp(){return false;}
    private static Grid Inventory(int width,int height,GameItem parent=null)
    {
        var grid=(Grid)FormatterServices.GetUninitializedObject(typeof(Grid));
        AccessTools.Field(typeof(GameGridInventory),"items").SetValue(grid,new List<GameItem>());
        AccessTools.Field(typeof(GameGridInventory),"<inventoryShape>k__BackingField").SetValue(grid,new GridShapeBuilder().SetData(new byte[width*height],width).Build());
        AccessTools.Field(typeof(TagSystem),"<dict>k__BackingField").SetValue(grid,new Dictionary<string,TagState>());
        AccessTools.Field(typeof(GameInventory),"<parent>k__BackingField").SetValue(grid,parent);
        return grid;
    }
    private static Item Owned(string id){var item=new Item(id);GeneralHelper.SetItemOwned(item,true);return item;}
    public static string Run()
    {
        checks=0;bool enabled=ModEntry.Enabled;ModEntry.Enabled=true;FarmSpecies.Register();ExpansionStock.Register();
        var h=new Harmony("pelican_cultivation.compatibility_verification");
        var overlayField=AccessTools.Field(typeof(OverlayHandler),"current");object oldOverlay=overlayField.GetValue(null);overlayField.SetValue(null,FormatterServices.GetUninitializedObject(typeof(OverlayHandler)));
        object oldSettings=AccessTools.Field(typeof(UISettings),"current").GetValue(null);
        var settings=FormatterServices.GetUninitializedObject(typeof(UISettings));AccessTools.Field(typeof(UISettings),"gridSize").SetValue(settings,16);AccessTools.Field(typeof(UISettings),"current").SetValue(null,settings);
        try
        {
            Patch(h,AccessTools.Method(typeof(ItemDirectory),"CreateEmptyItem",new[]{typeof(string)}),"Empty");
            Patch(h,AccessTools.Method(typeof(DirectoryMaster),"Item",new[]{typeof(string),typeof(bool)}),"Factory");
            Patch(h,AccessTools.Method(typeof(GameItem),"SetSpriteAndShape",new[]{typeof(string),typeof(string)}),"Sprite");
            Patch(h,AccessTools.Method(typeof(Crops),"Sprite"),"CropSprite");
            Patch(h,AccessTools.Method(typeof(GameItem),"IsRootedInBackInv"),"Rooted");
            Patch(h,AccessTools.Method(typeof(OverlayHandler),"UpdateInventoryStateUI"),"NoOp");
            Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Ready"),"Ready");Patch(h,AccessTools.PropertyGetter(typeof(Progress),"Day"),"Day");
            Patch(h,AccessTools.Method(typeof(ModEntry),"L"),"Text");
            Storage();SavePaths();Weights();Harvest();Feed();Names();Bootstrap(h);
            return "Passed "+checks+" compatibility regressions: native wrapped-tray placement/removal, repeat attachment, saved cage path repair, live-weight recovery, movable fertile eggs, harvest ownership, balanced night fish, feed migration, drink names and early-run initialization.";
        }
        finally{h.UnpatchAll(h.Id);AccessTools.Field(typeof(UISettings),"current").SetValue(null,oldSettings);overlayField.SetValue(null,oldOverlay);ModEntry.Enabled=enabled;}
    }
    private static void Storage()
    {
        var cage=Owned("animal_cage");cage.EnableTag("CAGE_TAG");var tray=Inventory(6,2,cage);var stock=Inventory(10,8,cage);
        var panel=WrappedPanel("pelican_products",tray);
        var row=(MachineryPanelRow)FormatterServices.GetUninitializedObject(typeof(MachineryPanelRow));AccessTools.Field(typeof(MachineryPanelRow),"panels").SetValue(row,new List<MachineryPanel>{panel});
        var window=(PixelWindow)FormatterServices.GetUninitializedObject(typeof(PixelWindow));AccessTools.Field(typeof(PixelWindow),"<childElement>k__BackingField").SetValue(window,row);cage.Bind(window);
        var factoryCage=new Item("animal_cage");tray.mayInventoryAddItemFunc=(food,inventory)=>GeneralHelper.IsItemOwned(factoryCage);
        Check(!GraphUtils.CanAccept(tray,Owned("pelican_egg")),"stale factory-owned tray callback reproduces false-full rejection");
        FarmHusbandry.AttachCage(cage);
        Check(!(panel.content is GameGridInventory)&&ReferenceEquals(FarmHusbandry.Products(cage),tray),"actual native MachineryIntake traversal resolves the production tray");
        for(int load=0;load<5;load++)FarmHusbandry.AttachCage(cage);
        Check(row.Panels.Count==1&&row.children.Count==1,"repeated load/morning attachment never appends duplicate wrapped trays");
        var productBag=Inventory(10,8);
        foreach(string id in new[]{"pelican_milk","pelican_truffle_small","pelican_truffle","pelican_egg","pelican_goat_milk"})
        {
            var product=Owned(id);
            Check(GraphUtils.CanAccept(tray,product),"native empty product tray accepts "+id);
            Check(GraphUtils.TryAcceptAll(tray,product)==0 && product.parentInventory==tray,"native product transfer commits "+id);
            Check(product.MayRemove(),"product can be taken back out: "+id);
            Check(GraphUtils.TryAcceptAll(productBag,product)==0&&product.parentInventory==productBag,"native transfer removes the same owned product from the wrapped tray: "+id);
        }
        var foreign=new Item("pelican_milk");Check(!GraphUtils.CanAccept(tray,foreign),"product bay rejects vendor-owned items");
        var egg=ExpansionStock.CreateEgg();GeneralHelper.SetItemOwned(egg,true);
        Check(GraphUtils.TryAcceptAll(stock,egg)==0 && egg.MayRemove(),"fertile egg is movable after actual native insertion");
        var bag=Inventory(10,8);Check(GraphUtils.TryAcceptAll(bag,egg)==0 && egg.parentInventory==bag,"fertile egg can leave cage with its original identity");
        Check(GraphUtils.TryAcceptAll(stock,egg)==0 && egg.MayRemove(),"fertile egg can return to the cage and move again");
        var hen=ExpansionStock.CreateAnimal(true);GeneralHelper.SetItemOwned(hen,true);FarmSpecies.SetString(hen,"ANIMAL_SEXE_TAG","FEMALE");
        Crops.SetInt(hen,"ANIMAL_AGE_TAG",4);FarmSpecies.SetFloat(hen,"ANIMAL_HEALTH_TAG",100);GeneticHelper.SetTrait(hen,ExpansionStock.Chickens,"ANIMAL_MAX_HEALTH_TAG",1);
        Check(GraphUtils.TryAcceptAll(stock,hen)==0,"native cage accepts laying hen");
        for(day=1;day<=4;day++)
        {
            FarmSpecies.SetFloat(hen,"ANIMAL_HUNGER_TAG",800);ExpansionStock.Produce(hen,stock);ExpansionStock.Produce(hen,stock);
            Check(tray.childItems.Count==1 && tray.childItems[0].identifier=="pelican_egg","hen lays exactly one egg on each of the first four nights: "+day);
            Check(hen.GetTagReadonly("ANIMAL_HUNGER_TAG").GetFloat()==650,"hen pays one real production cost: "+day);tray.Expel(tray.childItems[0]);
        }
    }
    private static MachineryIntake Intake(PixelElement content)
    {
        var intake=(MachineryIntake)FormatterServices.GetUninitializedObject(typeof(MachineryIntake));
        AccessTools.Field(typeof(MachineryIntake),"<content>k__BackingField").SetValue(intake,content);return intake;
    }
    private static MachineryPanel WrappedPanel(string role,PixelElement content)
    {
        var panel=(MachineryPanel)FormatterServices.GetUninitializedObject(typeof(MachineryPanel));panel.role=role;
        AccessTools.Field(typeof(MachineryPanel),"<content>k__BackingField").SetValue(panel,Intake(content));return panel;
    }
    private static GridPixelElement SlotPanel(GameSlotInventory slot)
    {
        var grid=(GridPixelElement)FormatterServices.GetUninitializedObject(typeof(GridPixelElement));
        AccessTools.Field(typeof(GridPixelElement),"childElements").SetValue(grid,new PixelElement[]{Intake(slot)});return grid;
    }
    private static void SavePaths()
    {
        var cage=Owned("animal_cage");cage.uniqueId=987;var stock=Inventory(10,8,cage);var tray=Inventory(6,2,cage);
        var dispenser=(Slot)FormatterServices.GetUninitializedObject(typeof(Slot));var butcher=(Slot)FormatterServices.GetUninitializedObject(typeof(Slot));
        var row=(MachineryPanelRow)FormatterServices.GetUninitializedObject(typeof(MachineryPanelRow));
        var panels=new List<MachineryPanel>{WrappedPanel("cage",stock),WrappedPanel("dispenser",SlotPanel(dispenser)),WrappedPanel("butcher",SlotPanel(butcher)),WrappedPanel("pelican_products",tray)};
        AccessTools.Field(typeof(MachineryPanelRow),"panels").SetValue(row,panels);
        var window=(PixelWindow)FormatterServices.GetUninitializedObject(typeof(PixelWindow));AccessTools.Field(typeof(PixelWindow),"<childElement>k__BackingField").SetValue(window,row);cage.Bind(window);
        Check(ReferenceEquals(AnimalCage.GetDispenserSlot(cage),dispenser)&&ReferenceEquals(AnimalCage.GetButcherSlot(cage),butcher),"migration uses real native nested dispenser/butcher slots");
        var numbers=CageSaveMigration.InventoryNumbers(cage);
        var saved=new SaveItemNode{identifier="animal_cage",uniqueId=cage.uniqueId,uuid=1,childItems=new List<long>{2,3,4,5},childItemInventoryNode=new List<int>{numbers[dispenser]+2,numbers[butcher]+2,numbers[tray]+1,numbers[stock]}};
        var records=new List<SaveItemNode>{saved,new SaveItemNode{identifier="livestock_feed_dispenser",uuid=2},new SaveItemNode{identifier="auto_butcher",uuid=3},new SaveItemNode{identifier="pelican_egg",uuid=4},new SaveItemNode{identifier="pelican_chicken",uuid=5}};
        var scope=CageSaveMigration.Enter(records);
        try
        {
            CageSaveMigration.Normalize(cage);
            Check(saved.childItemInventoryNode.SequenceEqual(new[]{numbers[dispenser],numbers[butcher],numbers[tray],numbers[stock]}),"duplicate-panel saves restore machine slots and products while preserving animal placement");
            var once=saved.childItemInventoryNode.ToArray();CageSaveMigration.Normalize(cage);Check(once.SequenceEqual(saved.childItemInventoryNode),"cage path migration is idempotent");
            var nested=CageSaveMigration.Enter(new List<SaveItemNode>());CageSaveMigration.Exit(nested);saved.childItemInventoryNode[0]+=2;CageSaveMigration.Normalize(cage);Check(saved.childItemInventoryNode[0]==numbers[dispenser],"nested decode restores its outer migration scope");
        }
        finally{CageSaveMigration.Exit(scope);}
        saved.childItemInventoryNode[0]+=2;CageSaveMigration.Normalize(cage);Check(saved.childItemInventoryNode[0]==numbers[dispenser]+2,"migration cannot touch unrelated non-decoding runtime objects");
    }
    private static void Weights()
    {
        foreach(string species in new[]{FarmRules.Cow,FarmRules.Pig,ExpansionStock.Chicken,ExpansionStock.Goat})
        {
            var animal=Owned("animal");FarmSpecies.SetString(animal,"ANIMAL_SPECIES_TAG",species);Crops.SetInt(animal,"ANIMAL_AGE_TAG",9);
            animal.EnableTag("ANIMAL_DISEASED_TAG");FarmSpecies.SetFloat(animal,"ANIMAL_HEALTH_TAG",31);FarmSpecies.SetFloat(animal,"ANIMAL_WEIGHT_TAG",800);
            AnimalWeightGuard.Repair(animal);FarmSpecies.SetFloat(animal,"ANIMAL_WEIGHT_TAG",0);AnimalWeightGuard.Repair(animal);
            Check(animal.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()==800,"zero weight restores the last valid weight: "+species);
            FarmSpecies.SetFloat(animal,"ANIMAL_WEIGHT_TAG",750);AnimalWeightGuard.Repair(animal);Check(animal.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()==750,"positive illness/starvation weight loss is preserved: "+species);
            FarmSpecies.SetFloat(animal,"ANIMAL_WEIGHT_TAG",float.NaN);AnimalWeightGuard.Repair(animal,700);Check(animal.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()==700,"disease boundary snapshot repairs invalid weight: "+species);
            animal.DisableTag(AnimalWeightGuard.LastWeight);FarmSpecies.SetFloat(animal,AnimalWeightGuard.LastWeight,0);FarmSpecies.SetFloat(animal,"ANIMAL_WEIGHT_TAG",0);AnimalWeightGuard.Repair(animal);
            Check(animal.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()>0&&animal.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()<700,"old zero record gets only the native minimum floor: "+species);
            Check(animal.IsTag("ANIMAL_DISEASED_TAG")&&animal.GetTagReadonly("ANIMAL_HEALTH_TAG").GetFloat()==31,"weight repair preserves actual health and disease: "+species);
            animal.EnableTag("ANIMAL_IS_DEAD_TAG");FarmSpecies.SetFloat(animal,"ANIMAL_WEIGHT_TAG",0);AnimalWeightGuard.Repair(animal);Check(animal.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()==0,"repair never invents carcass weight: "+species);
        }
        var native=Owned("rat");FarmSpecies.SetString(native,"ANIMAL_SPECIES_TAG",AnimalSpecies.Rat.speciesID);AnimalWeightGuard.Repair(native);Check(native.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat()==0,"native species outside this mod retain their own weight rules");
    }
    private static void Harvest()
    {
        foreach(string species in new[]{FarmRules.Cow,FarmRules.Pig,ExpansionStock.Chicken,ExpansionStock.Goat})
        {
            var animal=Owned("animal");FarmSpecies.SetString(animal,"ANIMAL_SPECIES_TAG",species);FarmSpecies.SetFloat(animal,"ANIMAL_WEIGHT_TAG",1500);
            var cuts=HusbandryHelper.CreateButcherCuts(animal);
            Check(cuts.Count>0 && cuts.All(GeneralHelper.IsItemOwned),"native butcher output inherits ownership: "+species);
            GeneralHelper.SetItemOwned(animal,false);Check(HusbandryHelper.CreateButcherCuts(animal).All(i=>!GeneralHelper.IsItemOwned(i)),"vendor livestock does not create free owned meat: "+species);
        }
        foreach(var species in new[]{FarmSpecies.SeaCucumbers,FarmSpecies.Lobsters}.Concat(ExpansionStock.Fish.Values))
        {
            var fish=Owned(species.fishIdentifier);fish.EnableTag("FISH_ITEM_TAG");FarmSpecies.SetString(fish,"FISH_SPECIES_TAG",species.speciesID);
            Crops.SetInt(fish,"FISH_AGE_TAG",species.stageStartDays[4]);Crops.SetInt(fish,FarmRules.FedNights,8);AquacultureHelper.SeedKcalEatenForAge(fish,species);
            var cuts=AquacultureHelper.CreateFilletPieces(fish);
            Check(cuts.Count>0 && cuts.All(GeneralHelper.IsItemOwned),"native fish fillets inherit ownership: "+species.speciesID);
            int index=Array.FindIndex(ExpansionStock.FishKeys,key=>ExpansionStock.Fish[key]==species);
            if(index>=0)
            {
                int kcal=AquacultureHelper.GetMeatKcal(fish);Check(kcal<=NightFishBalance.AdultCalories[index] && kcal>0,"adult night fish has bounded meat calories: "+species.speciesID+"="+kcal);
                FarmSpecies.SetFloat(fish,"FISH_KCAL_EATEN_TAG",1000000);Check(AquacultureHelper.GetMeatKcal(fish)==kcal,"maintenance feeding cannot grow unlimited meat");
                Crops.SetInt(fish,"FISH_CARCASS_KCAL_TAG",100000);fish.EnableTag(AquacultureHelper.FISH_IS_DEAD_TAG);
                NightFishCarcassPatch.Prefix(fish);Check(fish.GetTagReadonly("FISH_CARCASS_KCAL_TAG").GetInt()<=kcal,"old carcass yield is bounded too");
            }
        }
    }
    private static void Feed()
    {
        var feed=Owned("livestock_feed_dispenser");feed.EnableTag("FEED_DISPENSER_TAG");Crops.SetInt(feed,"STORED_OTHER",33714);Crops.SetInt(feed,"STORED_KCAL_TAG",33714);
        PlantDiet.RestoreLegacyFeed(feed);PlantDiet.RestoreLegacyFeed(feed);
        Check(PlantDiet.Available(feed)==33714 && feed.GetTagReadonly("STORED_KCAL_TAG").GetInt()==33714,"legacy non-meat supply remains usable without creating calories");
        Check(FarmFeedForecast.Supply(new[]{feed}).Plant==33714,"report uses the same restored feed as herbivores");
        PlantDiet.Draw(feed,100);PlantDiet.RestoreLegacyFeed(feed);Check(PlantDiet.Available(feed)==33614,"legacy restoration cannot refill spent feed");
        var explicitOther=Owned("livestock_feed_dispenser");explicitOther.EnableTag("FEED_DISPENSER_TAG");Crops.SetInt(explicitOther,"STORED_OTHER",500);Crops.SetInt(explicitOther,PlantDiet.PlantKcal,0);
        PlantDiet.RestoreLegacyFeed(explicitOther);Check(PlantDiet.Available(explicitOther)==0,"known non-plant food is not relabelled as plant feed");
    }
    private static void Names()
    {
        foreach(int kind in Enumerable.Range(1,Rules.WineKeys.Length-1))
        {
            var bottle=Owned("wine_bottle");bottle.EnableTag(WineHelper.WINE_CONTAINER_TAG);Crops.SetInt(bottle,WineHelper.WINE_CONTAINER_STATE,2);Crops.SetInt(bottle,Rules.WineTag,kind);bottle.name="Bloomberry wine";
            Check(bottle.GetDisplayName()=="wine_"+Rules.WineKeys[kind],"native display lookup repairs stale custom brew name "+kind);
            GeneralHelper.SetCustomName(bottle,"My bottle");Check(bottle.GetDisplayName(true)=="My bottle","explicit personal name remains preferred");
        }
    }
    private static void Bootstrap(Harmony h)
    {
        h.Unpatch(AccessTools.PropertyGetter(typeof(Progress),"Ready"),HarmonyPatchType.Prefix,h.Id);
        var storeField=AccessTools.Field(typeof(PlayerStore),"instance");object oldStore=storeField.GetValue(null);
        var menuField=AccessTools.Field(typeof(NewGameData),"<Instance>k__BackingField");object oldMenu=menuField.GetValue(null);bool oldReady=ModEntry.GameplayReady;
        var menu=(NewGameData)FormatterServices.GetUninitializedObject(typeof(NewGameData));menuField.SetValue(null,menu);
        var entryField=AccessTools.Field(typeof(EmporiumEntry),"<Instance>k__BackingField");object oldEntry=entryField.GetValue(null);
        var entry=(EmporiumEntry)FormatterServices.GetUninitializedObject(typeof(EmporiumEntry));var pointer=AccessTools.Field(typeof(UnityEngine.Object),"m_CachedPtr");pointer.SetValue(entry,new IntPtr(1));pointer.SetValue(menu,new IntPtr(1));
        Patch(h,AccessTools.Method(typeof(Progress),"OnLoaded"),"Initialize");
        try
        {
            initializations=0;storeField.SetValue(null,null);menu.isInMainMenu=true;ModEntry.GameplayReady=false;Progress.EnsureReady();Check(initializations==0 && !PlayerStore.HasInstance,"menu cannot construct a player store");
            storeField.SetValue(null,FormatterServices.GetUninitializedObject(typeof(PlayerStore)));entryField.SetValue(null,entry);
            Progress.EnsureReady();Check(initializations==0&&!Progress.Ready,"an early or menu store without an inventory graph cannot initialize gameplay");
            AccessTools.Field(typeof(EmporiumEntry),"<invElement>k__BackingField").SetValue(entry,Inventory(2,2));
            Progress.EnsureReady();Check(initializations==1&&Progress.Ready&&menu.isInMainMenu,"a fully loaded store is usable even when NewGameData retains its menu flag");
            ModEntry.Enabled=false;Check(!Progress.Ready,"disabling the mod immediately removes gameplay readiness");ModEntry.Enabled=true;
            initializations=0;menu.isInMainMenu=false;
            for(int i=1;i<=3;i++){day=i;ModEntry.GameplayReady=false;Progress.EnsureReady();Check(ModEntry.GameplayReady && initializations==i,"first three nights initialize without waiting for a morning report: "+i);Progress.EnsureReady();Check(initializations==i,"initialization is not repeated during an active run");}
        }
        finally{pointer.SetValue(entry,IntPtr.Zero);pointer.SetValue(menu,IntPtr.Zero);storeField.SetValue(null,oldStore);entryField.SetValue(null,oldEntry);menuField.SetValue(null,oldMenu);ModEntry.GameplayReady=oldReady;}
    }
}
