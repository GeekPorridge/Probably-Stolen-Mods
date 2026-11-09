using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PelicanCultivation;

public static class FarmVerification
{
    private static int checks;
    private static void Check(bool value,string context){if(!value) throw new Exception("FARM FAIL: "+context);checks++;}
    private sealed class Item : GameItem
    {
        public Item(string id,int w=1,int h=1) : base(id)
        {modifiedState=state;var data=new byte[w*h];for(int i=0;i<data.Length;i++) data[i]=1;SetShape(new GridShapeBuilder().SetData(data,w));}
        public override void Validate(){modifiedState=state;}
        public override void Destroy(){unitCount=0;parentInventory=null;}
    }
    public static bool TextFixture(string key,object[] args,ref string __result){__result=NightReportVerification.Text(key,args);return false;}
    public static string Run()
    {
        checks=0;FarmSpecies.Register();
        Check(FarmSpecies.Cattle!=AnimalSpecies.Rat && FarmSpecies.Pigs!=AnimalSpecies.Rat,"native rat species not overwritten");
        Check(AnimalSpecies.Rat.growthStages[3]==5 && AnimalSpecies.Rat.growthStages[4]==11,"native rat stage timing unchanged");
        Check(FarmSpecies.Cattle.growthStages[3]==6 && FarmSpecies.Pigs.growthStages[3]==10,"cattle mature before pigs");
        Check(FarmSpecies.Lobsters.diet==FishDiet.Carnivore && !FarmSpecies.Lobsters.CanBreed,"lobsters are meat-only and nonbreeding");
        Check(FarmSpecies.SeaCucumbers.diet==FishDiet.Omnivore && FarmSpecies.SeaCucumbers.CanBreed,"sea cucumbers retain omnivory and breeding");
        Check(FarmSpecies.SeaCucumbers.remediationMlPer100gPerNight==0,"native multi-pollutant remediation disabled for sea cucumbers");
        foreach(string species in new[]{FarmRules.Cow,FarmRules.Pig}) foreach(string sex in new[]{"MALE","FEMALE"})
        {
            int produced=0,clock=0;int adult=species==FarmRules.Cow?6:10,elder=species==FarmRules.Cow?26:22,interval=species==FarmRules.Cow?2:3;
            for(int age=0;age<40;age++)
            {
                bool eligible=FarmRules.Produces(species,sex,age,adult,elder,true,true);
                if(eligible && ++clock>=interval){produced++;clock=0;}
                Check(!FarmRules.Produces(species,sex,age,adult,elder,false,true),"unfed animals cannot produce");
                Check(!FarmRules.Produces(species,sex,age,adult,elder,true,false),"unhealthy animals cannot produce");
            }
            Check(produced==(species==FarmRules.Cow?(sex=="FEMALE"?10:0):(sex=="MALE"?4:0)),"full lifecycle product count by sex");
        }
        double lastCleanup=0;int lastCalories=0;
        for(int nights=0;nights<=3650;nights++)
        {
            int calories=FarmRules.SeaCalories(nights);double cleanup=FarmRules.SeaCleanupMl(nights);
            Check(calories>=lastCalories && cleanup>=lastCleanup,"longer rearing never reduces accrued sea-cucumber benefits");
            Check(!double.IsNaN(cleanup) && cleanup<40 && calories<5000,"long-lived sea-cucumber curve stays finite and restrained");
            lastCalories=calories;lastCleanup=cleanup;
        }
        Check(FarmRules.SeaCleanupMl(40)-FarmRules.SeaCleanupMl(20)<FarmRules.SeaCleanupMl(20)-FarmRules.SeaCleanupMl(0),"later cleanup gains taper");
        Check(FarmRules.SeaCalories(40)-FarmRules.SeaCalories(20)<FarmRules.SeaCalories(20)-FarmRules.SeaCalories(0),"later nutrition gains taper");
        foreach(string species in new[]{FarmRules.Cow,FarmRules.Pig,FarmRules.Lobster}) for(int kcal=0;kcal<=10000;kcal+=73)
        {
            var cuts=FarmRules.SplitCalories(species,kcal);
            Check(cuts.Sum()==kcal,"slaughter conserves total edible calories");
            foreach(int cut in cuts){Check(cut>0 && cut<=900,"bounded valid cut");Check(FarmGeometry.Shape(FarmSpecies.MeatId(species,cut).Substring(8),out _,out _),"every cut has a real shape mapping");}
        }
        Check(FarmSpecies.MeatId(FarmRules.Pig,900)=="pelican_pork_large","large pork gets its own item identifier");
        Check(FarmRules.MeatPrice(FarmRules.Lobster,900)>FarmRules.MeatPrice(FarmRules.Pig,900),"lobster meat is the premium meat route");
        for(int mask=0;mask<32;mask++) for(int day=1;day<=14;day++)
        {
            bool farming=(mask&1)!=0,mammals=(mask&2)!=0,aq=(mask&4)!=0,advanced=(mask&8)!=0,dna=(mask&16)!=0;
            var stock=FarmStock.Quantities(day,farming,mammals,aq,advanced,dna,true);
            if(day%7!=3 && day%7!=5 && day%7!=6) Check(stock.Values.Sum()==0,"no off-day stock");
            if(day%7==6)
            {
                Check(FarmStock.Entries.Where(e=>e.Seed>=0).All(e=>stock[e.Key]==0),"Saturday never sells seeds");
                Check(stock["nutrifruit_small"]==0,"Saturday never carries Nutrifruit at any capability level");
                Check(stock["pelican_pig_MALE"]==(mammals?1:0) && stock["pelican_pig_FEMALE"]==(mammals?1:0),"independently gated male/female livestock");
                Check(stock["pelican_sea_cucumber_roe"]==(aq?1:0),"sea-cucumber eggs require base aquaculture");
                Check(stock["pelican_lobster_roe"]==(aq&&advanced?1:0),"lobster eggs require advanced aquaculture");
                Check(stock["industrial_fish_feed"]==(aq?(advanced?2:1):0),"feed quantities upgrade only for aquaculture");
            }
            if(day%7==3) Check(stock["pelican_cow_MALE"]==0 && stock["eel"]==0,"Wednesday stock contains no livestock");
            Check(FarmRules.VisitDay(day,farming,mammals,aq)==(((day%7==3||day%7==5)&&farming)||(day%7==6&&(mammals||aq))),"ability-dependent visit scheduling");
        }
        var noBonus=FarmStock.Quantities(3,true,true,true,true,true,false);
        Check(noBonus["nutrifruit_small"]==0 && noBonus["industrial_fish_feed"]==0,"one failed Wednesday bonus gives no supplementary bundle");
        Check(FarmRules.CanPayProduction(true,900,1200) && !FarmRules.CanPayProduction(true,600,1200),"milk production spends nutrition without exhausting the animal");
        Check(FarmRules.SeaCalories(10)==424 && FarmRules.SeaCalories(20)==683 && FarmRules.SeaCalories(30)==884 && FarmRules.SeaCalories(29)==866,"approved well-fed sea-cucumber milestones");
        Check(FarmSpecies.Lobsters.stageStartDays[4]==14 && FarmSpecies.Lobsters.oldAgeDays==45 && FarmSpecies.SeaCucumbers.stageStartDays[4]==8,"faster aquatic maturity preserves lobster mortality timing");
        Check((int)(AquacultureHelper.GetExpectedKcalEatenAtAge(FarmSpecies.Lobsters,30)*FarmSpecies.Lobsters.conversionEfficiency)==3281,"native fully-fed calculation yields the proposed day-30 lobster meat");
        Check(AquacultureHelper.GetExpectedKcalEatenAtAge(FarmSpecies.SeaCucumbers,30)>1200,"faster sea-cucumber growth includes increased real feed costs");
        Placement();TransitionMetadata();
        return "Passed "+checks+" husbandry checks (sex/stages, stock gates, tapered growth, placement and metadata conservation).";
    }
    private static void Placement()
    {
        GridShape open=new GridShapeBuilder().SetData(new byte[4],2).Build();
        var original=new Item("fry");var children=new List<GameItem>();for(int i=0;i<6;i++) children.Add(new Item("fish_"+i));
        Check(FarmFishSafety.Plan(open,new[]{original},original,children)==null,"six fry cannot disappear into four occupied cells");
        Check(original.unitCount==1 && children.All(s=>s.unitCount==1),"planning never destroys original or temporary objects");
        children.RemoveRange(3,3);var plan=FarmFishSafety.Plan(open,new[]{original},original,children);
        Check(plan!=null && plan.Count==3,"original group footprint is released when planning complete replacement");
        var cells=new HashSet<string>();foreach(var shape in plan) for(int y=shape.minY;y<=shape.maxY;y++) for(int x=shape.minX;x<=shape.maxX;x++) if(shape.Get(x,y)==1) Check(cells.Add(x+","+y),"new fish placements do not overlap");
        var blocker=new Item("kept",2,2);
        Check(FarmFishSafety.Plan(open,new GameItem[]{original,blocker},original,children)==null,"existing unrelated fish are not displaced or deleted");
    }
    private static void TransitionMetadata()
    {
        var source=new Item("group");source.EnableTag("FISH_GROUP_TAG");source.EnableTag("FISH_FRY_TAG");
        FarmSpecies.SetString(source,"FISH_SPECIES_TAG",FishSpecies.Tilapia.speciesID);Crops.SetInt(source,"FISH_GROUP_COUNT_TAG",6);
        FarmSpecies.SetFloat(source,"FISH_KCAL_EATEN_TAG",1200);FarmSpecies.SetFloat(source,"FISH_KCAL_GROWN_TAG",900);FarmSpecies.SetFloat(source,"FISH_KCAL_PELLET_TAG",120);
        FarmSpecies.SetFloat(source,"FISH_KCAL_INDUSTRIAL_TAG",300);FarmSpecies.SetFloat(source,"FISH_HUNGER_TAG",600);FarmSpecies.SetFloat(source,"FISH_HEALTH_TAG",75);
        Crops.SetInt(source,"CALORIE_VALUE_TAG",60);
        Crops.SetInt(source,"OTHER_MOD_DATA",992);Crops.SetInt(source,"FISH_AGE_TAG",8);
        var child=new Item("individual");FarmSpecies.SetString(child,"FISH_SEX_TAG","MALE");
        FarmFishSafety.Prepare(source,child,FishSpecies.Tilapia,6,false);
        Check(child.IsTag("FISH_ITEM_TAG") && !child.IsTag("FISH_GROUP_TAG") && !child.IsTag("FISH_FRY_TAG"),"conversion changes only life-stage classification");
        Check(!child.IsTag("CALORIE_VALUE_TAG"),"roe food-calorie classification does not leak onto live offspring");
        Check(child.GetTagReadonly("FISH_KCAL_EATEN_TAG").GetFloat()*6==1200 && child.GetTagReadonly("FISH_HUNGER_TAG").GetFloat()*6==600,"nutrition and hunger conserved across all offspring");
        Check(child.GetTagReadonly("FISH_KCAL_INDUSTRIAL_TAG").GetFloat()*6==300 && child.GetTagReadonly("FISH_KCAL_PELLET_TAG").GetFloat()*6==120,"feed provenance conserved");
        Check(child.GetTagReadonly("FISH_HEALTH_TAG").GetFloat()==75 && child.GetTagReadonly("OTHER_MOD_DATA").GetInt()==992 && child.GetTagReadonly("FISH_AGE_TAG").GetInt()==8,"health, age and other-mod metadata survive conversion");
        Check(child.GetTagReadonly("FISH_SEX_TAG").GetString()=="MALE","factory-assigned sex survives group-state copying");
        Crops.SetInt(child,"OTHER_MOD_DATA",1);Check(source.GetTagReadonly("OTHER_MOD_DATA").GetInt()==992,"child state does not alias original state");
    }
    public static string RunPatchedIntegration()
    {
        int before=checks;bool old=ModEntry.Enabled;ModEntry.Enabled=true;
        var fish=new Item("sea");FarmSpecies.SetString(fish,"FISH_SPECIES_TAG",FarmRules.Sea);Crops.SetInt(fish,FarmRules.FedNights,20);
        Check(AquacultureHelper.GetMeatKcal(fish)==683,"actual patched native sea-cucumber calorie query uses the revised whole-harvest curve");
        fish.EnableTag("FISH_IS_DEAD_TAG");Crops.SetInt(fish,"FISH_CARCASS_KCAL_TAG",43);
        Check(AquacultureHelper.GetMeatKcal(fish)==43,"a partly eaten sea-cucumber carcass cannot regenerate its missing calories");
        Check(AquacultureItems.GetMeatKcal("pelican_lobster_meat_large")==900,"native lookup understands custom lobster calories");
        Check(AquacultureItems.GetMeatValue("pelican_lobster_meat_large")==180,"native lookup understands the revised lobster price");
        PresentationMigration();
        TooltipCorrections();
        foreach(var species in FishSpecies.All.Values)
        {
            int count=Math.Max(1,species.groupCount);
            float native=(float)AccessTools.Method(typeof(AquacultureHelper),"GetFryGainPerNight").Invoke(null,new object[]{species,count})/count;
            Check(FarmRules.FryTransitionGrams(species,count)==native,"safe conversion retains the native final-night larval growth/feed charge");
        }
        var shape=(GridShape)AccessTools.Method(typeof(AquacultureHelper),"GetSpriteShape").Invoke(null,new object[]{ModEntry.ModId,"lobster_large",false,0});
        Check(shape.width*shape.height==6,"actual patched native aquatic geometry uses six lobster cells");
        var manager=new StoreClientManager();manager.clientStack=new List<StoreClient>();
        Merchant.ReconcileSchedule(manager,()=>new StoreClient{identifier=Merchant.Id},3,-1,false,false,true,true);
        Check(manager.clientStack.Count==0,"actual merchant queue respects missing farming unlock on Wednesday");
        Merchant.ReconcileSchedule(manager,()=>new StoreClient{identifier=Merchant.Id},6,-1,false,false,true,false);
        Check(manager.clientStack.Count==1,"actual merchant queue permits mammal-only Saturday");
        ModEntry.Enabled=false;Check(FarmShapePatch.Prefix(ModEntry.ModId,"lobster_large",false,0,ref shape),"disabled mod forwards native shape handling");ModEntry.Enabled=old;
        return "Passed "+(checks-before)+" patched husbandry integration checks, including the actual native aquatic geometry callback.";
    }
    private static void PresentationMigration()
    {
        var ids=new[]{"beef_tiny","beef_medium","beef_large","pork_tiny","pork_medium","pork_large","lobster_meat_tiny","lobster_meat_small","lobster_meat_medium","lobster_meat_large"};
        var calories=new[]{100,400,900,100,400,900,100,200,400,900};
        var prices=new long[]{14,58,130,17,67,150,20,40,80,180};
        for(int i=0;i<ids.Length;i++)
        {
            string key=ids[i],species=key.StartsWith("beef")?FarmRules.Cow:key.StartsWith("pork")?FarmRules.Pig:FarmRules.Lobster;
            FarmGeometry.Shape(key,out int w,out int h);
            var item=new Item("pelican_"+key,w,h){name="Old calorie name",shortDescription="Old description",flavorText="Old flavor",unitValue=999,unitCount=2};
            FoodItemHelper.InitFoodItem(item,calories[i],false,true);FarmSpecies.SetString(item,"MEAT_TYPE_TAG",species);
            item.EnableTag("ANIMAL_PRION_TAG");item.EnableTag("ANIMAL_DISEASED_TAG");Crops.SetInt(item,"OTHER_MOD_DATA",71);
            GeneralHelper.SetItemOwned(item,true);
            // Simulate the old save's missing beef luxury and excessive lobster luxury.
            if(species!=FarmRules.Cow) item.SetGameItemType("LUXURY_ITEM");
            FarmPresentation.RefreshFood(item);
            Check(item.name=="farm_"+key,"loaded cuts resolve a size-specific name without embedding calorie values");
            Check(item.IsGameItemType("LUXURY_ITEM")== (w*h==6),"only six-cell cuts are luxury after loading an old item");
            Check(item.IsGameItemType("FOOD") && item.unitValue==prices[i],"loaded cuts retain food classification and use approved proportional prices");
            Check((calories[i]==100)?item.shortDescription=="" && item.flavorText=="":item.shortDescription.EndsWith("_desc") && item.flavorText.EndsWith("_flavor"),"bite cuts have no narrative; larger cuts have description and flavor");
            Check(item.unitCount==2 && item.modifiedShape.width==w && item.modifiedShape.height==h && item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==calories[i],"metadata repair preserves quantity, footprint and edible calories");
            Check(GeneralHelper.IsItemOwned(item) && item.GetTagReadonly("MEAT_TYPE_TAG").GetString()==species && item.IsTag("ANIMAL_PRION_TAG") && item.IsTag("ANIMAL_DISEASED_TAG") && item.GetTagReadonly("OTHER_MOD_DATA").GetInt()==71,"repair preserves ownership, meat source, disease and foreign data");
            FarmPresentation.RefreshFood(item);Check(item.unitValue==prices[i],"reloading never compounds price changes");
            Check(AquacultureItems.GetMeatValue(item.identifier)==prices[i],"native yield valuation agrees with the new cut prices");
        }
        var partial=new Item("pelican_beef_large",3,2);FoodItemHelper.InitFoodItem(partial,137,false,true);
        FarmPresentation.RefreshFood(partial);
        Check(partial.name=="farm_beef_large" && partial.IsGameItemType("LUXURY_ITEM") && partial.unitValue==20 && partial.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==137,"partly depleted large cut retains its size and cannot regain missing calories or full value");
        foreach(bool small in new[]{true,false})
        {
            var truffle=new Item(small?"pelican_truffle_small":"pelican_truffle");FoodItemHelper.InitFoodItem(truffle,small?125:300,false);
            truffle.unitValue=small?60:120;truffle.SetGameItemType("LUXURY_ITEM");FarmPresentation.RefreshFood(truffle);
            Check(truffle.unitValue==(small?45:90) && truffle.IsGameItemType("LUXURY_ITEM") && truffle.flavorText.EndsWith("_flavor"),"saved truffles get new prices and flavor while retaining luxury classification");
        }
        var sea=new Item("pelican_sea_cucumber_food",2,2){unitValue=8};FoodItemHelper.InitFoodItem(sea,43,false,true);
        Crops.SetInt(sea,FarmRules.FedNights,20);Crops.SetInt(sea,"PELICAN_SEA_HARVEST_KCAL",43);Crops.SetInt(sea,"PELICAN_SEA_HARVEST_STAGE",4);
        FarmPresentation.RefreshFood(sea);
        Check(sea.unitValue==8 && sea.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()==43 && sea.GetTagReadonly(FarmRules.FedNights).GetInt()==20 && sea.GetTagReadonly("PELICAN_SEA_HARVEST_KCAL").GetInt()==43 && sea.GetTagReadonly("PELICAN_SEA_HARVEST_STAGE").GetInt()==4,"sea-cucumber narrative update leaves its saved harvest snapshot and value intact");
        var native=new Item("raw_meat"){name="Native meat",shortDescription="Native description",flavorText="Native flavor",unitValue=159};
        FarmPresentation.RefreshFood(native);
        Check(native.name=="Native meat" && native.shortDescription=="Native description" && native.flavorText=="Native flavor" && native.unitValue==159,"native items are not changed by farm metadata repair");
        Check(!FarmPresentation.TryMeat("pelican_beef_unknown",out _,out _),"unknown identifiers are not interpreted as registered meat cuts");
    }
    private static string[] TooltipSlices(RichTextBuilder builder)
    {
        var field=AccessTools.Field(typeof(RichTextBuilder),"slices");
        var text=AccessTools.Field(field.FieldType.GetGenericArguments()[0],"text");
        return ((System.Collections.IList)field.GetValue(builder)).Cast<object>().Select(s=>(string)text.GetValue(s)).ToArray();
    }
    public static bool HeadlessAddLine(RichTextBuilder __instance,string text,ref RichTextBuilder __result)
    {
        // Native text layout calls Unity ECalls. Keep the actual private slice
        // storage, replacing only the unavailable engine-backed AddLine body.
        var field=AccessTools.Field(typeof(RichTextBuilder),"slices");var type=field.FieldType.GetGenericArguments()[0];
        var slice=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
        AccessTools.Field(type,"text").SetValue(slice,text);((System.Collections.IList)field.GetValue(__instance)).Add(slice);
        __result=__instance;return false;
    }
    private static void TooltipCorrections()
    {
        foreach(string species in new[]{FarmRules.Cow,FarmRules.Pig,FarmRules.Lobster,FarmRules.Sea})
        {
            var live=new Item("custom_live");FarmSpecies.SetString(live,FarmRules.Mammal(species)?"ANIMAL_SPECIES_TAG":"FISH_SPECIES_TAG",species);
            Check(TooltipPolicy.HideModInfo(live) && !CropModInfoPatch.Prefix(live),"added livestock and aquatic stock suppress only the late mod-information section");
        }
        foreach(string id in new[]{"pelican_beef_large","pelican_pork_tiny","pelican_lobster_meat_small","pelican_milk","pelican_truffle","pelican_sea_cucumber_food"})
            Check(TooltipPolicy.HideModInfo(new Item(id)),"added products hide the unwanted mod-information section");
        Check(!TooltipPolicy.HideModInfo(new Item("raw_meat")),"native items keep their existing mod-information behavior");
        var fixture=new Harmony("pelican_cultivation.tooltip_fixture");
        fixture.Patch(AccessTools.Method(typeof(RichTextBuilder),"AddLine"),new HarmonyMethod(typeof(FarmVerification).GetMethod("HeadlessAddLine")));
        try{
        var lobster=new Item("lobster");Crops.SetInt(lobster,"FISH_AGE_TAG",30);
        var builder=new RichTextBuilder(40);builder.AddLine("native status");builder.AddLine("aquaculture_old_age_in");
        FarmAgeTooltip.Postfix(builder,lobster,FarmSpecies.Lobsters);
        var text=TooltipSlices(builder);
        Check(Array.IndexOf(text,"native status")>=0 && Array.IndexOf(text,"aquaculture_old_age")>=0 && Array.IndexOf(text,"aquaculture_old_age_in")<0,"elder lobster no longer says it will enter old age later; native rows remain");
        Check(FarmSpecies.Lobsters.oldAgeDays==45 && lobster.GetTagReadonly("FISH_AGE_TAG").GetInt()==30,"tooltip correction does not change age or mortality threshold");
        var native=new RichTextBuilder(40);native.AddLine("aquaculture_old_age_in");FarmAgeTooltip.Postfix(native,lobster,FishSpecies.Tilapia);
        Check(Array.IndexOf(TooltipSlices(native),"aquaculture_old_age_in")>=0,"native aquatic species tooltips are unchanged");
        Crops.SetInt(lobster,"FISH_AGE_TAG",29);var younger=new RichTextBuilder(40);younger.AddLine("aquaculture_old_age_in");FarmAgeTooltip.Postfix(younger,lobster,FarmSpecies.Lobsters);
        Check(Array.IndexOf(TooltipSlices(younger),"aquaculture_old_age_in")>=0,"young lobster's existing progression line is retained");
        bool enabled=ModEntry.Enabled;ModEntry.Enabled=false;
        Check(CropModInfoPatch.Prefix(new Item("pelican_milk")),"disabled mod forwards native mod-information hooks");ModEntry.Enabled=enabled;
        }finally{fixture.UnpatchAll(fixture.Id);}
    }
    public static string ValidatePatchBindings()
    {
        int count=0;
        foreach(var type in typeof(ModEntry).Assembly.GetTypes())
        {
            var attributes=type.GetCustomAttributes(typeof(HarmonyPatch),false);if(attributes.Length==0) continue;
            var info=((HarmonyPatch)attributes[0]).info;
            var target=AccessTools.Method(info.declaringType,info.methodName,info.argumentTypes);
            if(target==null) throw new Exception("Patch target missing: "+type.FullName);
            foreach(var patch in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Where(m=>m.Name=="Prefix"||m.Name=="Postfix"||m.Name=="Finalizer"))
            foreach(var parameter in patch.GetParameters())
            {
                if(parameter.Name.StartsWith("__")) continue;
                var original=target.GetParameters().FirstOrDefault(p=>p.Name==parameter.Name);
                if(original==null) throw new Exception("Patch parameter missing: "+type.Name+"."+parameter.Name);
                Type actual=parameter.ParameterType.IsByRef?parameter.ParameterType.GetElementType():parameter.ParameterType;
                Type expected=original.ParameterType.IsByRef?original.ParameterType.GetElementType():original.ParameterType;
                if(!actual.IsAssignableFrom(expected)) throw new Exception("Patch parameter type mismatch: "+type.Name+"."+parameter.Name);
            }
            count++;
        }
        return "Validated "+count+" Harmony target and named-parameter bindings against the installed game.";
    }
}
