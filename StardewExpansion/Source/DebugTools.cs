using System;

namespace PelicanCultivation
{
    public static class DebugTools
    {
        public static void Register()
        {
            ModHelper.RemoveDebugFunctions(ModEntry.ModId);
            ModHelper.AddDebugFunction(ModEntry.ModId,ModHelper.DEBUG_CATEGORY_MODDING,ModEntry.L("debug_farmer"),QueueFarmer,true);
            ModHelper.AddDebugFunction(ModEntry.ModId,ModHelper.DEBUG_CATEGORY_MODDING,ModEntry.L("debug_report"),Report,true);
        }
        public static void QueueFarmer()
        {
            if(!Progress.Ready) return;
            var store=PlayerStore.Instance;
            if(Merchant.IsPresent || store.storeClientManager.clientStack.Exists(c=>c.identifier==Merchant.Id)) return;
            store.storeClientManager.AddNextClient(Merchant.Create());
            ModEntry.Note("Debug farmer queued as next visitor. The current visitor is preserved.");
        }
        public static void Report()
        {
            var mod=ModLoader.Mods.Find(m=>m.Manifest.ID==ModEntry.ModId);
            ModEntry.Note("Diagnostics: sprites="+(mod?.Sprites.Count??0)+"; locales="+(mod?.Localization.Count??0)+"; farmerFactory="+StoreClientListDict.storeClientDict.ContainsKey(Merchant.Id)+"; ready="+Progress.Ready);
            if(!Progress.Ready) return;
            var manager=PlayerStore.Instance.storeClientManager;
            ModEntry.Note("Day="+Progress.Day+"; visits="+Progress.Get("farmer_visits")+"; ancientObtained="+Progress.EverObtained+"; farmerPresent="+Merchant.IsPresent+"; queued="+manager.clientStack.Exists(c=>c.identifier==Merchant.Id));
        }
    }
}
