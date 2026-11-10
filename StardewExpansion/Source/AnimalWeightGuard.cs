using System;
using HarmonyLib;

namespace PelicanCultivation
{
    public static class AnimalWeightGuard
    {
        public const string LastWeight="PELICAN_LAST_VALID_WEIGHT";
        public static bool Valid(float weight)=>weight>0&&!float.IsNaN(weight)&&!float.IsInfinity(weight);
        private static bool Custom(GameItem item)
        {
            string species=item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString();
            return FarmRules.Mammal(species)||ExpansionStock.NewAnimal(species);
        }
        public static void Repair(GameItem item,float before=0)
        {
            if(!ModEntry.Enabled||item==null||!Custom(item)||HusbandryHelper.IsAnimalDead(item))return;
            float weight=item.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat();
            if(!Valid(weight))
            {
                float previous=item.GetTagReadonly(LastWeight).GetFloat();
                if(Valid(before))weight=before;
                else if(Valid(previous))weight=previous;
                else
                {
                    var species=HusbandryHelper.GetSpecies(item);if(species?.weightStages==null||species.weightStages.Length==0||species.growthStages==null)return;
                    int age=item.GetTagReadonly("ANIMAL_AGE_TAG").GetInt(),stage=0;
                    for(int i=1;i<species.growthStages.Length;i++)if(age>=species.growthStages[i])stage=i;
                    // Match the native minimum instead of inventing a fully
                    // grown body's harvest value for a corrupt old record.
                    weight=Math.Max(1,(float)Math.Round(species.weightStages[Math.Min(stage,species.weightStages.Length-1)]*0.25f));
                }
                FarmSpecies.SetFloat(item,"ANIMAL_WEIGHT_TAG",weight);
            }
            FarmSpecies.SetFloat(item,LastWeight,weight);
        }
    }
    [HarmonyPatch(typeof(HusbandryHelper),nameof(HusbandryHelper.HandleDisease))]
    public static class AnimalDiseaseWeightPatch
    {
        public static void Prefix(GameItem item,out float __state){__state=item.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat();}
        public static void Postfix(GameItem item,float __state){AnimalWeightGuard.Repair(item,__state);}
    }
}
