using System;

namespace PelicanCultivation
{
    public static class FishingDifficulty
    {
        private static ConfigEntry<int> level;
        public const int Default=3;
        public static int Clamp(int value)=>Math.Max(1,Math.Min(5,value));
        public static int Current=>Clamp(level?.Value??Default);
        public static void Define(ModConfig config)
        {
            level=config.Define("night_fishing_difficulty",Default,"Night fishing / 夜钓",()=>Expansion.L("config_fishing_difficulty"),
                min:1,max:5,tooltip:()=>Expansion.L("config_fishing_difficulty_hint"));
        }
    }
}
