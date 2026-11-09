using System;

namespace MetalWineBottles
{
    public enum BatchResult { InvalidPair, OutputFull, Completed }

    public sealed class IngotStock
    {
        public object Item;
        public int Tier;
        public int Count;
    }

    public sealed class BatchPlan
    {
        public IngotStock First, Second;
        public int Tier, Bottles;
    }

    // Pure rules: six native grades numbered 1..6; two physical units per batch.
    public static class BottleRules
    {
        public static readonly int[] DefaultYields = { 1, 3, 5, 7, 9, 10 };

        public static int MixedTier(int first, int second)
        {
            if (first < 1 || first > 6 || second < 1 || second > 6) return 0;
            return (first + second) / 2;
        }

        public static BatchPlan Plan(IngotStock first, IngotStock second, int[] yields)
        {
            if (yields == null || yields.Length != 6) throw new ArgumentException("Six yields required.");
            if (first == null || second == null || first.Item == null || second.Item == null
                || ReferenceEquals(first.Item, second.Item) || first.Count < 1 || second.Count < 1) return null;
            int tier = MixedTier(first.Tier, second.Tier);
            if (tier == 0) return null;
            return new BatchPlan { First = first, Second = second, Tier = tier, Bottles = Math.Max(1, yields[tier - 1]) };
        }
    }

    public interface IBottleBatch
    {
        bool PairAvailable { get; }
        bool TryStageBottle();
        void ConsumePair();
        void Rollback();
    }

    public static class BatchTransaction
    {
        // Reserve room for the whole output before consuming either input.
        public static BatchResult Run(int count, IBottleBatch batch)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (!batch.PairAvailable) return BatchResult.InvalidPair;
            bool committed = false;
            try
            {
                for (int i = 0; i < count; i++)
                    if (!batch.TryStageBottle()) return BatchResult.OutputFull;
                if (!batch.PairAvailable) return BatchResult.InvalidPair;
                batch.ConsumePair();
                committed = true;
                return BatchResult.Completed;
            }
            finally
            {
                if (!committed) batch.Rollback();
            }
        }
    }
}
