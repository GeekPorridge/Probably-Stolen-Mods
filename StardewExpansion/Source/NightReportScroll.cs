using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PelicanCultivation
{
    public static class NightReportLayout
    {
        private static bool ValidFontSize(float value) => value>0 && !float.IsNaN(value) && !float.IsInfinity(value);
        public static float FontSize(float prefabSize,float prefabMax,bool autoSize,float currentSize)
        {
            // The shipped prefab stores a previously fitted 15, while its
            // original auto-size base/max is 24. Freezing the fitted value
            // shrinks even a short report. Keep external font enlargements.
            float native=autoSize && ValidFontSize(prefabMax)?prefabMax:prefabSize;
            return ValidFontSize(native)?Math.Max(native,ValidFontSize(currentSize)?currentSize:0):currentSize;
        }
        public static float RowHeight(float preferred,float fontSize)
            => Math.Max(fontSize*1.3f,preferred)+4;
        public static float ContentHeight(float viewport,IEnumerable<float> rows,float spacing,float padding)
        {
            float height=Math.Max(0,padding);int count=0;
            foreach(float row in rows){height+=Math.Max(0,row);count++;}
            return Math.Max(Math.Max(1,viewport),height+Math.Max(0,count-1)*spacing);
        }
    }

    [HarmonyPatch(typeof(StartOfDayUIManager),nameof(StartOfDayUIManager.InitPanel))]
    public static class NightReportScrollPatch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(StartOfDayUIManager __instance)
        {if(ModEntry.Enabled) NightReportScroll.Ensure(__instance);}
    }

    // Keep the original contentGroupObject and its actual native/add-on row
    // objects. Only wrap that group, leaving the ledger frame and footer alone.
    public sealed class NightReportScroll : MonoBehaviour
    {
        private const float BarWidth=11,BarGap=7;
        private static readonly List<NightReportScroll> active=new List<NightReportScroll>();
        private StartOfDayUIManager manager;
        private RectTransform content,viewport;
        private Transform originalParent;
        private int originalSibling;
        private RectState originalRect;
        private ScrollRect scroll;
        private VerticalLayoutGroup layout;
        private ContentSizeFitter fitter;
        private bool addedLayout,fitterEnabled,oldLayoutEnabled,oldControlHeight,oldExpandHeight,oldControlWidth,oldExpandWidth;
        private bool dirty=true,resetTop=true,measuring,restored;
        private int previousChildren=-1;
        private float previousWidth=-1,previousHeight=-1;
        private readonly Dictionary<TextMeshProUGUI,RowState> rows=new Dictionary<TextMeshProUGUI,RowState>();

        private struct RectState
        {
            public Vector2 Min,Max,Pivot,Position,Size;
            public Vector3 Scale;
            public Quaternion Rotation;
            public RectState(RectTransform r){Min=r.anchorMin;Max=r.anchorMax;Pivot=r.pivot;Position=r.anchoredPosition;Size=r.sizeDelta;Scale=r.localScale;Rotation=r.localRotation;}
            public void Apply(RectTransform r){r.anchorMin=Min;r.anchorMax=Max;r.pivot=Pivot;r.anchoredPosition=Position;r.sizeDelta=Size;r.localScale=Scale;r.localRotation=Rotation;}
        }
        private sealed class RowState
        {
            public bool AutoSize,Wrap,AddedElement,IgnoreLayout;
            public TextOverflowModes Overflow;
            public LayoutElement Element;
            public float MinHeight,PreferredHeight,FlexibleHeight,FontSize;
            public RowState(TextMeshProUGUI text)
            {
                AutoSize=text.enableAutoSizing;Wrap=text.enableWordWrapping;Overflow=text.overflowMode;
                FontSize=text.fontSize;
                Element=text.GetComponent<LayoutElement>();AddedElement=Element==null;
                if(AddedElement) Element=text.gameObject.AddComponent<LayoutElement>();
                IgnoreLayout=Element.ignoreLayout;MinHeight=Element.minHeight;PreferredHeight=Element.preferredHeight;FlexibleHeight=Element.flexibleHeight;
            }
            public void Restore(TextMeshProUGUI text)
            {
                if(text==null) return;text.fontSize=FontSize;text.enableAutoSizing=AutoSize;text.enableWordWrapping=Wrap;text.overflowMode=Overflow;
                if(Element==null) return;
                if(AddedElement) UnityEngine.Object.Destroy(Element);
                else{Element.ignoreLayout=IgnoreLayout;Element.minHeight=MinHeight;Element.preferredHeight=PreferredHeight;Element.flexibleHeight=FlexibleHeight;}
            }
        }
        private static RectTransform Rect(string name,Transform parent)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.localScale=Vector3.one;return r;
        }
        private static void Stretch(RectTransform r)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
        public static void Ensure(StartOfDayUIManager owner)
        {
            if(owner==null || owner.contentGroupObject==null) return;
            NightReportScroll created=null;
            try
            {
                active.RemoveAll(s=>s==null);
                var existing=active.Find(s=>s.manager==owner);
                if(existing!=null){existing.resetTop=true;existing.dirty=true;existing.Refresh();return;}
                var body=owner.contentGroupObject.transform as RectTransform;
                if(body==null || body.parent==null) return;
                var root=Rect("PelicanNativeNightReportScroll",body.parent);
                var host=root.gameObject.AddComponent<NightReportScroll>();
                created=host;
                host.manager=owner;host.content=body;host.originalParent=body.parent;host.originalSibling=body.GetSiblingIndex();host.originalRect=new RectState(body);
                active.Add(host);host.Configure(root);host.Refresh();
            }
            catch(Exception error){if(created!=null) created.Restore();ModEntry.Error("Native night report scrolling",error);}
        }
        private void Configure(RectTransform root)
        {
            originalRect.Apply(root);root.SetSiblingIndex(originalSibling);
            var oldElement=content.GetComponent<LayoutElement>();
            if(oldElement!=null)
            {
                var replacement=root.gameObject.AddComponent<LayoutElement>();
                replacement.ignoreLayout=oldElement.ignoreLayout;replacement.minWidth=oldElement.minWidth;replacement.minHeight=oldElement.minHeight;
                replacement.preferredWidth=oldElement.preferredWidth;replacement.preferredHeight=oldElement.preferredHeight;
                replacement.flexibleWidth=oldElement.flexibleWidth;replacement.flexibleHeight=oldElement.flexibleHeight;replacement.layoutPriority=oldElement.layoutPriority;
            }
            var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;hit.canvasRenderer.cullTransparentMesh=false;
            viewport=Rect("Viewport",root);Stretch(viewport);viewport.offsetMax=new Vector2(-BarWidth-BarGap,0);
            viewport.gameObject.AddComponent<RectMask2D>();
            content.SetParent(viewport,false);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(0.5f,1);
            content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;content.localScale=Vector3.one;content.localRotation=Quaternion.identity;
            layout=content.GetComponent<VerticalLayoutGroup>();addedLayout=layout==null;
            if(addedLayout){layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=5;}
            oldLayoutEnabled=layout.enabled;oldControlHeight=layout.childControlHeight;oldControlWidth=layout.childControlWidth;oldExpandHeight=layout.childForceExpandHeight;oldExpandWidth=layout.childForceExpandWidth;
            layout.enabled=true;
            layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            fitter=content.GetComponent<ContentSizeFitter>();if(fitter!=null){fitterEnabled=fitter.enabled;fitter.enabled=false;}
            scroll=root.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=38;scroll.inertia=true;
            var bar=Rect("Scrollbar",root);bar.anchorMin=new Vector2(1,0);bar.anchorMax=Vector2.one;bar.pivot=new Vector2(1,0.5f);bar.sizeDelta=new Vector2(BarWidth,-4);bar.anchoredPosition=Vector2.zero;
            var track=bar.gameObject.AddComponent<Image>();track.color=new Color(0.35f,0.25f,0.13f,0.18f);
            var sliding=Rect("SlidingArea",bar);Stretch(sliding);sliding.offsetMin=new Vector2(1,1);sliding.offsetMax=new Vector2(-1,-1);
            var handle=Rect("Handle",sliding);Stretch(handle);var grip=handle.gameObject.AddComponent<Image>();grip.color=new Color(0.42f,0.28f,0.12f,0.78f);
            var scrollbar=bar.gameObject.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.BottomToTop;scrollbar.handleRect=handle;scrollbar.targetGraphic=grip;
            scrollbar.navigation=new Navigation{mode=Navigation.Mode.None};scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        private void OnEnable(){TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged);dirty=true;}
        private void OnDisable(){TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged);}
        private void TextChanged(UnityEngine.Object value)
        {
            var text=value as TMP_Text;
            if(!measuring && content!=null && text!=null && text.transform.IsChildOf(content)) dirty=true;
        }
        private void LateUpdate()
        {
            if(!ModEntry.Enabled){Restore();return;}
            if(content==null || viewport==null || !gameObject.activeInHierarchy) return;
            if(dirty || content.childCount!=previousChildren || Math.Abs(viewport.rect.width-previousWidth)>0.5f || Math.Abs(viewport.rect.height-previousHeight)>0.5f) Refresh();
        }
        private void Refresh()
        {
            if(restored || content==null || viewport==null || scroll==null || measuring) return;
            measuring=true;
            try
            {
                float width=viewport.rect.width;if(width<=1) {dirty=true;return;}
                var removed=new List<TextMeshProUGUI>();foreach(var row in rows) if(row.Key==null) removed.Add(row.Key);
                foreach(var text in removed) rows.Remove(text);
                var heights=new List<float>();
                var template=manager.startOfDayTMPPrefab==null?null:manager.startOfDayTMPPrefab.GetComponent<TextMeshProUGUI>();
                float nativeSize=template==null?0:template.fontSize,nativeMax=template==null?0:template.fontSizeMax;
                bool nativeAutoSize=template!=null && template.enableAutoSizing;
                for(int i=0;i<content.childCount;i++)
                {
                    var child=content.GetChild(i) as RectTransform;if(child==null || !child.gameObject.activeSelf) continue;
                    var text=child.GetComponent<TextMeshProUGUI>();
                    if(text!=null)
                    {
                        if(!rows.TryGetValue(text,out var state)){state=new RowState(text);rows.Add(text,state);}
                        // Height follows text, rather than shrinking every line
                        // to fit the old fixed-height non-scrolling report.
                        text.enableAutoSizing=false;text.enableWordWrapping=true;text.overflowMode=TextOverflowModes.Overflow;
                        text.fontSize=NightReportLayout.FontSize(nativeSize,nativeMax,nativeAutoSize,text.fontSize);
                        float height=NightReportLayout.RowHeight(text.GetPreferredValues(text.text,Math.Max(1,width-layout.padding.horizontal),float.PositiveInfinity).y,text.fontSize);
                        state.Element.ignoreLayout=false;state.Element.minHeight=height;state.Element.preferredHeight=height;state.Element.flexibleHeight=0;heights.Add(height);
                    }
                    else
                    {
                        var element=child.GetComponent<LayoutElement>();if(element!=null && element.ignoreLayout) continue;
                        heights.Add(Math.Max(child.rect.height,LayoutUtility.GetPreferredHeight(child)));
                    }
                }
                float heightNeeded=NightReportLayout.ContentHeight(viewport.rect.height,heights,layout.spacing,layout.padding.vertical);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,heightNeeded);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                if(resetTop){scroll.StopMovement();scroll.verticalNormalizedPosition=1;resetTop=false;}
                previousChildren=content.childCount;previousWidth=viewport.rect.width;previousHeight=viewport.rect.height;dirty=false;
            }
            catch(Exception error){dirty=false;ModEntry.Error("Night report text layout",error);Restore();}
            finally{measuring=false;}
        }
        private void Restore()
        {
            if(restored) return;restored=true;
            foreach(var row in rows) row.Value.Restore(row.Key);rows.Clear();
            if(content!=null && originalParent!=null)
            {
                content.SetParent(originalParent,false);content.SetSiblingIndex(originalSibling);originalRect.Apply(content);
                if(layout!=null)
                {
                    if(addedLayout) Destroy(layout);
                    else{layout.enabled=oldLayoutEnabled;layout.childControlHeight=oldControlHeight;layout.childControlWidth=oldControlWidth;layout.childForceExpandHeight=oldExpandHeight;layout.childForceExpandWidth=oldExpandWidth;}
                }
                if(fitter!=null) fitter.enabled=fitterEnabled;
            }
            active.Remove(this);Destroy(gameObject);
        }
        public static void RestoreAll(){foreach(var host in active.ToArray()) if(host!=null) host.Restore();active.Clear();}
    }
}
