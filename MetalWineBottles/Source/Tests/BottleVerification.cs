using System;
using System.Collections.Generic;
using MetalWineBottles;

public static class BottleVerification
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        checks++;
    }

    private sealed class Batch : IBottleBatch
    {
        public bool PairExists = true, InvalidateAfterStaging;
        public int Capacity = 10, Staged, Attempts, Source = 4, Target = 7, Rolls, ThrowAt;
        public bool PairAvailable => PairExists && (!InvalidateAfterStaging || Staged == 0);
        public bool TryStageBottle()
        {
            Attempts++;
            if (Attempts == ThrowAt) throw new InvalidOperationException("Injected factory/placement failure.");
            if (Staged >= Capacity) return false;
            Staged++;
            return true;
        }
        public void ConsumePair() { Source--; Target--; }
        public void Rollback() { Staged = 0; Rolls++; }
    }

    // Managed native objects with no Unity rendering. These test the real item
    // tags and callbacks without loading or modifying a save.
    private sealed class NativeItem : GameItem
    {
        public NativeItem(string id, string purity = null) : base(id)
        {
            identifier = id;
            state = new TagSystem();
            state.EnableTag("IS_OWNED_TAG");
            if (purity != null) state.GetTag("INGOT_PURITY_TAG").Enable().SetString(purity);
            modifiedState = state;
        }
        public override void Destroy() { unitCount = 0; parentInventory = null; }
        public override void Validate() { }
    }

    private sealed class NativeInventory : GameInventory
    {
        private readonly List<GameItem> items = new List<GameItem>();
        public override List<GameItem> childItems => items;
        public override List<GraphNodeStorage> children => new List<GraphNodeStorage>();
        public override bool IsAdjacent(SlotMarker a, SlotMarker b) => false;
        public override void Validate() { }
        public override bool MayHaveValidInventorySlot(GameItem item) => false;
        public override SlotMarker TryFindOneValidInventorySlot(GameItem item) => null;
        public override bool CheckInventorySlotStillValid(GameItem item, GridShape shape, TagSystem state) => true;
        public override SlotMarker TryInventorySlot(GameItem item, int count, UnityEngine.Vector2 center, GridShape shape, TagSystem state) => null;
        public override SlotMarker GetItemInventorySlot(GameItem item) => null;
        public override SlotMarker GetIncompleteItemInventorySlot(GameItem item) => null;
        public override List<TreeNodeRender> HighlightSlot(SlotMarker slot, RenderHandler.BackgroundColor color, bool replace) => new List<TreeNodeRender>();
        public override void ClearHighlight() { }
        public override bool UncheckedAccept(GameItem item) { items.Add(item); item.parentInventory = this; return true; }
        public override bool Expel(GameItem item) { items.Remove(item); item.parentInventory = null; return true; }
        public override bool AddParent(GraphNodeStorage node) { parent = node; return true; }
        public override bool RemoveParent(GraphNodeStorage node) { parent = null; return true; }
    }

    private static IngotStock Stock(int tier, int count = 1) => new IngotStock { Item = new object(), Tier = tier, Count = count };

    public static string Run()
    {
        checks = 0;
        int[] yields = BottleRules.DefaultYields;
        int[,] expected = {
            {1,1,2,2,3,3}, {1,2,2,3,3,4}, {2,2,3,3,4,4},
            {2,3,3,4,4,5}, {3,3,4,4,5,5}, {3,4,4,5,5,6}
        };
        for (int a = 1; a <= 6; a++)
            for (int b = 1; b <= 6; b++)
            {
                BatchPlan plan = BottleRules.Plan(Stock(a), Stock(b), yields);
                Check(plan != null && plan.Tier == expected[a - 1,b - 1], "mixed purity " + a + "+" + b);
                Check(plan.Bottles == yields[expected[a - 1,b - 1] - 1], "mixed output " + a + "+" + b);
            }
        var first = Stock(2, 5);
        var second = Stock(6, 7);
        Check(BottleRules.Plan(first, null, yields) == null, "one ingot cannot form a pair");
        Check(BottleRules.Plan(first, first, yields) == null, "one stack cannot target itself");
        Check(BottleRules.Plan(first, Stock(3, 0), yields) == null, "empty target is rejected");
        Check(BottleRules.Plan(Stock(0), second, yields) == null, "unknown true purity is rejected");
        BatchPlan stacked = BottleRules.Plan(first, second, yields);
        Check(stacked.First == first && stacked.Second == second, "one unit selected from each chosen ingot");
        Check(first.Count == 5 && second.Count == 7, "planning does not consume input");
        Check(BottleRules.MixedTier(0, 3) == 0 && BottleRules.MixedTier(3, 7) == 0, "invalid grades rejected");
        foreach (int count in yields)
        {
            for (int capacity = 0; capacity < count; capacity++)
            {
                Batch batch = new Batch { Capacity = capacity };
                Check(BatchTransaction.Run(count, batch) == BatchResult.OutputFull, "full inventory detected");
                Check(batch.Source == 4 && batch.Target == 7 && batch.Staged == 0 && batch.Rolls == 1,
                    "partial output removed and both ingots retained");
            }
            Batch success = new Batch { Capacity = count };
            Check(BatchTransaction.Run(count, success) == BatchResult.Completed, "exact capacity succeeds");
            Check(success.Source == 3 && success.Target == 6 && success.Staged == count && success.Rolls == 0,
                "success consumes exactly one unit from each ingot");
        }
        Batch missing = new Batch { PairExists = false };
        Check(BatchTransaction.Run(10, missing) == BatchResult.InvalidPair && missing.Attempts == 0,
            "invalid pair produces nothing");
        Batch lost = new Batch { InvalidateAfterStaging = true };
        Check(BatchTransaction.Run(10, lost) == BatchResult.InvalidPair && lost.Staged == 0 && lost.Source == 4 && lost.Target == 7,
            "pair changed during staging rolls back output");
        Batch throws = new Batch { ThrowAt = 4 };
        bool caught = false;
        try { BatchTransaction.Run(10, throws); } catch (InvalidOperationException) { caught = true; }
        Check(caught && throws.Staged == 0 && throws.Source == 4 && throws.Target == 7,
            "placement exception rolls back without spending ingots");
        Batch large = new Batch { Capacity = 50 };
        Check(BatchTransaction.Run(50, large) == BatchResult.Completed && large.Staged == 50 && large.Source == 3 && large.Target == 6,
            "configurable large batch still consumes exactly two units");
        int[] purityMods = { -25, -15, 0, 25, 50, 200 };
        for (int a = 0; a < 6; a++)
            for (int b = 0; b < 6; b++)
            {
                double ingotValue = 50 * (1 + purityMods[a] / 100.0) + 50 * (1 + purityMods[b] / 100.0);
                int bottles = yields[BottleRules.MixedTier(a + 1, b + 1) - 1];
                Check(12 * bottles < ingotValue, "ordinary bottle value below pair opportunity cost");
            }
        TestNativeInteraction();
        return checks + " checks passed: direct conversion, native item tags and callbacks, mixed grades, whole-batch rollback and ordinary value bounds.";
    }

    private static void TestNativeInteraction()
    {
        ModEntry entry = new ModEntry();
        entry.OnEnable();
        try
        {
            var inventory = new NativeInventory();
            var source = new NativeItem("metal_ingot", "INGOT_PURITY_LOW") { parentInventory = inventory };
            var target = new NativeItem("metal_ingot", "INGOT_PURITY_PERFECT") { parentInventory = inventory };
            source.state.GetTag("INGOT_PURITY_FAKE_TAG").Enable().SetString("INGOT_PURITY_PERFECT");
            Check(IngotConversion.NativeTier(source) == 2, "true purity used even when public purity is forged");
            Check(IngotConversion.NativeTier(target) == 6, "native perfect tag mapped correctly");
            int uses = 0, may = 0, can = 0;
            source.mayThisTargetItemFunc = (s, t) => { may++; return t.identifier == "vending_machine_core"; };
            source.canThisTargetItemFunc = (s, t) => { can++; return t.identifier == "vending_machine_core"; };
            source.onThisTargetItemFunc = (s, t) => { uses++; };
            IngotConversion.Attach(source);
            Check(source.mayThisTargetItemFunc(source, target) && source.canThisTargetItemFunc(source, target),
                "native ingot-to-ingot callbacks accept an owned pair");
            Check(may == 0 && can == 0, "new pair handling does not invoke upgrade filters");
            Check(!IngotConversion.CanCombine(source, source), "native self-target rejected");
            target.state.DisableTag("IS_OWNED_TAG");
            Check(!IngotConversion.CanCombine(source, target), "unowned target cannot be consumed");
            target.state.EnableTag("IS_OWNED_TAG");
            inventory.LockInv();
            Check(!IngotConversion.CanCombine(source, target), "locked inventory protects ingots");
            inventory.UnlockInv();
            var upgradeTarget = new NativeItem("vending_machine_core");
            Check(source.mayThisTargetItemFunc(source, upgradeTarget) && source.canThisTargetItemFunc(source, upgradeTarget),
                "original upgrade filters remain available");
            source.onThisTargetItemFunc(source, upgradeTarget);
            Check(may == 1 && can == 1 && uses == 1, "original upgrade callback runs once");
            entry.OnDisable();
            Check(!IngotConversion.CanCombine(source, target), "conversion stops when disabled");
            Check(source.mayThisTargetItemFunc(source, upgradeTarget), "disabled mod still delegates native upgrades");
        }
        finally { entry.OnDisable(); }
    }
}
