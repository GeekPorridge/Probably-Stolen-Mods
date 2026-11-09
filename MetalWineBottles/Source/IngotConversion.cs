using System;
using System.Collections.Generic;
using HarmonyLib;

namespace MetalWineBottles
{
    [HarmonyPatch(typeof(MaterialDirectory), "MetalIngot")]
    public static class IngotFactoryPatch
    {
        public static void Postfix(GameItem __result)
        {
            if (ModEntry.Enabled && __result != null) IngotConversion.Attach(__result);
        }
    }

    public static class IngotConversion
    {
        private static readonly HashSet<GameItem> activeItems = new HashSet<GameItem>();

        public static int NativeTier(GameItem ingot)
        {
            if (ingot == null || ingot.identifier != "metal_ingot") return 0;
            // Same true-purity tag as the native helper; never use the public or
            // counterfeit grade to determine production.
            switch (ingot.GetTagReadonly(IngotPurityHelper.INGOT_PURITY_TAG).GetString())
            {
                case IngotPurityHelper.INGOT_PURITY_VERY_LOW: return 1;
                case IngotPurityHelper.INGOT_PURITY_LOW: return 2;
                case IngotPurityHelper.INGOT_PURITY_FAIR: return 3;
                case IngotPurityHelper.INGOT_PURITY_HIGH: return 4;
                case IngotPurityHelper.INGOT_PURITY_VERY_HIGH: return 5;
                case IngotPurityHelper.INGOT_PURITY_PERFECT: return 6;
                default: return 0;
            }
        }

        private static bool IsIngotPair(GameItem source, GameItem target) =>
            source != null && target != null && source.identifier == "metal_ingot" && target.identifier == "metal_ingot";

        internal static BatchPlan PlanFor(GameItem source, GameItem target)
        {
            if (!IsIngotPair(source, target) || ReferenceEquals(source, target)
                || !GeneralHelper.IsItemEffectivelyOwned(source) || !GeneralHelper.IsItemEffectivelyOwned(target)
                || source.parentInventory?.IsRemoveLocked() == true || target.parentInventory?.IsRemoveLocked() == true
                || (source.parentInventory == null && target.parentInventory == null)) return null;
            return BottleRules.Plan(
                new IngotStock { Item = source, Tier = NativeTier(source), Count = source.unitCount },
                new IngotStock { Item = target, Tier = NativeTier(target), Count = target.unitCount }, ModEntry.Yields());
        }

        public static bool CanCombine(GameItem source, GameItem target) =>
            ModEntry.Enabled && source != null && target != null
            && !activeItems.Contains(source) && !activeItems.Contains(target) && PlanFor(source, target) != null;

        public static void Attach(GameItem ingot)
        {
            var originalMay = ingot.mayThisTargetItemFunc;
            var originalCan = ingot.canThisTargetItemFunc;
            var originalUse = ingot.onThisTargetItemFunc;
            ingot.mayThisTargetItemFunc = (source, target) =>
                ModEntry.Enabled && IsIngotPair(source, target) ? CanCombine(source, target)
                : originalMay != null && originalMay(source, target);
            ingot.canThisTargetItemFunc = (source, target) =>
                ModEntry.Enabled && IsIngotPair(source, target) ? CanCombine(source, target)
                : originalCan != null && originalCan(source, target);
            ingot.onThisTargetItemFunc = (source, target) =>
            {
                if (ModEntry.Enabled && IsIngotPair(source, target)) TryCombine(source, target);
                else originalUse?.Invoke(source, target);
            };
        }

        public static bool TryCombine(GameItem source, GameItem target)
        {
            if (!CanCombine(source, target)) return false;
            BatchPlan plan = PlanFor(source, target);
            activeItems.Add(source);
            activeItems.Add(target);
            try
            {
                var batch = new NativeBottleBatch(plan, source.parentInventory, target.parentInventory);
                BatchResult result = BatchTransaction.Run(plan.Bottles, batch);
                if (result == BatchResult.OutputFull) ShowFullInventory();
                return result == BatchResult.Completed;
            }
            catch (Exception error) { ModEntry.Error(error); return false; }
            finally { activeItems.Remove(source); activeItems.Remove(target); }
        }

        private static void ShowFullInventory()
        {
            GenericConfirmationUIManager ui = GenericConfirmationUIManager.Instance;
            if (ui == null) return;
            ui.OpenPanelSingleButton(ModEntry.L("output_full"), LocHelper.GetLocalizedMainMenu("confirm"), () => ui.ClosePanel());
        }
    }

    internal sealed class NativeBottleBatch : IBottleBatch
    {
        private readonly BatchPlan plan;
        private readonly GameItem source, target;
        private readonly GameInventory sourceInventory, targetInventory;
        private readonly List<GameInventory> destinations = new List<GameInventory>();
        private readonly List<GameItem> staged = new List<GameItem>();

        internal NativeBottleBatch(BatchPlan plan, GameInventory sourceInventory, GameInventory targetInventory)
        {
            this.plan = plan;
            source = (GameItem)plan.First.Item;
            target = (GameItem)plan.Second.Item;
            this.sourceInventory = sourceInventory;
            this.targetInventory = targetInventory;
            if (targetInventory != null) destinations.Add(targetInventory);
            if (sourceInventory != null && !ReferenceEquals(sourceInventory, targetInventory)) destinations.Add(sourceInventory);
        }

        public bool PairAvailable
        {
            get
            {
                BatchPlan current = IngotConversion.PlanFor(source, target);
                return current != null && ReferenceEquals(source.parentInventory, sourceInventory)
                    && ReferenceEquals(target.parentInventory, targetInventory)
                    && current.First.Tier == plan.First.Tier && current.Second.Tier == plan.Second.Tier;
            }
        }

        public bool TryStageBottle()
        {
            GameItem bottle = DirectoryMaster.Item("wine_bottle");
            if (bottle == null) throw new InvalidOperationException("Native wine_bottle is unavailable.");
            staged.Add(bottle);
            // Ordinary placement only. No machine inventory filters are changed.
            foreach (GameInventory inventory in destinations)
            {
                if (inventory.IsInsertLocked()) continue;
                if (GraphUtils.TryAcceptAll(inventory, bottle) == 0 && bottle.parentInventory != null) return true;
            }
            return false;
        }

        public void ConsumePair()
        {
            ConsumeOne(source);
            ConsumeOne(target);
        }

        private static void ConsumeOne(GameItem item)
        {
            if (item.unitCount == 1) item.Destroy();
            else item.SetUnitCount(item.unitCount - 1);
        }

        public void Rollback()
        {
            foreach (GameItem bottle in staged) bottle.Destroy();
            staged.Clear();
        }
    }
}
