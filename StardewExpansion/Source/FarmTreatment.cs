using System;
using System.Collections.Generic;

namespace PelicanCultivation
{
    public static class FarmTreatment
    {
        public static bool NeedsSerum(GameItem animal,string immunityPart)
            => animal!=null && animal.IsTag("ANIMAL_ITEM_TAG") && !HusbandryHelper.IsAnimalDead(animal)
                && GeneralHelper.IsItemOwned(animal) && animal.IsTag("ANIMAL_DISEASED_TAG")
                && !string.IsNullOrEmpty(immunityPart) && animal.GetTagReadonly(immunityPart).GetInt()<=0
                && !animal.IsTag(immunityPart+"_INGESTER_ACTIVE");
        public static IEnumerable<GameItem> AvailableSerums()
        {
            var seen=new HashSet<GameItem>();
            foreach(var root in new GraphNodeStorage[]{EmporiumEntry.Instance.invElement,EmporiumEntry.Instance.backInvinvElement})
                foreach(var item in GraphUtils.FindAllChildrenType<GameItem>(root))
                {
                    if(!seen.Add(item) || item.identifier!="serum_green" || !GeneralHelper.IsItemOwned(item)) continue;
                    bool reserved=false;
                    foreach(var parent in GraphUtils.FindParentsType<GameItem>(item.parentInventory))
                        if(FarmHusbandry.Device(parent) || parent.IsGameItemType("MACHINE") || parent.IsTag("STANDARD_MACHINE_TAG") || parent.IsTag("manufacture")) {reserved=true;break;}
                    if(!reserved) yield return item;
                }
        }
        public static bool UseSerum(GameItem animal,IEnumerable<GameItem> supplies,string immunityPart)
        {
            if(!NeedsSerum(animal,immunityPart)) return false;
            foreach(var serum in supplies)
            {
                if(serum==null || serum.identifier!="serum_green" || !GeneralHelper.IsItemOwned(serum) || !UseCountHelper.CanUse(serum)
                    || serum.onThisTargetItemFunc==null || serum.canThisTargetItemFunc==null
                    || !serum.canThisTargetItemFunc(serum,animal)) continue;
                // Invoke the native item callback: it spends a real dose and
                // injects the real temporary effect, without clearing disease.
                int before=animal.GetTagReadonly(immunityPart).GetInt();
                serum.onThisTargetItemFunc(serum,animal);
                return animal.GetTagReadonly(immunityPart).GetInt()>before;
            }
            return false;
        }
        public static void TryTreatAtNight(GameItem animal)
        {
            if(!ModEntry.Enabled || !Progress.Ready || !FarmNightReport.Collecting || !FarmSettings.AutoImmunity) return;
            try
            {
                var liquid=Liquid.GetLiquidFromId("livestock_immunity");
                if(liquid!=null && UseSerum(animal,AvailableSerums(),liquid.currentPart)) FarmNightReport.RecordTreatment();
            }
            catch(Exception error){ModEntry.Error("Nightly immunity serum",error);}
        }
    }
}
