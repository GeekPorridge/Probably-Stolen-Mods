using System;
using System.Collections.Generic;
using PelicanCultivation;

public static class SeaFounderVerification
{
    private static int checks;
    private static void Check(bool value,string context){if(!value) throw new Exception("FOUNDER FAIL: "+context);checks++;}
    private sealed class Item : GameItem
    {
        public Item(string id) : base(id){modifiedState=state;SetShape(new GridShapeBuilder().SetData(new byte[]{1},1));}
        public override void Validate(){modifiedState=state;}
        public override void Destroy(){unitCount=0;parentInventory=null;}
    }
    private static Item Egg(bool owned)
    {
        var item=new Item("pelican_sea_cucumber_roe");item.EnableTag("FISH_ROE_TAG");item.EnableTag("FISH_GROUP_TAG");
        FarmSpecies.SetString(item,"FISH_SPECIES_TAG",FarmRules.Sea);Crops.SetInt(item,"FISH_GROUP_COUNT_TAG",2);GeneralHelper.SetItemOwned(item,owned);return item;
    }
    public static string Run(IDictionary<string,int> data)
    {
        checks=0;data[SeaFounderPair.PurchasedKey]=0;data[SeaFounderPair.EstablishedKey]=0;
        var farmer=new StoreClient{identifier=Merchant.Id};var egg=Egg(false);
        Check(!egg.IsTag(SeaFounderPair.BatchTag),"displayed eggs are not claimed until an actual purchase");
        SeaFounderPurchasePatch.Postfix(egg,45,new StoreClient{identifier="other_vendor"});
        Check(data[SeaFounderPair.PurchasedKey]==0,"other vendors do not claim the farmer's starter batch");
        SeaFounderPurchasePatch.Postfix(egg,0,farmer);Check(!egg.IsTag(SeaFounderPair.BatchTag),"a zero-cost nonpurchase does not claim a starter");
        SeaFounderPurchasePatch.Postfix(egg,45,farmer);
        Check(egg.IsTag(SeaFounderPair.BatchTag) && data[SeaFounderPair.PurchasedKey]==1,"successful first purchase marks exactly its own clutch");
        var next=Egg(true);SeaFounderPurchasePatch.Postfix(next,45,farmer);Check(!next.IsTag(SeaFounderPair.BatchTag),"later purchases are not forced into paired sexes");
        GeneralHelper.SetItemOwned(egg,true);Crops.SetInt(egg,"OTHER_MOD_DATA",92);
        var fry=new Item("pelican_sea_cucumber_fry");FarmFishSafety.Prepare(egg,fry,FarmSpecies.SeaCucumbers,1,true);
        Check(fry.IsTag(SeaFounderPair.BatchTag) && fry.GetTagReadonly("FISH_GROUP_COUNT_TAG").GetInt()==2,"starter marker and projected count survive egg-to-larvae conversion");
        Check(!SeaFounderPair.Guarantees(egg,FarmSpecies.SeaCucumbers,1,true),"the guarantee is not consumed at the grouped hatch stage");
        var restored=new Item("pelican_sea_cucumber_fry");foreach(var pair in fry.state.dict) restored.state.dict[pair.Key]=pair.Value.Clone();restored.Validate();
        Check(SeaFounderPair.Guarantees(restored,FarmSpecies.SeaCucumbers,2,false),"saved starter larvae retain their guarantee on restoration");
        Check(!SeaFounderPair.Guarantees(restored,FarmSpecies.SeaCucumbers,1,false),"one surviving larva never creates a second free animal");
        Check(!SeaFounderPair.Guarantees(restored,FarmSpecies.Lobsters,2,false),"other aquatic species retain their native sex rules");
        foreach(string factorySex in new[]{"MALE","FEMALE"})
        {
            var children=new List<GameItem>();
            for(int index=0;index<2;index++)
            {
                var child=new Item("pelican_sea_cucumber");FarmSpecies.SetString(child,"FISH_SEX_TAG",factorySex);
                FarmFishSafety.Prepare(restored,child,FarmSpecies.SeaCucumbers,2,false);
                SeaFounderPair.PrepareIndividual(child,index,true);children.Add(child);
                Check(child.GetTagReadonly("OTHER_MOD_DATA").GetInt()==92 && !child.IsTag(SeaFounderPair.BatchTag),"paired individuals preserve other state and do not propagate the starter marker");
            }
            Check(children[0].GetTagReadonly("FISH_SEX_TAG").GetString()=="MALE" && children[1].GetTagReadonly("FISH_SEX_TAG").GetString()=="FEMALE","an all-male or all-female factory result becomes exactly one male and one female");
            var full=new GridShapeBuilder().SetData(new byte[]{0},1).Build();
            Check(FarmFishSafety.Plan(full,new[]{restored},restored,children)==null && data[SeaFounderPair.EstablishedKey]==0 && restored.IsTag(SeaFounderPair.BatchTag),"insufficient space retains the original group and unconsumed guarantee");
        }
        var randomChild=new Item("pelican_sea_cucumber");FarmSpecies.SetString(randomChild,"FISH_SEX_TAG","FEMALE");SeaFounderPair.PrepareIndividual(randomChild,0,false);
        Check(randomChild.GetTagReadonly("FISH_SEX_TAG").GetString()=="FEMALE","ordinary offspring keep their factory-assigned sex");
        SeaFounderPair.Commit();Check(data[SeaFounderPair.EstablishedKey]==1 && !SeaFounderPair.Guarantees(restored,FarmSpecies.SeaCucumbers,2,false),"a successfully established pair consumes the one-time guarantee");
        data[SeaFounderPair.PurchasedKey]=0;data[SeaFounderPair.EstablishedKey]=0;
        var legacy=Egg(true);Crops.SetInt(legacy,"BOUGHT_PRICE_TAG",45);SeaFounderPair.RecoverPurchasedBatch(legacy,FarmSpecies.SeaCucumbers);
        Check(legacy.IsTag(SeaFounderPair.BatchTag) && data[SeaFounderPair.PurchasedKey]==1,"old purchased eggs can recover the guarantee from native purchase metadata");
        data[SeaFounderPair.PurchasedKey]=0;data[SeaFounderPair.EstablishedKey]=0;
        var natural=Egg(true);SeaFounderPair.RecoverPurchasedBatch(natural,FarmSpecies.SeaCucumbers);
        Check(!natural.IsTag(SeaFounderPair.BatchTag) && data[SeaFounderPair.PurchasedKey]==0,"unbought naturally laid eggs do not receive a starter guarantee");
        bool enabled=ModEntry.Enabled;ModEntry.Enabled=false;SeaFounderPurchasePatch.Postfix(natural,45,farmer);ModEntry.Enabled=enabled;
        Check(!natural.IsTag(SeaFounderPair.BatchTag),"disabled mod does not alter purchase state");
        return "Passed "+checks+" sea-cucumber founder-pair checks (first purchase, hatch metadata, both same-sex rolls, restoration, full space and legacy purchase recovery).";
    }
}
