using System;

namespace MetalWineBottles
{
    public sealed class ModEntry : IMod
    {
        public const string ModId = "metal_wine_bottles";
        public const string Version = "0.2.1";
        internal static bool Enabled;
        private static ModLog log;
        private static readonly ConfigEntry<int>[] yields = new ConfigEntry<int>[6];

        public void Init(ModManifest manifest)
        {
            log = new ModLog(manifest);
            ModConfig config = ModHelper.GetConfig(ModId);
            for (int i = 0; i < yields.Length; i++)
            {
                int grade = i + 1;
                yields[i] = config.Define("direct_bottles_grade_" + grade, BottleRules.DefaultYields[i],
                    "Bottle yield / 出瓶量", () => L("grade_" + grade), min: 1, max: 50,
                    tooltip: () => L("config_hint"));
            }
        }

        public void OnEnable()
        {
            if (Enabled) return;
            Enabled = true;
            ModHook.OnCreateTooltipLate += Tooltip;
            log?.Log("Metal Wine Bottles " + Version + " enabled: use one ingot on another; average purity rounded down; only the two ingots are consumed.");
        }

        public void OnDisable()
        {
            Enabled = false;
            ModHook.OnCreateTooltipLate -= Tooltip;
        }

        internal static int[] Yields()
        {
            int[] result = new int[6];
            for (int i = 0; i < result.Length; i++)
                result[i] = yields[i] == null ? BottleRules.DefaultYields[i] : yields[i].Value;
            return result;
        }

        internal static string L(string key, params object[] args) => ModHelper.GetLocalized(ModId, key, args);
        internal static void Error(Exception error) => log?.Error("Direct bottle conversion stopped: " + error);

        private static void Tooltip(RichTextBuilder builder, GameItem item)
        {
            if (!Enabled || item == null) return;
            if (item.identifier == "metal_ingot")
            {
                builder.AddLine(" ");
                builder.AddLine(L("ingot_hint"));
                builder.AddLine(L("yield_hint", string.Join(" / ", Yields())));
                builder.AddLine(L("mix_hint"));
            }
        }
    }
}
