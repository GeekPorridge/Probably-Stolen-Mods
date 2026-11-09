using System;

namespace PelicanCultivation
{
    public static class ReservoirEvents
    {
        public const string ActivityId="pelican_cultivation.reservoir.king_activity";
        public const string Modifier="pelican_king_chance_bonus",RollDay="fishing_event_roll_day";
        public const float DailyChance=20;
        public static void Enable()
        {
            ModHelper.AddAfterhourEvent(ModEntry.ModId,ActivityId,Fishing.LocationId,
                ()=>Expansion.L("king_activity_night"),1,()=>Expansion.L("king_activity_desc"),
                new[]{ModHelper.AfterhourEffect(()=>Expansion.L("king_activity_effect"),Modifier,1,AfterhourEffectPolarity.Good)},
                dailyChancePercent:DailyChance,tone:AfterhourEventTone.Good,canStart:ReserveDailyRoll);
        }
        public static void Disable()=>ModHelper.RemoveAfterhourEvents(ModEntry.ModId);
        public static bool ReserveDailyRoll()
        {
            if(!Progress.Ready||!FarmStock.Aquaculture||Progress.Get(RollDay,-1)==Progress.Day)return false;
            // The native daily-event loop calls canStart before its chance roll.
            // Persist even an unsuccessful roll so reopening/loading cannot reroll.
            Progress.Set(RollDay,Progress.Day);return true;
        }
        public static void EnsureToday()
        {
            // Covers installing/unlocking the mod after the native new-day loop.
            if(ReserveDailyRoll()&&RNG.Roll(DailyChance))ModHelper.StartAfterhourEvent(ActivityId,1);
        }
        public static double Multiplier=>!Progress.Ready?1:Math.Max(1,1+ModHelper.GetAfterhourModifier(Fishing.LocationId,Modifier));
    }
}
