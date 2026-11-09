using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PelicanCultivation
{
    // Borrow only the vanilla hydroponic guide's sprites and font references.
    // This creates no native book manager, drag/dock handler, zoom state or
    // serialized UnityEvent listener, so other guides keep their own state.
    public sealed class ManualWindow : MonoBehaviour,IPointerDownHandler,IBeginDragHandler,IDragHandler
    {
        public const string PageTag="PELICAN_MANUAL_PAGE";
        private static readonly List<ManualWindow> opened=new List<ManualWindow>();
        private RectTransform rect;
        private TextMeshProUGUI title,heading,body,counter;
        private Image picture;
        private Button left,right;
        private ManualBookData data;
        private GameItem item;
        private int page;
        private Vector2 pointer;
        public static void CloseAll(){foreach(var view in opened.ToArray())if(view!=null)view.Close();opened.Clear();}
        public static void Open(string key,GameItem item)
        {
            var source=MultiPageUIManager.Instance;
            if(source==null||source.panel==null||source.books==null||source.books.Count<=2)return;
            CloseAll();
            var guide=source.books[2];
            var candidates=source.panel.GetComponentsInChildren<Image>(true).Where(i=>!source.books.Where(b=>b!=guide).Any(b=>i.transform.IsChildOf(b.transform))).Concat(guide.GetComponentsInChildren<Image>(true)).Distinct();
            var paper=candidates.Where(i=>i.sprite!=null).OrderByDescending(i=>PaperRank(i.sprite.name)).ThenByDescending(i=>Math.Abs(i.rectTransform.rect.width*i.rectTransform.rect.height)).FirstOrDefault();
            if(paper==null)throw new InvalidOperationException("The native hydroponic guide has no paper background");
            var font=guide.GetComponentsInChildren<TextMeshProUGUI>(true).OrderByDescending(t=>(t.text??"").Length).FirstOrDefault();
            var parent=source.panel.transform.parent as RectTransform;
            var corners=new Vector3[4];paper.rectTransform.GetWorldCorners(corners);
            Vector3 low=parent.InverseTransformPoint(corners[0]),high=parent.InverseTransformPoint(corners[2]);
            float width=Math.Abs(high.x-low.x),height=Math.Abs(high.y-low.y);
            if(width<200||height<300){width=640;height=850;}
            float fit=Math.Min(1,Math.Min(parent.rect.width*.94f/width,parent.rect.height*.94f/height));
            width*=fit;height*=fit;
            var root=new GameObject("Pelican illustrated manual",typeof(RectTransform));root.SetActive(false);
            var rect=root.transform as RectTransform;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(width,height);rect.localScale=Vector3.one;
            var background=root.AddComponent<Image>();background.sprite=paper.sprite;background.type=paper.type;background.color=paper.color;background.raycastTarget=true;
            // Native pixel-item hit testing deliberately skips ordinary Images.
            // A render node with no PixelElement stops that search at the paper,
            // blocking machine hover and item actions behind this window.
            root.AddComponent<TreeNodeRender>();
            var view=root.AddComponent<ManualWindow>();view.rect=rect;view.item=item;
            view.title=view.Text("title",font,28,.14f,.045f,.72f,.06f);
            var band=view.Box("section band",.12f,.12f,.76f,.045f);band.color=new Color(.51f,.63f,.28f,1);
            view.heading=view.Text("chapter",font,24,.14f,.119f,.72f,.05f);view.heading.alignment=TextAlignmentOptions.Center;view.heading.color=Color.white;
            view.picture=view.Box("illustration",.39f,.19f,.22f,.15f);view.picture.preserveAspect=true;view.picture.color=Color.white;
            view.body=view.Text("body",font,Books.BodyFontSize,.14f,.37f,.72f,.48f);view.body.alignment=TextAlignmentOptions.TopLeft;view.body.lineSpacing=5;
            view.counter=view.Text("page",font,20,.33f,.92f,.34f,.05f);
            view.left=view.Button("previous",source.buttonLeft,.26f,.925f,.055f,.045f,()=>view.Flip(-1));
            view.right=view.Button("next",source.buttonRight,.69f,.925f,.055f,.045f,()=>view.Flip(1));
            var close=source.panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(n=>b.onClick.GetPersistentMethodName(n).IndexOf("Close",StringComparison.OrdinalIgnoreCase)>=0));
            if(close==null)close=source.panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name.IndexOf("close",StringComparison.OrdinalIgnoreCase)>=0);
            view.Button("close",close?.gameObject,.92f,.026f,.055f,.05f,view.Close);
            // Initialize TMP and resolve stretched rectangles before measuring.
            // The new window contains only our components; enabling it cannot
            // run a native book, dock or zoom handler.
            root.SetActive(true);Canvas.ForceUpdateCanvases();view.body.ForceMeshUpdate(true);
            var area=view.body.rectTransform.rect;
            view.data=Books.Build(key,text=>view.body.GetPreferredValues(text,Math.Max(24,area.width-6),float.PositiveInfinity).y<=Math.Max(30,area.height-10));
            view.page=Math.Max(0,Math.Min(view.data.pageCount-1,item.GetTagReadonly(PageTag).GetInt()));
            opened.Add(view);view.ShowPage();root.transform.SetAsLastSibling();
            OverlayHandler.current?.HideTooltip();TooltipManager._instance?.HideTooltip();
            if(PaperUIManager.Instance?.audioSourcePickup!=null)PaperUIManager.Instance.audioSourcePickup.Play();
        }
        private static int PaperRank(string name)
        {name=(name??"").ToLowerInvariant();return name.Contains("notebook")?4:name.Contains("manual")?3:name.Contains("book")?2:name.Contains("paper")?1:0;}
        private RectTransform Rect(string name,float x,float y,float width,float height)
        {
            var node=new GameObject(name,typeof(RectTransform));var r=node.transform as RectTransform;r.SetParent(rect,false);r.anchorMin=new Vector2(x,1-y-height);r.anchorMax=new Vector2(x+width,1-y);r.offsetMin=r.offsetMax=Vector2.zero;return r;
        }
        private Image Box(string name,float x,float y,float w,float h){var image=Rect(name,x,y,w,h).gameObject.AddComponent<Image>();image.raycastTarget=false;return image;}
        private TextMeshProUGUI Text(string name,TextMeshProUGUI sample,float size,float x,float y,float w,float h)
        {
            var text=Rect(name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();if(sample?.font!=null)text.font=sample.font;
            text.fontSize=size;text.enableAutoSizing=false;text.enableWordWrapping=true;text.color=new Color(.20f,.21f,.16f,1);text.alignment=TextAlignmentOptions.Center;text.overflowMode=TextOverflowModes.Overflow;text.raycastTarget=false;
            var localizer=text.gameObject.AddComponent<TMPLocalizer>();localizer.ChangeFontOnly=true;
            var source=sample?.GetComponent<TMPLocalizer>();if(source!=null)localizer.fontRole=source.fontRole;
            // Keep vanilla locale-aware font selection without an empty native
            // string-table component replacing our already localized content.
            var strings=text.gameObject.GetComponent("LocalizeStringEvent") as Behaviour;if(strings!=null)strings.enabled=false;
            return text;
        }
        private Button Button(string name,GameObject sample,float x,float y,float w,float h,Action click)
        {
            var r=Rect(name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();var native=sample?.GetComponentInChildren<Button>(true);var graphic=native?.targetGraphic as Image??sample?.GetComponentInChildren<Image>(true);
            if(graphic!=null){image.sprite=graphic.sprite;image.type=graphic.type;image.color=graphic.color;}else image.color=new Color(.51f,.63f,.28f,1);
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;
            if(native!=null){button.colors=native.colors;button.spriteState=native.spriteState;button.transition=native.transition;}
            // A fresh event contains no links to MultiPageUIManager or StoreUI.
            button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(()=>click());
            if(image.sprite==null){var label=Text(name+" label",body,24,x,y,w,h);label.text=name=="previous"?"◀":name=="next"?"▶":"×";}
            return button;
        }
        private void Flip(int delta)
        {
            int next=Math.Max(0,Math.Min(data.pageCount-1,page+delta));if(next==page)return;page=next;ShowPage();
            if(EmporiumAudioPlayer.Instance?.flip1!=null)EmporiumAudioPlayer.Instance.flip1.Play();
        }
        private void ShowPage()
        {
            title.text=data.title;heading.text=data.subtitle[page];body.text=data.body[page];counter.text=Expansion.L("manual_page",page+1,data.pageCount);
            var mod=ModLoader.Mods.Find(m=>m.Manifest.ID==ModEntry.ModId);picture.sprite=mod==null?null:Assets.InventorySprite(mod,data.images[page]);picture.enabled=picture.sprite!=null;
            left.gameObject.SetActive(page>0);right.gameObject.SetActive(page+1<data.pageCount);Crops.SetInt(item,PageTag,page);
        }
        public void Close(){opened.Remove(this);if(PaperUIManager.Instance?.audioSourceDrop!=null)PaperUIManager.Instance.audioSourceDrop.Play();Destroy(gameObject);}
        public void OnPointerDown(PointerEventData e){rect.SetAsLastSibling();}
        public void OnBeginDrag(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(rect.parent as RectTransform,e.position,e.pressEventCamera,out pointer);}
        public void OnDrag(PointerEventData e)
        {
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect.parent as RectTransform,e.position,e.pressEventCamera,out var now)){rect.anchoredPosition+=now-pointer;pointer=now;}
        }
        private void OnDestroy(){opened.Remove(this);}
    }
}
