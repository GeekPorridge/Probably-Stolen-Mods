using System;
using System.Collections.Generic;
using System.Linq;

namespace PelicanCultivation
{
    public sealed class ManualBookData
    {
        public string title;public string[] subtitle,body,images;public int pageCount;
    }
    public static class Books
    {
        public const float BodyFontSize=24;
        public static void CloseAll()=>ManualWindow.CloseAll();
        public static bool IsManual(GameItem item)=>item!=null&&new[]{"pelican_plant_manual","pelican_animal_manual","pelican_artisan_manual"}.Contains(item.identifier);
        public static GameItem Item(string key)
        {
            var item=ItemDirectory.CreateEmptyItem("pelican_"+key);Crops.Sprite(item,key,2,3);item.name=Expansion.L(key);item.shortDescription=Expansion.L("manual_desc");item.unitValue=20;item.SetGameItemType("DOCUMENT");
            item.mayActivateSlotItemFunc=(self,inv,slot)=>ModEntry.Enabled&&GeneralHelper.IsItemOwned(self);
            item.canActivateSlotItemFunc=(self,inv,slot)=>ModEntry.Enabled&&GeneralHelper.IsItemOwned(self);
            BookHelper.InitGuide(item);
            item.onLoaded+=self=>{self.DisableTag("BOOK_TAG");self.DisableTag("BOOK_ID");};
            item.onActivateSlotItemFunc=(self,inv,slot)=>Open(key,self);return item;
        }
        private sealed class Draft
        {
            public readonly List<string> Titles=new List<string>(),Bodies=new List<string>(),Images=new List<string>();
            public string Cover;
            public Func<string,bool> Fits;
        }
        private static void Page(Draft book,string title,string body,string sprite=null)
        {foreach(string part in Paginate(body,book.Fits)){book.Titles.Add(title);book.Bodies.Add(part);book.Images.Add(sprite??book.Cover);}}
        // Runtime uses the native TMP font and book-body rectangle. The fallback
        // is for headless validation only, never a substitute for font shrinking.
        public static List<string> Paginate(string body,Func<string,bool> fits=null)
        {
            fits=fits??(text=>text.Length<=160);
            var result=new List<string>();string remaining=(body??"").Trim();
            while(remaining.Length>0)
            {
                if(fits(remaining)){result.Add(remaining);break;}
                int lo=1,hi=remaining.Length;
                while(lo<hi){int mid=(lo+hi+1)/2;if(fits(remaining.Substring(0,mid)))lo=mid;else hi=mid-1;}
                int end=lo;
                if(end<remaining.Length&&char.IsHighSurrogate(remaining[end-1]))end--;
                for(int n=end-1;n>=Math.Max(1,end/2);n--)
                    if(remaining[n]=='\n'||remaining[n]==' '||remaining[n]=='。'||remaining[n]=='.'||remaining[n]=='；'){end=n+1;break;}
                end=Math.Max(1,end);result.Add(remaining.Substring(0,end).Trim());remaining=remaining.Substring(end).TrimStart();
            }
            if(result.Count==0)result.Add("");return result;
        }
        public static void Open(string key,GameItem item)
        {
            Progress.EnsureReady();
            if(!ModEntry.Enabled||!Progress.Ready){ModEntry.Note("Manual unavailable: "+Progress.RuntimeState);return;}
            ManualWindow.Open(key,item);
        }
        public static ManualBookData Build(string key,Func<string,bool> fits=null)
        {
            var book=new Draft{Fits=fits,Cover=key};
            Page(book,Expansion.L(key),Expansion.L("book_intro_"+key),key);
            if(key=="plant_manual")
            {
                foreach(var crop in Rules.Crops)
                {
                    var seed=DirectoryMaster.Item(crop.SeedId,false);int first=HydroponicHelper.GetRequiredProgress(seed),yield=HydroponicHelper.GetBaseHarvestQuantity(seed);Crops.SetInt(seed,HydroponicHelper.PERENNIAL_ESTABLISHED_INT,1);
                    int again=crop.Regrowth>0?HydroponicHelper.GetRequiredProgress(seed):0;
                    string cycle=again>0?Expansion.L("plant_cycle_perennial",Rules.Days(first,8),Rules.Days(again,8)):Expansion.L("plant_cycle_annual",Rules.Days(first,8));
                    string use=Expansion.Flower(crop)?Expansion.L("plant_use_perfume"):crop.WineKind>0?Expansion.L("plant_use_wine",ModEntry.L("wine_"+Rules.WineKeys[crop.WineKind])):Expansion.L("plant_use_food");
                    string body=cycle+"\n\n"+Expansion.L("plant_basics",yield,HydroponicHelper.GetWaterUsePerCycle(seed))+"\n\n"+use;
                    if(crop.Sized)body+="\n\n"+Expansion.L("plant_giants");
                    if(crop.Key=="ancient_fruit")body+="\n\n"+Expansion.L("plant_ancient");
                    seed.Destroy();Page(book,ModEntry.L("crop_"+crop.Key),body,crop.Key);
                }
                if(NetworkUpgrade.IsPlantDnaUnlocked())
                {
                    Page(book,Expansion.L("plant_genetics"),Expansion.L("plant_genetics_body"));
                    foreach(var trait in PlantTraitRegistry.All)
                    {
                        string description=trait.description??"";if(trait.describeRank!=null)description+="\n\n"+trait.describeRank(1);
                        description+="\n\n"+Expansion.L("trait_rank",trait.IsUnlimited?Expansion.L("unlimited"):trait.maxRank.ToString(),trait.complexity);
                        Page(book,trait.displayName,description);
                    }
                }
            }
            else if(key=="animal_manual")
            {
                foreach(string animal in new[]{"cow","pig","chicken","goat","lobster","sea_cucumber"})Page(book,animal=="chicken"||animal=="goat"?Expansion.L(animal):ModEntry.L("farm_"+animal),Expansion.L("book_animal_"+animal),animal+"_"+(animal=="lobster"||animal=="sea_cucumber"?"large":"female"));
                foreach(string fish in ExpansionStock.FishKeys)Page(book,Expansion.L(fish),Expansion.L("book_fish_"+fish),fish+"_large");
                Page(book,Expansion.L("fish_breeding"),Expansion.L("fish_breeding_body"));
                if(NetworkUpgrade.IsPlantDnaUnlocked())Page(book,Expansion.L("animal_genetics"),Expansion.L("animal_genetics_body"));
            }
            else
            {
                foreach(string machine in ArtisanMachines.Keys)Page(book,Expansion.L(machine),Expansion.L("book_machine_"+machine),machine);
                Page(book,Expansion.L("artisan_dairy"),Expansion.L("book_dairy"),"goat_cheese");
                Page(book,Expansion.L("artisan_perfume"),Expansion.L("book_perfume"),"fairy_perfume");
                Page(book,Expansion.L("artisan_grill"),Expansion.L("book_grill"),"cooked_beef");
                Page(book,Expansion.L("artisan_bait"),Expansion.L("book_bait"),"premium_bait");
                Page(book,Expansion.L("reservoir_title"),Expansion.L("book_fishing"),"legend");
                Page(book,Expansion.L("king_activity_night"),Expansion.L("book_reservoir_events"),"legend");
                Page(book,Expansion.L("fish_kings"),Expansion.L("book_kings"));
            }
            return new ManualBookData{title=Expansion.L(key),subtitle=book.Titles.ToArray(),body=book.Bodies.ToArray(),images=book.Images.ToArray(),pageCount=book.Bodies.Count};
        }
    }
}
