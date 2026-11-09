using System;
using System.Globalization;
using UnityEngine;

namespace StationScavenging
{
    internal sealed class LowerLevelsLocation : ScavengingLocation
    {
        private readonly AfterhourLocation original;
        internal LowerLevelsLocation(AfterhourLocation original) : base(Region.Lower, original) { this.original = original; }
        public override string Id { get { return original.Id; } }
        public override string ImageKey { get { return original.ImageKey; } }
        public override string BackgroundKey { get { return original.BackgroundKey; } }
        public override int BackgroundSortingOrder { get { return original.BackgroundSortingOrder; } }
        public override bool UsesAfterhourBag { get { return original.UsesAfterhourBag; } }
        public override bool ShowsStoreCounters { get { return original.ShowsStoreCounters; } }
        public override bool AllowsHazardousWaste { get { return original.AllowsHazardousWaste; } }
        public override bool CountsDumpedWaste { get { return original.CountsDumpedWaste; } }
        public override int MaxConcurrentEvents { get { return original.MaxConcurrentEvents; } }
        public override bool IsDiscovered() { return original.IsDiscovered(); }
        // Disposal and story visits remain possible with no searches left.
        public override bool IsAvailable() { return original.IsAvailable(); }
        public override string GetLeaveWarning() { return original.GetLeaveWarning(); }
        public override Vector2? GetFrontCounterPosition(int width, int height) { return original.GetFrontCounterPosition(width, height); }
        public override Vector2? GetBackCounterPosition(int width, int height) { return original.GetBackCounterPosition(width, height); }
        public override void Arrive()
        {
            EnterNative();
            base.Arrive();
        }
        internal void EnterNative()
        {
            StationGame.EnsureRun();
            StationGame.ResumeLowerVisit();
            original.Arrive();
            // Native Arrive resets its counter. Restore the shared ledger before
            // drawing odds or resolving any lower-area injury roll.
            StationGame.RestoreNativeAttempts();
        }
        public override void Leave() { try { original.Leave(); } finally { base.Leave(); } }
        public override void Tick() { original.Tick(); base.Tick(); }
    }

    internal class ScavengingLocation : AfterhourLocation
    {
        private readonly Region region;
        private readonly AfterhourLocation originalArt;
        private NativeScavengingPanel panel;
        private CustomUIWindow fallback;
        private OutingState state;
        private bool departureRequested;
        private float nextRefresh;
        private bool errorLogged;
        private string lastBlockReason;

        internal ScavengingLocation(Region region, AfterhourLocation originalArt) { this.region = region; this.originalArt = originalArt; }
        public override string Id { get { return StationRules.LocationId(region); } }
        public override string Source { get { return ModEntry.ModId; } }
        public override string ImageKey { get { return region == Region.Lower ? originalArt.ImageKey : StationArt.CoverKey(region); } }
        public override string AmbienceId { get { return originalArt.AmbienceId; } }
        public override GameObject StageObject { get { return region == Region.Lower ? originalArt.StageObject : StationArt.Stage(region); } }
        public override string BackgroundKey { get { return region == Region.Lower ? originalArt.BackgroundKey : StationArt.BackgroundKey(region); } }
        public override GameObject PanelObject { get { return panel == null ? null : panel.Root; } }
        public override string GetTitle() { return ModEntry.L("region_" + (int)region); }
        public override bool IsDiscovered() { return PlayerStore.HasInstance; }
        public override bool IsAvailable()
        {
            return PlayerStore.HasInstance && StationGame.IsUnlocked(region) && !StationGame.GetOuting(region).Ended
                && StationGame.Night.Remaining > 0 && base.IsAvailable();
        }
        public override string GetLeaveWarning() { return ModEntry.L("leave_warning"); }

        public override void Arrive()
        {
            state = StationGame.GetOuting(region);
            departureRequested = errorLogged = false;
            lastBlockReason = null;
            nextRefresh = 0;
            StationGame.Save(state);
            DisposePanel();
            var source = originalArt.PanelObject;
            if (source != null) panel = new NativeScavengingPanel(source, Search, Withdraw);
            Refresh();
            if (region != Region.Lower)
            {
                var current = StationEvents.Select(region, StationGame.Seed, state.Day);
                if (current != null && ModHelper.IsAfterhourEventActive(current.Id))
                    StoreUIManager.Instance.Notify(ModEntry.L("event_arrival", ModEntry.L("event_" + current.Key + "_title")));
            }
        }

        public override void Leave()
        {
            if (state != null) { state.End(Departure.Voluntary); StationGame.Save(state); }
            DisposePanel();
            fallback = null;
        }

        internal void DisposePanel()
        {
            if (panel != null) panel.Dispose();
            panel = null;
        }

        public override void BuildPanel(CustomUIBuilder builder)
        {
            fallback = builder.Handle;
            builder.AddLabel("", "attempts");
            builder.AddLabel("", "chance");
            builder.AddLabel("", "risk");
            builder.AddSpace(8f);
            builder.AddButton(LocHelper.GetLocalizedUI("afterhour_scavenge_action"), Search);
            Refresh();
        }

        private bool IsPaused()
        {
            var ui = StoreUIManager.Instance;
            return Time.timeScale <= 0 || (ui != null && ui.escapeUIManager != null && ui.escapeUIManager.menuPanel != null && ui.escapeUIManager.menuPanel.activeSelf)
                || (GameOptionUIManager.Instance != null && GameOptionUIManager.Instance.IsOpen);
        }

        private bool InTransition()
        {
            return StoreUIManager.Instance != null && StoreUIManager.Instance.blackscreen != null && StoreUIManager.Instance.blackscreen.gameObject.activeSelf;
        }

        public override void Tick()
        {
            if (state == null || !ModEntry.Enabled) return;
            try
            {
                if (state.Departure == Departure.Caught) { ForceReturn(); return; }
                if (region != Region.Lower) StationArt.FitActive(region);
                if (Time.unscaledTime < nextRefresh) return;
                nextRefresh = Time.unscaledTime + 0.1f;
                Refresh();
            }
            catch (Exception ex)
            {
                if (!errorLogged) ModEntry.Note("Scavenging location error: " + ex);
                errorLogged = true;
            }
        }

        private void Search()
        {
            if (state == null || IsPaused() || InTransition()) return;
            StationGame.Search(state);
            if (state.Departure == Departure.Caught) ForceReturn();
            else Refresh();
        }

        private void Withdraw()
        {
            if (InTransition() || MapUIManager.Instance == null || MapUIManager.Instance.Current != this) return;
            // A canceled native ground-item confirmation must not end the outing.
            MapUIManager.Instance.Return();
        }

        private void ForceReturn()
        {
            if (departureRequested || InTransition() || MapUIManager.Instance == null || MapUIManager.Instance.Current != this) return;
            departureRequested = true;
            StationGame.Save(state);
            var entry = EmporiumEntry.Instance;
            try
            {
                var lost = GraphUtils.FindAllChildrenType<GameItem>(entry.afterhourInventory);
                for (int i = lost.Count - 1; i >= 0; i--)
                {
                    try { if (lost[i] != null && lost[i].parentInventory != null) lost[i].Destroy(); }
                    catch (Exception ex) { ModEntry.Note("Ground cleanup: " + ex.Message); }
                }
                entry.CloseItemWindowUIWindow();
                entry.HideAfterhourInv();
                if (GenericConfirmationUIManager.Instance != null && GenericConfirmationUIManager.Instance.panel != null && GenericConfirmationUIManager.Instance.panel.activeSelf)
                    GenericConfirmationUIManager.Instance.ClosePanel();
            }
            finally { ModHelper.ReturnFromAfterhour(false); }
        }

        private void Refresh()
        {
            if (state == null) return;
            var night = StationGame.Night;
            string count = ModEntry.L("attempts", night.Remaining, night.Total);
            if (ModHelper.IsAfterhourEventActive(VanillaAfterhourEvents.GEORGIO_ASSISTANCE)) count += ModEntry.L("shelter_bonus");
            string chance, risk;
            if (region == Region.Lower)
            {
                chance = LocHelper.GetLocalizedMechanic("afterhour_minor_wound_chance", ScavHelper.GetMinorWoundChance().ToString("F2", CultureInfo.InvariantCulture));
                risk = LocHelper.GetLocalizedMechanic("afterhour_major_wound_chance", ScavHelper.GetMajorWoundChance().ToString("F2", CultureInfo.InvariantCulture));
            }
            else
            {
                chance = ModEntry.L("precious_chance", (StationGame.PreciousChance(state) * 100).ToString("0.00", CultureInfo.InvariantCulture));
                risk = ModEntry.L("detection", (StationGame.DetectionChance(state) * 100).ToString("0.00", CultureInfo.InvariantCulture));
            }
            string reason = StationGame.BlockReason(state);
            if (panel != null) panel.Refresh(count, chance, risk, reason == null);
            if (fallback != null && fallback.IsAlive)
            {
                fallback.Get("attempts")?.SetText(count);
                fallback.Get("chance")?.SetText(chance);
                fallback.Get("risk")?.SetText(risk);
            }
            if ((reason == "no_cash" || reason == "exhausted" || reason == "caught" || reason == "wounded") && reason != lastBlockReason)
                StoreUIManager.Instance.Notify(ModEntry.L("block_" + reason));
            lastBlockReason = reason;
        }
    }
}
