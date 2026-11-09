using System;

namespace StationScavenging
{
    public enum Region { Lower, Upper, Security, Revolution, BlackMarket }
    public enum LootProfile { Ordinary, Focused }
    public enum SupplyCondition { Normal, Household, Equipment, Patrol }
    public enum Departure { None, Voluntary, Caught }

    public sealed class BalanceSettings
    {
        public int BaseFine = 20;
        public double FineRate = 0.25;
        public int FineCap = 200;
        public double BaseReputationLoss = 0.5;
        public double ReputationValueDivisor = 400;
        public double StandardReputationCap = 3;
        public int PreciousRestNights = 3;
        public double CrateChance = 0.01;
        public double CrateChanceAtEnd = 0.05;
        public double KeycardChance = 0.015;
        public double KeycardChanceAtEnd = 0.075;
    }

    public sealed class NightState
    {
        public int Day;
        public int Total = 10;
        public int Used;
        public bool Caught;
        public bool Closed;
        public int Remaining { get { return Caught || Closed ? 0 : Math.Max(0, Total - Used); } }
        public double Progress { get { return Math.Max(0, Math.Min(1, Used / (double)Math.Max(1, Total - 1))); } }

        public bool TrySpend()
        {
            if (Remaining <= 0) return false;
            Used++;
            return true;
        }
    }

    public sealed class OutingState
    {
        public int Day;
        public Region Region;
        public int Searches;
        public int PreciousFinds;
        public long FoundValue;
        public Departure Departure;
        public bool Ended { get { return Departure != Departure.None; } }
        public bool TrySearch(NightState night)
        {
            if (Ended || night == null || night.Day != Day || !night.TrySpend()) return false;
            Searches++;
            return true;
        }

        public bool End(Departure reason)
        {
            if (Ended || reason == Departure.None) return false;
            Departure = reason;
            return true;
        }

        public void RecordValue(long value)
        {
            FoundValue = Math.Min(1000000000L, FoundValue + Math.Max(0, Math.Min(1000000000L, value)));
        }
    }

    public struct Penalty
    {
        public int Credits;
        public double Reputation;
        public double ReputationCap;
    }

    public static class StationRules
    {
        public const int AllRegionsMask = 31;

        public static string LocationId(Region region)
        {
            switch (region)
            {
                case Region.Upper: return "station_scavenging.upper_levels";
                case Region.Security: return "station_scavenging.security";
                case Region.Revolution: return "station_scavenging.revolution";
                case Region.BlackMarket: return "station_scavenging.black_market";
                default: return "dumping_ground";
            }
        }

        public static int RequiredDay(Region region)
        {
            switch (region)
            {
                case Region.Upper: return 10;
                case Region.Security: return 15;
                case Region.Revolution: return 18;
                case Region.BlackMarket: return 20;
                default: return 1;
            }
        }

        public static double RequiredReputation(Region region)
        {
            switch (region)
            {
                case Region.Upper: return 5;
                case Region.Security: return 10;
                case Region.Revolution: return 12;
                case Region.BlackMarket: return 15;
                default: return 0;
            }
        }

        public static string FactionId(Region region)
        {
            switch (region)
            {
                case Region.Upper: return "FACTION_UPPER_LEVEL";
                case Region.Security: return "FACTION_SECURITY";
                case Region.Revolution: return "FACTION_REVOLUTION";
                case Region.BlackMarket: return "FACTION_BLACK_MARKET";
                default: return "FACTION_LOWER_LEVEL";
            }
        }

        public static bool MeetsUnlock(Region region, int day, double reputation, bool exchangeUnlocked)
        {
            return region == Region.Lower || day >= RequiredDay(region) && reputation >= RequiredReputation(region)
                && (region != Region.BlackMarket || exchangeUnlocked);
        }

        public static double DetectionChance(Region region, double progress, SupplyCondition condition)
        {
            if (region == Region.Lower) return 0;
            // Per-action odds are calibrated for a ten-search night, rather than
            // carrying over the much steeper real-time curve.
            double chance = 0.01;
            if (region == Region.Security) chance += 0.01;
            else if (region == Region.Revolution || region == Region.BlackMarket) chance += 0.005;
            if (condition == SupplyCondition.Patrol) chance += 0.02;
            return Math.Min(0.40, chance + ClampProgress(progress) * 0.05);
        }

        public static bool CanFindPrecious(int day, int lastFoundDay, int restNights)
        {
            return lastFoundDay < 0 || day - lastFoundDay > Math.Max(0, restNights);
        }

        public static int PreciousWait(int day, int lastFoundDay, int restNights)
        {
            return lastFoundDay < 0 ? 0 : Math.Max(0, lastFoundDay + Math.Max(0, restNights) + 1 - day);
        }

        public static double PreciousChance(Region region, double progress, bool available, bool blackHasCards, BalanceSettings settings)
        {
            // All five regions, including lower service crates, share rest nights.
            if (!available) return 0;
            bool cards = region == Region.BlackMarket && blackHasCards;
            double start = Math.Max(0, Math.Min(1, Finite(cards ? settings.KeycardChance : settings.CrateChance, 0)));
            double end = Math.Max(start, Math.Min(1, Finite(cards ? settings.KeycardChanceAtEnd : settings.CrateChanceAtEnd, start)));
            return start + (end - start) * ClampProgress(progress);
        }

        private static double ClampProgress(double progress)
        {
            return Math.Max(0, Math.Min(1, Finite(progress, 0)));
        }

        public static Penalty GetPenalty(long foundValue, double gainMultiplier, double factionLossMultiplier, BalanceSettings settings)
        {
            double value = Math.Max(0, foundValue);
            int cap = Math.Max(0, settings.FineCap);
            double fee = Math.Max(0, settings.BaseFine) + value * Math.Max(0, Finite(settings.FineRate, 0.25));
            double gain = Math.Max(0, Finite(gainMultiplier, 1));
            double faction = Math.Max(0, Finite(factionLossMultiplier, 1));
            double repCap = Math.Max(0, Finite(settings.StandardReputationCap, 3));
            double divisor = Math.Max(1, Finite(settings.ReputationValueDivisor, 400));
            double rep = Math.Min(repCap, (Math.Max(0, settings.BaseReputationLoss) + value / divisor) * faction) * gain;
            return new Penalty { Credits = (int)Math.Min(cap, Math.Ceiling(fee)), Reputation = rep, ReputationCap = repCap * gain };
        }

        public static int StableChoice(int seed, int day, int salt, int count)
        {
            if (count <= 1) return 0;
            unchecked
            {
                uint v = (uint)seed ^ (uint)day * 0x9E3779B9u ^ (uint)salt * 0x85EBCA6Bu;
                v ^= v >> 16; v *= 0x7FEB352Du; v ^= v >> 15; v *= 0x846CA68Bu; v ^= v >> 16;
                return (int)(v % (uint)count);
            }
        }

        private static double Finite(double value, double fallback) { return double.IsNaN(value) || double.IsInfinity(value) ? fallback : value; }
    }
}
