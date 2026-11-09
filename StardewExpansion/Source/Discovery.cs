namespace PelicanCultivation
{
    public static class Discovery
    {
        private const string SearchStartedChannel="station_scavenging:lower_search_started";
        private const string SearchFinishedChannel="station_scavenging:lower_search_finished";
        private const string SearchAbortedChannel="station_scavenging:lower_search_aborted";
        private static object activeLowerState;
        private static bool foundSeed;
        private static bool listening;
        public static bool StationLoaded => ModHelper.IsModRunning("station_scavenging");

        public static void Enable()
        {
            if(listening) return;
            ModHelper.OnMessage(ModEntry.ModId,SearchStartedChannel,BeforeLowerSearch);
            ModHelper.OnMessage(ModEntry.ModId,SearchFinishedChannel,AfterLowerSearch);
            ModHelper.OnMessage(ModEntry.ModId,SearchAbortedChannel,AbortLowerSearch);
            listening=true;
        }

        public static void Disable()
        {
            if(listening)
            {
                ModHelper.OffMessage(ModEntry.ModId,SearchStartedChannel,BeforeLowerSearch);
                ModHelper.OffMessage(ModEntry.ModId,SearchFinishedChannel,AfterLowerSearch);
                ModHelper.OffMessage(ModEntry.ModId,SearchAbortedChannel,AbortLowerSearch);
            }
            listening=false;
            activeLowerState=null;
            foundSeed=false;
        }

        public static void BeforeLowerSearch(string sender,object state)
        {
            if(sender!="station_scavenging") return;
            activeLowerState=state;
            foundSeed=false;
        }

        public static void AfterLowerSearch(string sender,object state)
        {
            if(sender!="station_scavenging") return;
            try
            {
                if(ModEntry.Enabled && foundSeed && activeLowerState!=null)
                    ModHelper.SendMessage(ModEntry.ModId,"record_precious",activeLowerState);
            }
            finally
            {
                activeLowerState=null;
                foundSeed=false;
            }
        }

        public static void AbortLowerSearch(string sender,object state)
        {
            if(sender!="station_scavenging") return;
            activeLowerState=null;
            foundSeed=false;
        }

        public static void MaybeReplace(ref GameItem item)
        {
            if(item==null || !Rules.EligibleForDiscovery(Progress.Day)) return;
            bool station=StationLoaded;
            if(station)
            {
                if(activeLowerState==null || item.identifier!="loot_crate_service") return;
                // Select within an existing core-valuable outcome, preserving the
                // total valuable chance and the original common-item pool.
                if(!RNG.Roll(ModEntry.StationSeedShare)) return;
            }
            else if(!RNG.Roll(ModEntry.NativeSeedChance)) return;
            var replacement=DirectoryMaster.Item(Rules.Crops[Rules.AncientIndex].SeedId);
            if(item.parentInventory!=null) { replacement.Destroy(); return; }
            item.Destroy(); item=replacement;
            if(station) foundSeed=true;
        }
    }
}
