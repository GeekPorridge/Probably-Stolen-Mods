using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PelicanCultivation
{
    public static class Fishing
    {
        public const string LocationId="pelican_cultivation.reservoir";
        public static readonly string[] Kings={"legend","crimsonfish","glacierfish","mutant_carp"};
        public static readonly int[] KingPrices={1600,900,1000,1100},Cooldowns={12,8,8,8};
        public static readonly double[] KingChances={0.5,1,1,1};
        private static CustomUIElement status,counters,cast,claim,normalChoice,premiumChoice,bar;
        private static CustomUIWindow window;
        private static RectTransform cursor,target;
        private static int choice=-1;
        private static int fightDifficulty=FishingDifficulty.Default;
        private static readonly float[] ordinaryWidths={0.24f,0.20f,0.16f,0.13f,0.10f},kingWidthScales={1.60f,1.25f,1f,0.85f,0.70f};
        private static readonly float[] ordinarySpeeds={0.30f,0.36f,0.42f,0.49f,0.56f},kingSpeeds={0.52f,0.64f,0.75f,0.87f,1.00f};
        private static bool active,premiumCast,paused,panelNeedsLayout,rebuilding;
        private static float starts,lastTick,tension,reeled,danger,lastRefresh,pauseStarted,nextRepair;

        public static void Enable()
        {
            ReservoirLocation.Register();ReservoirEvents.Enable();
        }
        public static void Disable(){Leave();ReservoirStage.Restore();ReservoirEvents.Disable();ModHelper.RemoveAfterhourLocations(ModEntry.ModId);}
        public static int Ordinary(double roll)=>roll<0.45?0:roll<0.75?1:roll<0.90?2:3;
        public static int Select(double roll,double ordinaryRoll,bool premium,int day,int[] readyDays,double eventMultiplier=1)
        {
            double value=Math.Max(0,Math.Min(0.999999999,roll))*100,edge=0;
            for(int i=0;i<4;i++){edge+=KingChances[i]*(premium?1.5:1)*Math.Max(1,eventMultiplier);if(value<edge)return day>=readyDays[i]?4+i:Ordinary(ordinaryRoll);}
            if(value<edge+(premium?3:8))return 8;return Ordinary(ordinaryRoll);
        }
        private static bool King(int fish)=>fish>=4&&fish<8;
        public static float ZoneWidth(int fish,int level=FishingDifficulty.Default)
        {
            int rank=FishingDifficulty.Clamp(level)-1;
            if(!King(fish))return ordinaryWidths[rank];
            float width=fish==4?0.10f:fish==5?0.13f:fish==6?0.12f:0.11f;
            return width*kingWidthScales[rank];
        }
        public static float TargetSpeed(int fish,int level=FishingDifficulty.Default)
        {
            int rank=FishingDifficulty.Clamp(level)-1;
            return King(fish)?kingSpeeds[rank]:ordinarySpeeds[rank];
        }
        public static float CursorSpeed(int fish,int level=FishingDifficulty.Default)
            =>TargetSpeed(fish,level)*(1-ZoneWidth(fish,level))*1.35f+0.10f;
        public static float TargetCenter(int fish,float elapsed,int level=FishingDifficulty.Default)
        {float width=ZoneWidth(fish,level);return width/2+(1-width)*Mathf.PingPong(Math.Max(0,elapsed)*TargetSpeed(fish,level)+0.5f,1);}
        public static bool Hit(float position,float center,float width)=>Math.Abs(position-center)<=width*0.5f;
        public static float Pull(float value,bool holding,float elapsed,float seconds,int fish,int level=FishingDifficulty.Default)
            => Mathf.Clamp01(value+((holding?1:-1)*CursorSpeed(fish,level)+(float)Math.Sin(elapsed*(King(fish)?2.4f:1.8f))*0.06f)*seconds);
        public static float ReelProgress(float value,float pull,float seconds,int fish,float elapsed=0,int level=FishingDifficulty.Default)
            => Mathf.Clamp01(value+(Hit(pull,TargetCenter(fish,elapsed,level),ZoneWidth(fish,level))?1f/(King(fish)?8:5):-0.10f)*seconds);
        public static int Remaining=>Math.Max(0,Progress.Get("fishing_limit")-Progress.Get("fishing_used"));
        private static void EnsureNight()
        {
            if(!Progress.Ready)return;
            // Initialize once from the actual backpack, after native departure transfer.
            // Reopening or revisiting cannot refill the night's quota.
            if(Progress.Get("fishing_quota_day",-1)==Progress.Day)return;
            Progress.Set("fishing_quota_day",Progress.Day);Progress.Set("fishing_used",0);
            Progress.Set("fishing_limit",CountBait(false)+CountBait(true));
        }
        public static void Arrive(){Leave();EnsureNight();TryClaim();}
        public static void Leave()
        {
            active=paused=false;choice=-1;ClearPanel();
        }
        private static void ClearPanel()
        {
            cursor=target=null;status=counters=cast=claim=normalChoice=premiumChoice=bar=null;window=null;
        }
        public static void Build(CustomUIBuilder builder)
        {
            EnsureNight();window=builder.Handle;panelNeedsLayout=true;var built=window;
            builder.UseMetaPrefabs().SetSize(490,360).SetDraggable(false).SetCloseOnEscape(false).OnClose(()=>{if(ReferenceEquals(window,built))ClearPanel();});
            builder.AddLabel("","pelican_fish_count");counters=builder.Last;
            if(ReservoirEvents.Multiplier>1)builder.AddLabel(Expansion.L("king_activity_night"));
            builder.BeginRow();builder.AddButton("",()=>SelectBait(false),"pelican_bait_normal");normalChoice=builder.Last.SetFixedWidth(210);
            builder.AddButton("",()=>SelectBait(true),"pelican_bait_premium");premiumChoice=builder.Last.SetFixedWidth(210);builder.End();
            builder.AddLabel(Expansion.L("fishing_idle"),"pelican_fish_status");status=builder.Last;
            var track=new GameObject("PelicanFishingTension",typeof(RectTransform),typeof(LayoutElement),typeof(Image));
            var trackRect=track.GetComponent<RectTransform>();trackRect.SetParent(builder.CurrentContainer,false);
            var layout=track.GetComponent<LayoutElement>();layout.minHeight=30;layout.preferredHeight=30;layout.minWidth=300;layout.preferredWidth=450;layout.flexibleWidth=1;
            var trackImage=track.GetComponent<Image>();trackImage.color=new Color(0.08f,0.12f,0.16f,1);trackImage.raycastTarget=false;
            bar=CustomUIElement.Bind(track,"pelican_fish_bar");
            float idleWidth=ZoneWidth(0,FishingDifficulty.Current);
            target=Stripe("PelicanFishTarget",new Color(0.18f,0.65f,0.46f,0.85f),0.5f-idleWidth/2,0.5f+idleWidth/2);
            cursor=Stripe("PelicanFishTension",new Color(1,0.87f,0.52f,1),0.5f,0.5f);if(cursor!=null)cursor.sizeDelta=new Vector2(4,0);
            builder.BeginRow();builder.AddButton(Expansion.L("cast"),Cast,"pelican_fish_cast");cast=builder.Last.SetFixedWidth(160);
            builder.AddButton(Expansion.L("claim"),TryClaim,"pelican_fish_claim");claim=builder.Last.SetFixedWidth(230);builder.End();
            builder.AddLabel(Expansion.L("fishing_controls"));Refresh();
            if(active){SetTarget(Math.Max(0,(paused?pauseStarted:Time.unscaledTime)-starts));SetTension();status?.SetText(Expansion.L("fishing_tension",(int)(tension*100),(int)(reeled*100)));}
        }
        public static void FinalizePanel()
        {
            // Keep the native bottom Return as the single exit control. A
            // destroyed/hidden window can still be rebuilt by RestorePanel.
            if(window?.frame?.closeButton!=null)
            {window.frame.closeButton.onClick=new Button.ButtonClickedEvent();window.frame.closeButton.interactable=false;window.frame.closeButton.gameObject.SetActive(false);}
            DockPanel();
        }
        public static void ReturnToStore(){var map=MapUIManager.Instance;if(map!=null&&map.Current?.Id==LocationId)map.Return();}
        public static float ViewScale(float viewWidth,float viewHeight,float panelWidth,float panelHeight)
            =>Math.Min(1,Math.Min(Math.Max(1,viewWidth-24)/Math.Max(1,panelWidth),Math.Max(1,viewHeight-24)/Math.Max(1,panelHeight)));
        public static void DockPanel()
        {
            var rect=window?.Rect;var parent=rect?.parent as RectTransform;if(rect==null||parent==null)return;
            if(panelNeedsLayout)
            {
                Canvas.ForceUpdateCanvases();var content=window.ContentRoot;LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                float inset=Math.Max(0,rect.rect.height-content.rect.height);
                rect.sizeDelta=new Vector2(490,Math.Max(360,LayoutUtility.GetPreferredHeight(content)+inset+10));panelNeedsLayout=false;
            }
            var canvas=rect.GetComponentInParent<Canvas>()?.rootCanvas;if(canvas==null)return;
            Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:(canvas.worldCamera??Camera.main);
            var pixels=canvas.pixelRect;Vector2 low,high;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,pixels.min,camera,out low)
                ||!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,pixels.max,camera,out high))return;
            float left=Math.Min(low.x,high.x),right=Math.Max(low.x,high.x),bottom=Math.Min(low.y,high.y),top=Math.Max(low.y,high.y);
            float scale=ViewScale(right-left,top-bottom,rect.rect.width,rect.rect.height);
            rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(1,0);rect.localScale=new Vector3(scale,scale,1);
            rect.localPosition=new Vector3(right-12,bottom+12,0);
        }
        private static bool RestorePanel(float now)
        {
            if(window?.IsAlive==true){if(!window.IsVisible)window.Show();return true;}
            UpdatePause(true,now);
            var map=MapUIManager.Instance;
            if(map==null||map.Current?.Id!=LocationId||map.IsInTransit||rebuilding||now<nextRepair)return false;
            nextRepair=now+0.5f;rebuilding=true;
            try{AccessTools.Method(typeof(MapUIManager),"OpenLocationPanel").Invoke(map,new object[]{map.Current});}
            catch(Exception e){ModEntry.Error("Reopen night fishing",e);}
            finally{rebuilding=false;}
            return window?.IsAlive==true;
        }
        private static RectTransform Stripe(string name,Color color,float left,float right)
        {
            if(bar?.Rect==null)return null;var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(bar.Rect,false);rect.anchorMin=new Vector2(left,0.15f);rect.anchorMax=new Vector2(right,0.85f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return rect;
        }
        private static GameItem[] BackpackBait()
        {
            var entry=EmporiumEntry.Instance;if(ReferenceEquals(entry,null))return new GameItem[0];
            // Ground and side pockets are not the player's brought backpack.
            return GraphUtils.FindAllChildrenType<GameItem>(entry.afterhourPocketSlotInvBackpack,
                i=>i!=null&&GeneralHelper.IsItemOwned(i)&&i.unitCount>0&&(i.identifier=="pelican_bait"||i.identifier=="pelican_premium_bait"),
                i=>i!=null&&GeneralHelper.IsItemOwned(i)).ToArray();
        }
        public static int CountBait(bool premium)=>BackpackBait().Where(i=>i.identifier==(premium?"pelican_premium_bait":"pelican_bait")).Sum(i=>i.unitCount);
        private static GameItem Bait(bool premium)=>BackpackBait().FirstOrDefault(i=>i.identifier==(premium?"pelican_premium_bait":"pelican_bait"));
        private static bool Pending=>Progress.Get("fishing_pending")>0||Progress.Get("fishing_pending_roe")>0;
        public static bool SelectBait(bool premium)
        {
            if(active||Pending||!Progress.Ready)return false;
            Progress.Set("fishing_selected_bait",premium?1:0);Refresh();return true;
        }
        private static void Refresh()
        {
            if(!Progress.Ready)return;
            counters?.SetText(Expansion.L("fishing_counts",Remaining,CountBait(false),CountBait(true)));
            bool pending=Pending;
            bool selected=Progress.Get("fishing_selected_bait")==1;
            normalChoice?.SetText(Expansion.L("fishing_bait_button",selected?"":"✓ ",Expansion.L("bait"),CountBait(false)));
            premiumChoice?.SetText(Expansion.L("fishing_bait_button",selected?"✓ ":"",Expansion.L("premium_bait"),CountBait(true)));
            normalChoice?.SetInteractable(!active&&!pending);premiumChoice?.SetInteractable(!active&&!pending);
            cast?.SetInteractable(!active&&!pending&&Remaining>0&&Bait(selected)!=null);
            claim?.SetInteractable(!active&&pending);
            if(pending)status?.SetText(Expansion.L("pending_catch"));
        }
        public static void Cast()
        {
            if(active||!Progress.Ready||ModHelper.GetCurrentAfterhourLocationId()!=LocationId)return;EnsureNight();
            if(Pending){TryClaim();return;}if(Remaining<=0){status?.SetText(Expansion.L("fishing_no_attempts"));return;}
            premiumCast=Progress.Get("fishing_selected_bait")==1;
            var bait=Bait(premiumCast);if(bait==null){status?.SetText(Expansion.L("no_bait"));return;}
            // Pay at cast, binding the selected bait type for this entire fight.
            // Landing, escaping, returning and rebuilding the UI never pay again.
            Expansion.ConsumeUnits(bait,1);Progress.Set("fishing_used",Progress.Get("fishing_used")+1);
            fightDifficulty=FishingDifficulty.Current;
            ReservoirEvents.EnsureToday();
            var ready=Kings.Select(k=>Progress.Get("fishing_ready_"+k)).ToArray();choice=Select(UnityEngine.Random.value,UnityEngine.Random.value,premiumCast,Progress.Day,ready,ReservoirEvents.Multiplier);
            active=true;paused=false;tension=0.5f;reeled=danger=0;starts=Time.unscaledTime+0.35f;lastTick=starts;
            SetTarget();
            SetTension();Refresh();status?.SetText(Expansion.L("fishing_tension",50,0));
        }
        private static void SetTarget(float elapsed=0)
        {float width=ZoneWidth(choice,fightDifficulty),center=TargetCenter(choice,elapsed,fightDifficulty);if(target!=null){target.anchorMin=new Vector2(center-width/2,0.15f);target.anchorMax=new Vector2(center+width/2,0.85f);target.offsetMin=target.offsetMax=Vector2.zero;}}
        private static void SetTension()
        {if(cursor!=null){cursor.anchorMin=new Vector2(tension,0.05f);cursor.anchorMax=new Vector2(tension,0.95f);cursor.anchoredPosition=Vector2.zero;}}
        public static void Tick()
        {
            if(!Progress.Ready||ModHelper.GetCurrentAfterhourLocationId()!=LocationId){active=false;return;}
            float now=Time.unscaledTime;
            if(!RestorePanel(now))return;DockPanel();
            if(!active){if(now-lastRefresh>=0.25f){lastRefresh=now;Refresh();}return;}
            if(bar?.IsAlive!=true||window?.IsVisible!=true){UpdatePause(true,now);return;}
            var ui=StoreUIManager.Instance;
            var confirmation=GenericConfirmationUIManager.Instance;
            bool menuOpen=ui!=null&&((ui.escapeUIManager!=null&&ui.escapeUIManager.menuPanel!=null&&ui.escapeUIManager.menuPanel.activeInHierarchy)
                ||(ui.gameOptionUIManager!=null&&ui.gameOptionUIManager.IsOpen));
            menuOpen|=confirmation!=null&&confirmation.panel!=null&&confirmation.panel.activeInHierarchy;
            var map=MapUIManager.Instance;menuOpen|=map!=null&&map.IsInTransit;
            if(UpdatePause(menuOpen,now)||now<starts)return;
            Advance(Input.GetKey(KeyCode.Space)||Input.GetMouseButton(0),now);
        }
        public static bool UpdatePause(bool menuOpen,float now)
        {
            if(!active){paused=false;return false;}
            if(menuOpen){if(!paused){paused=true;pauseStarted=now;}return true;}
            if(paused)
            {
                // Shift the fight's clock, not its state. The startup delay,
                // pull phase, timeout and existing danger all resume in place.
                float duration=Math.Max(0,now-pauseStarted);starts+=duration;lastTick+=duration;paused=false;
            }
            return false;
        }
        // Independent of input sampling for deterministic native CLR checks.
        public static void Advance(bool holding,float now)
        {
            if(!active||paused||now<starts)return;
            float seconds=Math.Max(0,Math.Min(0.1f,now-lastTick));lastTick=now;
            tension=Pull(tension,holding,now-starts,seconds,choice,fightDifficulty);reeled=ReelProgress(reeled,tension,seconds,choice,now-starts,fightDifficulty);
            danger=tension<0.02f||tension>0.98f?danger+seconds:0;SetTarget(now-starts);SetTension();
            status?.SetText(Expansion.L("fishing_tension",(int)(tension*100),(int)(reeled*100)));
            status?.SetTextColor(Hit(tension,TargetCenter(choice,now-starts,fightDifficulty),ZoneWidth(choice,fightDifficulty))?new Color(0.48f,1,0.68f):new Color(1,0.64f,0.38f));
            if(reeled>=1){Complete(true);return;}if(danger>=0.8f||now-starts>=45)Complete(false);
        }
        private static void Complete(bool success)
        {
            if(choice<0)return;active=false;int fish=choice;choice=-1;
            if(!success){Refresh();status?.SetText(Expansion.L("fish_escaped"));return;}
            Progress.Set("fishing_pending",fish+1);Progress.Set("fishing_pending_sex",UnityEngine.Random.value<0.5f?0:1);
            Progress.Set("fishing_pending_payment",0);Progress.Set("fishing_pending_bait",premiumCast?1:0);
            if(fish<4&&UnityEngine.Random.value<0.2f)Progress.Set("fishing_pending_roe",fish+1);
            TryClaim();
        }
        public static void TryClaim()
        {
            if(!Progress.Ready||active||ModHelper.GetCurrentAfterhourLocationId()!=LocationId)return;
            int pending=Progress.Get("fishing_pending");string caught=null;
            if(pending>0)
            {
                bool unpaid=Progress.Get("fishing_pending_payment")==1;
                var bait=unpaid?Bait(Progress.Get("fishing_pending_bait")==1):null;
                if(unpaid&&(bait==null||Remaining<=0)){Refresh();status?.SetText(Expansion.L("fishing_pending_bait"));return;}
                int fish=pending-1;GameItem item;
                if(fish<4)
                {
                    string key=ExpansionStock.FishKeys[fish];item=DirectoryMaster.Item("pelican_"+key);var species=ExpansionStock.Fish[key];
                    Crops.SetInt(item,"FISH_AGE_TAG",species.stageStartDays[4]);AquacultureHelper.SeedKcalEatenForAge(item,species);FarmSpecies.SetString(item,"FISH_SEX_TAG",Progress.Get("fishing_pending_sex")==0?"MALE":"FEMALE");
                    FarmSpecies.SetFloat(item,"FISH_HUNGER_TAG",species.maxHunger*0.8f);ExpansionStock.Refresh(item);item.unitValue=new[]{20,25,35,45}[fish];caught=Expansion.L(key);
                }
                else if(fish<8){item=DirectoryMaster.Item("pelican_"+Kings[fish-4]);caught=Expansion.L(Kings[fish-4]);}
                else{item=DirectoryMaster.Item("scrap_metal");caught=item.name;}
                GeneralHelper.SetItemOwned(item,true);
                if(GraphUtils.TryAcceptAll(EmporiumEntry.Instance.afterhourInventory,item)!=0)
                {if(item.parentInventory==null)item.Destroy();Refresh();status?.SetText(Expansion.L("fishing_ground_full"));return;}
                // Only r5's legacy unpaid reward still needs a placement-time
                // payment. New casts were paid up front and never pay again.
                if(unpaid){Expansion.ConsumeUnits(bait,1);Progress.Set("fishing_used",Progress.Get("fishing_used")+1);}
                Progress.Set("fishing_pending_payment",0);Progress.Set("fishing_pending",0);
                if(King(fish))Progress.Set("fishing_ready_"+Kings[fish-4],Progress.Day+Cooldowns[fish-4]);
            }
            int roe=Progress.Get("fishing_pending_roe");
            if(roe>0)
            {
                var item=DirectoryMaster.Item("pelican_"+ExpansionStock.FishKeys[roe-1]+"_roe");GeneralHelper.SetItemOwned(item,true);
                if(GraphUtils.TryAcceptAll(EmporiumEntry.Instance.afterhourInventory,item)==0)Progress.Set("fishing_pending_roe",0);else if(item.parentInventory==null)item.Destroy();
            }
            Refresh();if(!Pending&&caught!=null)status?.SetText(Expansion.L("fish_caught",caught));
        }
    }
}
