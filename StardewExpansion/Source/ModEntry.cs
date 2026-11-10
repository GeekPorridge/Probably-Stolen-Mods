using System;
using UnityEngine;

namespace PelicanCultivation
{
    public sealed class ModEntry : IMod
    {
        public const string ModId="pelican_cultivation", Version="2.0.4";
        public const string BuildRevision="r13";
        public static bool Enabled, GameplayReady;
        private static ModLog log;
        private static ConfigEntry<int> nativeSeedChance,stationSeedShare;
        private static ConfigEntry<bool> autoReplant;
        private static RuntimePump pump;
        public static int NativeSeedChance => nativeSeedChance==null?1:nativeSeedChance.Value;
        public static int StationSeedShare => stationSeedShare==null?20:stationSeedShare.Value;
        public static bool AutoReplantEnabled => autoReplant==null || autoReplant.Value;
        public void Init(ModManifest manifest)
        {
            log=new ModLog(manifest);
            var config=ModHelper.GetConfig(ModId);
            nativeSeedChance=config.Define("ancient_native_chance_percent",1,"Ancient seed / 上古种子",()=>L("config_native_chance"),min:0,max:10,tooltip:()=>L("config_native_hint"));
            stationSeedShare=config.Define("ancient_station_core_share_percent",20,"Ancient seed / 上古种子",()=>L("config_station_share"),min:0,max:100,tooltip:()=>L("config_station_hint"));
            autoReplant=config.Define("auto_replant",true,"Hydroponics / 水培",()=>L("config_auto_replant"),tooltip:()=>L("config_auto_replant_hint"));
            autoReplant.OnChanged+=enabled=>{ if(enabled) AutoPlant.RefreshExistingMachines(); };
            FarmSettings.Define(config);
            FishingDifficulty.Define(config);
        }
        public void OnEnable()
        {
            if(Enabled) return;
            Enabled=true;
            GameplayReady=false;
            Discovery.Enable();
            FarmSpecies.Register();
            Expansion.Enable();
            StoreClientListDict.storeClientDict[Merchant.Id]=Merchant.Create;
            ModHook.OnModItemDirectoryInit+=Crops.RegisterItems;
            ModHook.OnModItemDirectoryInit+=FarmSpecies.RegisterItems;
            ModHook.OnGoingSleepEarly+=FarmNightReport.BeginNight;
            ModHook.OnGoingSleepLate+=FarmNightReport.EndNight;
            ModHook.OnGenerateCustomerVeryLate+=Merchant.Queue;
            ModHook.OnCreateTooltipLate+=Brewing.Tooltip;
            ModHook.OnGameLoadedLate+=Progress.OnLoaded;
            ModHook.OnNewGameStarted+=NewGame;
            ModHook.OnWakingUpLate+=Progress.OnLoaded;
            ModHook.OnModAssetsLoaded+=Assets.Loaded;
            EnsurePump();
            Note("Stardew Expansion "+Version+" revision "+BuildRevision+" enabled; twelve crops, seven brews, artisan machines, husbandry, reservoir fishing and three paper manuals.");
        }
        public void OnDisable()
        {
            Enabled=false;
            GameplayReady=false;
            ModHook.OnModItemDirectoryInit-=Crops.RegisterItems;
            ModHook.OnModItemDirectoryInit-=FarmSpecies.RegisterItems;
            ModHook.OnGoingSleepEarly-=FarmNightReport.BeginNight;
            ModHook.OnGoingSleepLate-=FarmNightReport.EndNight;
            FarmNightReport.Reset();
            NightReportScroll.RestoreAll();
            ModHook.OnGenerateCustomerVeryLate-=Merchant.Queue;
            ModHook.OnCreateTooltipLate-=Brewing.Tooltip;
            ModHook.OnGameLoadedLate-=Progress.OnLoaded;
            ModHook.OnNewGameStarted-=NewGame;
            ModHook.OnWakingUpLate-=Progress.OnLoaded;
            ModHook.OnModAssetsLoaded-=Assets.Loaded;
            StoreClientListDict.storeClientDict.Remove(Merchant.Id);
            Discovery.Disable();
            Expansion.Disable();
            if(pump!=null) UnityEngine.Object.Destroy(pump);
            pump=null;
            ModHelper.RemoveDebugFunctions(ModId);
        }
        private static void NewGame(string scenario) => Progress.OnLoaded();
        public static string L(string key,params object[] args) => ModHelper.GetLocalized(ModId,key,args);
        public static void Note(string message) => log?.Log(message);
        public static void EnsurePump()
        { if(Enabled && pump==null && ModHook.Instance!=null) pump=ModHook.Instance.gameObject.AddComponent<RuntimePump>(); }
        public static void Error(string context,Exception e) => log?.Error(context+": "+e);
        public static void Message(string key)
        {
            if(GenericConfirmationUIManager.Instance==null) return;
            GenericConfirmationUIManager.Instance.OpenPanelSingleButton(L(key),LocHelper.GetLocalizedMainMenu("confirm"),()=>GenericConfirmationUIManager.Instance.ClosePanel());
        }
    }
    public sealed class RuntimePump : MonoBehaviour
    {
        private float nextTick;
        private bool observed,lastReady;
        private void Update()
        {
            // PlayerStore.Instance is a lazy factory, not a harmless null check.
            // Never touch it (or game time) until the game-loaded/new-game hook.
            if(!ModEntry.Enabled) return;
            bool ready=Progress.Ready;
            if(!observed || ready!=lastReady){observed=true;lastReady=ready;ModEntry.Note("Runtime readiness: "+Progress.RuntimeState);}
            Progress.EnsureReady();
            if(!ModEntry.GameplayReady) return;
            ReservoirEvents.EnsureToday();Fishing.Tick();
            if(!Progress.Ready) { ModEntry.GameplayReady=false; return; }
            if(Time.unscaledTime<nextTick) return;
            nextTick=Time.unscaledTime+0.75f;
            Merchant.Tick();
            BottleGeometry.RetryPending();
            CropGeometry.RetryPending();
            ProductGeometry.Retry();
        }
    }
    public static class Assets
    {
        private static Sprite fittedFarmer,farmerSource,farmerReference;
        public static Sprite Sprite(string key)
        {
            var mod=ModLoader.Mods.Find(m=>m.Manifest.ID==ModEntry.ModId);
            return mod!=null && mod.Sprites.TryGetValue(key,out var sprite)?sprite:null;
        }
        public static void Loaded(LoadedMod mod)
        {
            ModEntry.EnsurePump();
            if(mod.Manifest.ID==ModEntry.ModId)
            {
                RegisterReservoirSprites(mod);
                foreach(var key in new System.Collections.Generic.List<string>(mod.Sprites.Keys))
                    if(key!="farmer" && key!="farmer_ranch" && key!="farmer_artisan" && key!="reservoir_scene" && key!="reservoir_card" && key!=BottleGeometry.CompactSprite) InventorySprite(mod,key);
                RegisterFarmerPortrait();
                Crops.RegisterPlants();
                DebugTools.Register();
                ModEntry.Note("Resources ready: "+mod.Sprites.Count+" sprites; "+mod.Localization.Count+" locales. Farmer factory registered as "+Merchant.Id+".");
            }
        }
        public static void RegisterReservoirSprites(LoadedMod mod)
        {
            // CustomUIManager resolves mod Sprites/PaperSprites, never Textures.
            // Keep scenery out of inventory scaling and register UI aliases.
            foreach(string key in new[]{"reservoir_card","reservoir_scene"})
                if(mod.Textures.TryGetValue(key,out var texture)&&texture!=null)
                {
                    var sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(0.5f,0.5f),key=="reservoir_scene"?50:100);
                    sprite.name=key;mod.PaperSprites[key]=sprite;
                }
            ModEntry.Note("Reservoir card/background registered for native UI resolution.");
        }
        public static Sprite InventorySprite(LoadedMod mod,string key)
        {
            if(!mod.Sprites.TryGetValue(key,out var source) || source==null) return null;
            if(Math.Abs(source.pixelsPerUnit-Rules.InventoryPixelsPerUnit)<0.01f) return source;
            // UI Image.SetNativeSize divides the texture size by the sprite/canvas
            // PPU ratio. Preserve 128 texture pixels per 16-pixel inventory cell.
            var pivot=new Vector2(source.pivot.x/source.rect.width,source.pivot.y/source.rect.height);
            var sprite=UnityEngine.Sprite.Create(source.texture,source.rect,pivot,Rules.InventoryPixelsPerUnit);
            sprite.name=key;
            mod.Sprites[key]=sprite;
            return sprite;
        }
        public static Sprite RegisterFarmerPortrait()
        {
            var source=Sprite(Progress.Ready && Merchant.PortraitDay%7==5?"farmer_artisan":Progress.Ready && Merchant.PortraitDay%7==6?"farmer_ranch":"farmer")??Sprite("farmer");
            var dictionary=SpriteDict.Instance;
            if(source==null || dictionary?.spriteDictionary==null) return null;
            var reference=dictionary.GetSprite(SpriteDict.GetRetiredFarmerSpriteName());
            if(reference==null || reference.rect.width<=0 || reference.rect.height<=0) return null;
            if(fittedFarmer==null || farmerSource!=source || farmerReference!=reference)
            {
                float ppu=Rules.PortraitPixelsPerUnit(source.rect.width,source.rect.height,reference.rect.width,reference.rect.height,reference.pixelsPerUnit);
                var pivot=new Vector2(reference.pivot.x/reference.rect.width,reference.pivot.y/reference.rect.height+Rules.PortraitDownwardFraction);
                fittedFarmer=UnityEngine.Sprite.Create(source.texture,source.rect,pivot,ppu);
                fittedFarmer.name=Merchant.Id;
                farmerSource=source; farmerReference=reference;
                ModEntry.Note("Portrait fitted to native bounds: source="+source.rect.width+"x"+source.rect.height+"; native="+reference.rect.width+"x"+reference.rect.height+"; ppu="+ppu);
            }
            dictionary.spriteDictionary[Merchant.Id]=fittedFarmer;
            return fittedFarmer;
        }
    }
}
