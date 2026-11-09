using System;
using System.Globalization;

namespace StationScavenging
{
    internal static class StationFlavor
    {
        internal static string StatusKey(Region region)
        {
            var night = StationGame.Night;
            if (night.Caught) return "status_caught";
            if (night.Remaining <= 0) return "status_exhausted";
            if (region != Region.Lower && !StationGame.IsUnlocked(region)) return "status_locked";
            if (region != Region.Lower && StationGame.GetOuting(region).Ended) return "status_visited";
            return null;
        }

        internal static string CardInfo(Region region)
        {
            var night = StationGame.Night;
            int day = StoreStation.Instance.dayCounter;
            if (region == Region.Lower)
            {
                string hint = night.Remaining <= 0 ? ModEntry.L(night.Caught ? "block_caught" : "lower_exhausted_hint") : "";
                if (!StationGame.PreciousAvailable)
                    hint += (hint.Length == 0 ? "" : "\n") + ModEntry.L("rest_notice",
                        StationRules.PreciousWait(day, StationGame.LastPreciousDay, ModEntry.Settings().PreciousRestNights));
                return hint;
            }
            if (!StationGame.IsUnlocked(region))
            {
                string reason = day < StationRules.RequiredDay(region) ? "day" : StationGame.Reputation(region) < StationRules.RequiredReputation(region) ? "rep" : "exchange";
                int variant = StationRules.StableChoice(StationGame.Seed, day, 1007 + (int)region, 2);
                string taunt = ModEntry.L("taunt_" + (int)region + "_" + reason + "_" + variant);
                string requirements = ModEntry.L("unlock_requirements", StationRules.RequiredDay(region),
                    StationRules.RequiredReputation(region).ToString("0.#", CultureInfo.InvariantCulture),
                    StationGame.Reputation(region).ToString("0.#", CultureInfo.InvariantCulture));
                if (region == Region.BlackMarket && !StationGame.ExchangeUnlocked()) requirements += "\n" + ModEntry.L("unlock_exchange");
                return "<i>" + taunt + "</i>\n" + requirements;
            }
            string text = ModEntry.L("region_flavor_" + (int)region) + "\n" + ModEntry.L("supply_" + (int)StationGame.Condition(region));
            if (StationGame.Reputation(region) < StationRules.RequiredReputation(region))
            {
                int variant = StationRules.StableChoice(StationGame.Seed, day, 1007 + (int)region, 2);
                text = "<i>" + ModEntry.L("taunt_" + (int)region + "_rep_" + variant) + "</i>\n"
                    + ModEntry.L("reputation_existing_access") + "\n" + text;
            }
            if (!StationGame.PreciousAvailable)
                text += "\n" + ModEntry.L("rest_notice", StationRules.PreciousWait(day, StationGame.LastPreciousDay, ModEntry.Settings().PreciousRestNights));
            return text;
        }
    }
}
