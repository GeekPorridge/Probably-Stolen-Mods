using System;
using System.Collections.Generic;

namespace PelicanCultivation
{
    public static class Progress
    {
        // The menu flag belongs to NewGameData, not the loaded inventory. A
        // retained flag must not disable an already loaded store, and probing
        // the menu must never instantiate PlayerStore through its lazy getter.
        private static bool HasStore => PlayerStore.HasInstance && EmporiumEntry.Instance!=null && EmporiumEntry.Instance.invElement!=null;
        public static bool Ready => ModEntry.Enabled && ModEntry.GameplayReady && HasStore;
        public static int Day => Ready?StoreStation.Instance.dayCounter:0;
        public static string RuntimeState => "enabled="+ModEntry.Enabled+"; gameplay="+ModEntry.GameplayReady+"; store="+PlayerStore.HasInstance+"; emporium="+(EmporiumEntry.Instance!=null)+"; menu="+(NewGameData.Instance!=null && NewGameData.Instance.isInMainMenu)+"; ready="+Ready;
        public static int Get(string key,int fallback=0) => Ready?ModHelper.GetModDataInt(ModEntry.ModId,key,fallback):fallback;
        public static void Set(string key,int value) { if(Ready) ModHelper.SetModDataInt(ModEntry.ModId,key,value); }
        public static bool EverObtained => Get("ancient_obtained")!=0;
        public static void EnsureReady()
        {
            // Intro-skipping runs can precede both the initial-item and morning
            // UI hooks. HasInstance prevents constructing a save at the menu.
            if(ModEntry.Enabled && !ModEntry.GameplayReady && HasStore) OnLoaded();
        }
        public static void ObserveSeed(GameItem item)
        {
            if(item!=null && item.identifier==Rules.Crops[Rules.AncientIndex].SeedId && item.parentInventory!=null && GeneralHelper.IsItemOwned(item)) Set("ancient_obtained",1);
        }
        public static void OnLoaded()
        {
            if(!ModEntry.Enabled || !HasStore) return;
            ModEntry.GameplayReady=true;
            ModEntry.EnsurePump();
            if(!Ready) return;
            FarmSpecies.Register();
            ExpansionStock.Register();
            ReservoirLocation.EnsureRegistered();
            TownDialogue.Initialize();
            Assets.RegisterFarmerPortrait();
            var savedItems=new List<GameItem>(EmporiumEntry.Instance.GetAllItems());
            foreach(var item in savedItems){WineStorage.Expand(item);PlantDiet.RestoreLegacyFeed(item);}
            foreach(var item in savedItems)
            {
                Crops.RefreshLoaded(item);
                FarmSpecies.RefreshAnimal(item);
                FarmSpecies.RefreshFish(item);
                FarmPresentation.RefreshFood(item);
                Expansion.Refresh(item);
                if(item.IsTag("CAGE_TAG")) FarmHusbandry.AttachCage(item);
                if(WineHelper.IsFinishedWine(item)) Brewing.RefreshFinished(item,false);
                ObserveSeed(item);
            }
            Merchant.Restore();
            AutoPlant.RefreshExistingMachines();
            DebugTools.Register();
            ModEntry.Note("Game ready: farmer visits="+Get("farmer_visits")+"; day="+Day+"; farmer factory="+StoreClientListDict.storeClientDict.ContainsKey(Merchant.Id));
            ModEntry.Note("Loaded runtime: "+RuntimeState+"; aquaculture="+FarmStock.Aquaculture+"; reservoir="+(AfterhourLocation.Get(Fishing.LocationId)!=null));
        }
    }

    public static class Merchant
    {
        public const string Id="pelican_town_farmer";
        public static int PortraitDay => IsPresent?Progress.Get("stock_day",Progress.Day):Progress.Day;
        private static bool filling;
        public static bool IsPresent => Progress.Ready && PlayerStore.Instance.currentClientInstance?.GetClientBlueprint()?.identifier==Id;
        public static bool HasPendingStock
        {
            get { foreach(var entry in FarmStock.Entries) if(Progress.Get(entry.RemainingKey)>0) return true; return false; }
        }
        public static void Queue(StoreClientManager manager)
        {
            if(!Progress.Ready) return;
            ReconcileSchedule(manager,Create,Progress.Day,Progress.Get("farmer_last_visit_day",-1),IsPresent,FarmStock.Farming,FarmStock.Mammals,FarmStock.Aquaculture);
        }
        public static void ReconcileSchedule(StoreClientManager manager,Func<StoreClient> create,int operatingDay,int lastVisitDay=-1,bool isPresent=false,bool farming=true,bool mammals=true,bool aquaculture=true)
        {
            if(manager?.clientStack==null) return;
            bool eligible=FarmRules.VisitDay(operatingDay,farming,mammals,aquaculture) && lastVisitDay!=operatingDay && !isPresent;
            bool queued=false;
            for(int i=0;i<manager.clientStack.Count;i++)
            {
                if(manager.clientStack[i]?.identifier!=Id) continue;
                // Remove only our stale/duplicate queued visits. An ongoing
                // trade is separate from the queue and is never interrupted.
                if(!eligible || queued) manager.clientStack.RemoveAt(i--);
                else queued=true;
            }
            if(!eligible || queued) return;
            manager.clientStack.Add(create());
            ModEntry.Note("Fellow Townsman queued for Wednesday/Saturday; operating day="+operatingDay);
        }
        public static StoreClient Create()
        {
            var farmer=new StoreClient {identifier=Id,displayName=Progress.Day%7==5?Expansion.L("artisan_name"):ModEntry.L("farmer_name"),clientFaction="FACTION_LOWER_LEVEL",clientIntent=StoreClient.ClientIntent.SELL,nonShootable=true};
            StoreClientFactionSetup.InitLowerLevelCitizen(farmer);
            farmer.spriteName=SpriteDict.GetRetiredFarmerSpriteName();
            var portrait=Assets.RegisterFarmerPortrait();
            if(portrait!=null && SpriteDict.Instance?.spriteDictionary!=null)
            { SpriteDict.Instance.spriteDictionary[Id]=portrait; farmer.spriteName=Id; }
            SetDialogue(farmer);
            farmer.AddBasicDialog();
            farmer.CompleteClientCreation();
            // A full counter can leave all stock pending. Keep the visitor available
            // instead of playing the native "nothing left" exit dialogue.
            farmer.allDoneDialogue=null;
            farmer.OnDismiss+=()=>Progress.Set("stock_started",0);
            ModHelper.SetClientSource(farmer,ModEntry.ModId);
            return farmer;
        }
        private static void SetDialogue(StoreClient farmer)
        {
            int day=Progress.Get("stock_day",Progress.Day),code=TownDialogue.CurrentCode;
            farmer.mainDialogue.SetText(farmer.displayName,TownDialogue.Text(code,day)).SetEndAction(()=>{
                TownDialogue.Acknowledge(code,day);
                Progress.Set("stock_started",1);
                Fill();
            });
        }
        private static void Arrive(StoreClient farmer)
        {
            int today=Progress.Day;
            if(Rules.NewVisit(today,Progress.Get("farmer_last_visit_day",-1)))
            {
                Progress.Set("farmer_last_visit_day",today);
                if(today%7==5)Progress.Set("artisan_visits",Progress.Get("artisan_visits")+1);
                else Progress.Set("farmer_visits",Progress.Get("farmer_visits")+1);
                Progress.Set("stock_started",0);
                Progress.Set("stock_day",today);
                FarmStock.Capture();
            }
            TownDialogue.PrepareVisit();
            SetDialogue(farmer);
        }
        public static void OnVisit(StoreClient farmer)
        { if(farmer!=null && farmer.identifier==Id) Arrive(farmer); }
        public static void Restore()
        {
            if(!Progress.Ready) return;
            FarmStock.RefreshPending();
            Queue(PlayerStore.Instance.storeClientManager);
            if(!IsPresent) return;
            var farmer=PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
            // Arrival is idempotent by operating day. Loading a visit cannot
            // increment its count or regenerate its already-displayed stock.
            if(Progress.Get("stock_day",-1)<0) Arrive(farmer);
            TownDialogue.PrepareVisit();
            farmer.displayName=Merchant.PortraitDay%7==5?Expansion.L("artisan_name"):ModEntry.L("farmer_name");
            SetDialogue(farmer);
            var portrait=Assets.RegisterFarmerPortrait();
            if(portrait!=null && StoreClientMono.Instance!=null)
            { farmer.spriteName=Id; StoreClientMono.Instance.image.sprite=portrait; }
        }
        public static void Tick()
        {
            if(IsPresent && Progress.Get("stock_started")!=0) Fill();
        }
        public static void Fill()
        {
            if(filling || !IsPresent || EmporiumEntry.Instance==null || Progress.Get("stock_day",-1)<0) return;
            filling=true;
            try
            {
                FarmStock.RefreshPending();
                TryGift();
                var counter=EmporiumEntry.Instance.frontInvinvElement;
                foreach(var entry in FarmStock.Entries)
                {
                    int left=Progress.Get(entry.RemainingKey);
                    while(left>0 && counter.CanTakeAnything())
                    {
                        var seed=FarmStock.Spawn(entry);
                        int remaining=GraphUtils.TryAcceptAll(counter,seed);
                        if(remaining!=0 || seed.parentInventory==null)
                        { if(seed.parentInventory==null) seed.Destroy(); break; }
                        left--;
                        Progress.Set(entry.RemainingKey,left);
                    }
                }
            }
            catch(Exception e) { ModEntry.Error("Seed restocking",e); }
            finally { filling=false; }
        }
        private static void TryGift()
        {
            if(Merchant.PortraitDay%7==5||!FarmStock.Farming || !Rules.EligibleForGift(Progress.Get("farmer_visits"),Progress.EverObtained,Progress.Get("gift_delivered")!=0)) return;
            var seed=DirectoryMaster.Item(Rules.Crops[Rules.AncientIndex].SeedId,false);
            // Do not use EmporiumTryAdd: the native helper destroys an unplaced
            // item when every inventory is full. Leave the gift eligible instead.
            var destinations=new GraphNodeStorage[]{EmporiumEntry.Instance.frontInvinvElement,EmporiumEntry.Instance.invElement,EmporiumEntry.Instance.backInvinvElement};
            foreach(var destination in destinations)
            {
                if(!GraphUtils.CanAccept(destination,seed)) continue;
                if(GraphUtils.TryAcceptAll(destination,seed)==0 && seed.parentInventory!=null)
                {
                    GeneralHelper.SetItemOwned(seed,true);
                    Progress.Set("ancient_obtained",1);
                    Progress.Set("gift_delivered",1);
                    return;
                }
            }
            seed.Destroy();
        }
    }
}
