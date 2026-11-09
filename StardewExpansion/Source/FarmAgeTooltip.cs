using System.Collections;
using HarmonyLib;

namespace PelicanCultivation
{
    [HarmonyPatch(typeof(AquacultureHelper),"CreateFishTooltip")]
    public static class FarmAgeTooltip
    {
        public static void Postfix(RichTextBuilder builder,GameItem item,FishSpecies species)
        {
            if(!ModEntry.Enabled || species==null || !FarmRules.Aquatic(species.speciesID)) return;
            int age=item.GetTagReadonly("FISH_AGE_TAG").GetInt();
            if(species.GetStage(age)<FishStage.Elder || age>=species.oldAgeDays) return;
            // Native oldAgeDays starts mortality checks; it is later than the
            // Elder stage. Correct only the contradictory tooltip sentence.
            string oldLine=LocHelper.GetLocalizedMechanic("aquaculture_old_age_in",species.oldAgeDays-age);
            var slices=(IList)ModReflect.Get(builder,"slices");
            for(int i=slices.Count-1;i>=0;i--)
                if(ModReflect.Get<string>(slices[i],"text")==oldLine)
                {
                    slices.RemoveAt(i);
                    builder.AddLine(LocHelper.GetLocalizedMechanic("aquaculture_old_age"),true,RenderHandler.ColorPalette.Orange);
                    break;
                }
        }
    }
}
