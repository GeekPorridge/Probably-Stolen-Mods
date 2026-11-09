// Compile the production StationTransitUI unchanged against this small scene
// adapter. Tests lifecycle/render ordering and churn; not a Unity FPS benchmark.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        internal bool destroyed;
        public static bool operator ==(Object a, Object b)
        { bool an = ReferenceEquals(a, null) || a.destroyed, bn = ReferenceEquals(b, null) || b.destroyed; return an || bn ? an == bn : ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => base.GetHashCode();
        public static void DontDestroyOnLoad(Object o) { }
        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (o is GameObject go)
            {
                foreach (var child in go.transform.children.ToArray()) Destroy(child.gameObject);
                foreach (var c in go.components) { Call(c, "OnDestroy"); c.destroyed = true; }
                go.transform.SetParent(null, false);
            }
            o.destroyed = true;
        }
        internal static void Call(object target, string name)
        { target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null); }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component
    {
        private bool isEnabled = true;
        public bool enabled { get => isEnabled; set { if (isEnabled == value) return; isEnabled = value; Object.Call(this, value ? "OnEnable" : "OnDisable"); } }
    }
    public class GameObject : Object
    {
        public string name;
        public bool activeSelf = true;
        public Transform transform;
        internal readonly List<Component> components = new List<Component>();
        public GameObject(string name, params Type[] types)
        { this.name = name; transform = new RectTransform { gameObject = this }; components.Add(transform); }
        public T AddComponent<T>() where T : Component, new()
        { var value = new T { gameObject = this }; components.Add(value); if (activeSelf) Object.Call(value, "OnEnable"); return value; }
        public T GetComponent<T>() where T : Component
        { foreach (var c in components) if (c is T t) return t; return null; }
        public void SetActive(bool active)
        {
            if (activeSelf == active) return;
            if (active && name == "StationScavengingInfo")
            { TransitHarness.Metrics.Activations++; if (string.IsNullOrEmpty(GetComponent<TMPro.TextMeshProUGUI>().text)) TransitHarness.Metrics.EmptyActivations++; }
            activeSelf = active;
        }
    }
    public class Transform : Component
    {
        internal readonly List<Transform> children = new List<Transform>();
        public Transform parent;
        public int childCount => children.Count;
        public Transform GetChild(int i) => children[i];
        public void SetParent(Transform target, bool worldPositionStays)
        { parent?.children.Remove(this); parent = target; parent?.children.Add(this); }
        public Transform Find(string path)
        { var cursor = this; foreach (var segment in path.Split('/')) { Transform found = null; foreach (var c in cursor.children) if (c.gameObject.name == segment) { found = c; break; } if (found == null) return null; cursor = found; } return cursor; }
        public int GetSiblingIndex() => parent.children.IndexOf(this);
        public void SetAsFirstSibling() { parent.children.Remove(this); parent.children.Insert(0, this); TransitHarness.Metrics.Reorders++; }
        public void SetAsLastSibling() { parent.children.Remove(this); parent.children.Add(this); TransitHarness.Metrics.Reorders++; }
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : Component
        { TransitHarness.Metrics.Queries++; Collect(this, includeInactive, results); }
        private static void Collect<T>(Transform node, bool includeInactive, List<T> results) where T : Component
        { if (node == null || (!includeInactive && !node.gameObject.activeSelf)) return; var c = node.GetComponent<T>(); if (c != null) results.Add(c); foreach (var child in node.children) Collect(child, includeInactive, results); }
    }
    public class RectTransform : Transform { public Vector2 sizeDelta; }
    public struct Vector2 { public Vector2(float x, float y) { } }
    public struct Color { public Color(float r, float g, float b, float a) { } }
    public static class Canvas { public static event Action preWillRenderCanvases; public static void Render() => preWillRenderCanvases?.Invoke(); }
}
namespace TMPro
{
    public enum TextAlignmentOptions { TopLeft }
    public enum TextOverflowModes { Overflow }
    public class TextMeshProUGUI : UnityEngine.Component
    {
        private string value = "";
        public string text { get => value; set { this.value = value; TransitHarness.Metrics.Writes++; } }
        public object font;
        public float fontSize;
        public UnityEngine.Color color;
        public TextAlignmentOptions alignment;
        public TextOverflowModes overflowMode;
        public bool enableWordWrapping, enableAutoSizing, raycastTarget, richText;
    }
}
public class TMPLocalizer : UnityEngine.Component { public bool ChangeFontOnly; public FontLoader.FontRole fontRole; }
public static class FontLoader { public enum FontRole { Noto_Sans_Normal } }
public class TransitCardView : UnityEngine.Component { public TestLocation location; }
public class TestLocation { public string Id; }
public class TransitFrame : UnityEngine.Component { public UnityEngine.RectTransform cardsHost; public object fontNotoRegular; }
public static class TransitUI { public static bool IsOpen; public static UnityEngine.GameObject Root; }
public static class PlayerStore { public static bool HasInstance = true; }
public class CustomUIManager { public static CustomUIManager Instance = new CustomUIManager(); }
public static class LocHelper { public static string Locale = "zh"; public static string GetCurrentLocaleCode() => Locale; public static string GetLocalizedUI(string key) => Locale + ":Unavailable"; }
public static class ModHook
{
    public static event Action OnReturningStoreLate;
    public static event Action<string> OnModConfigApplied;
    public static event Action<string, string> OnAfterhourEventStarted, OnAfterhourEventEnded;
    public static void Return() => OnReturningStoreLate?.Invoke();
    public static void Configure(string id) => OnModConfigApplied?.Invoke(id);
    public static void Event(bool start) { if (start) OnAfterhourEventStarted?.Invoke("location", "event"); else OnAfterhourEventEnded?.Invoke("location", "event"); }
}
namespace StationScavenging
{
    public enum Region { Lower, Upper, Security, Revolution, BlackMarket }
    internal static class ModEntry { internal const string ModId = "station_scavenging"; internal static bool Enabled = true; internal static string L(string key) => LocHelper.Locale + ":" + key; }
    internal static class StationRules { internal static string LocationId(Region region) => ((int)region).ToString(); }
    internal static class StationGame { internal static bool Unlocked; internal static void EnsureRun() { } internal static bool IsUnlocked(Region region) => Unlocked; }
    internal static class StationFlavor
    {
        internal static int Revision;
        internal static string StatusKey(Region r) => "status" + Revision;
        internal static string CardInfo(Region r) { TransitHarness.Metrics.Builds++; return r == Region.Lower ? "" : LocHelper.Locale + ":info" + Revision + ":" + r; }
    }
}
namespace TransitHarness
{
    public static class Metrics { public static int Writes, Reorders, Queries, Builds, Activations, EmptyActivations; }
    public sealed class Result { public int Checks; public long StableFramesAllocatedBytes; public int PageChanges; public string Scope; }
    public static class Runner
    {
        private static int checks;
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        private static UnityEngine.GameObject Child(UnityEngine.Transform parent, string name)
        { var go = new UnityEngine.GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static TransitCardView AddCard(UnityEngine.Transform host, int region)
        {
            var root = Child(host, "Card_" + region);
            var status = Child(root.transform, "Status").AddComponent<TMPro.TextMeshProUGUI>(); status.text = "Unavailable";
            var content = Child(Child(Child(root.transform, "Events").transform, "Viewport").transform, "Content");
            Child(content.transform, "EventTitle"); Child(content.transform, "EventDescription");
            var card = root.AddComponent<TransitCardView>(); card.location = new TestLocation { Id = region.ToString() }; return card;
        }
        private static void NewWindow()
        {
            TransitUI.Root = new UnityEngine.GameObject("Transit");
            var frame = TransitUI.Root.AddComponent<TransitFrame>();
            frame.cardsHost = (UnityEngine.RectTransform)Child(TransitUI.Root.transform, "Cards").transform;
        }
        private static int Bindings()
        { var t = typeof(StationScavenging.StationTransitUI); var instance = t.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); return ((IList)t.GetField("bindings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance)).Count; }
        private static TMPro.TextMeshProUGUI Info(TransitCardView card) => card.transform.Find("Events/Viewport/Content/StationScavengingInfo").GetComponent<TMPro.TextMeshProUGUI>();
        public static Result Run()
        {
            NewWindow(); var host = TransitUI.Root.GetComponent<TransitFrame>().cardsHost;
            var card = AddCard(host, 1); var other = AddCard(host, 99);
            StationScavenging.StationTransitUI.Ensure(); TransitUI.IsOpen = true;
            UnityEngine.Canvas.Render();
            Check(Info(card).text == "zh:info0:Upper", "First render must contain the final localized text");
            Check(card.transform.Find("Status").GetComponent<TMPro.TextMeshProUGUI>().text == "zh:status0", "No generic unavailable flash on first render");
            Check(Metrics.EmptyActivations == 0, "A new label must receive text before becoming visible");
            Check(other.transform.Find("Events/Viewport/Content").childCount == 2, "Other locations remain untouched");
            var label = Info(card); var scrollContent = label.transform.parent;
            Check(label.transform.GetSiblingIndex() == 0, "Locked taunt appears before events");
            int writes = Metrics.Writes, queries = Metrics.Queries, builds = Metrics.Builds, reorders = Metrics.Reorders, activations = Metrics.Activations;
            for (int i = 0; i < 100; i++) UnityEngine.Canvas.Render();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) UnityEngine.Canvas.Render();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(Metrics.Writes == writes && Metrics.Queries == queries && Metrics.Builds == builds && Metrics.Reorders == reorders && Metrics.Activations == activations, "Idle/selection frames must not rewrite text, traverse cards, reorder or recreate labels");
            Check(allocated == 0, "Stable render adapter should allocate no managed memory");
            LocHelper.Locale = "en"; UnityEngine.Canvas.Render();
            Check(ReferenceEquals(label, Info(card)) && label.text == "en:info0:Upper", "Language change reuses the label and updates text");
            Check(ReferenceEquals(scrollContent, label.transform.parent), "Language change preserves native content/scroll object");
            TransitUI.IsOpen = false; writes = Metrics.Writes; UnityEngine.Canvas.Render();
            Check(Metrics.Writes == writes && !label.destroyed, "Closing retains labels for native closing animations");
            StationScavenging.StationFlavor.Revision = 1; TransitUI.IsOpen = true; UnityEngine.Canvas.Render();
            Check(ReferenceEquals(label, Info(card)) && label.text == "en:info1:Upper", "Reopening refreshes current state without recreating labels");
            StationScavenging.StationGame.Unlocked = true; ModHook.Return(); UnityEngine.Canvas.Render();
            Check(label.transform.GetSiblingIndex() == label.transform.parent.childCount - 1, "Unlocked information follows native event entries");
            builds = Metrics.Builds; ModHook.Configure("other_mod"); UnityEngine.Canvas.Render();
            Check(Metrics.Builds == builds, "Unrelated config changes cause no text work");
            ModHook.Configure("station_scavenging"); UnityEngine.Canvas.Render();
            Check(Metrics.Builds == builds + 5, "Own config invalidates one cached snapshot");
            builds = Metrics.Builds; ModHook.Event(true); UnityEngine.Canvas.Render();
            Check(Metrics.Builds == builds + 5, "A native event change invalidates one cached snapshot");
            for (int page = 0; page < 100; page++)
            {
                foreach (var child in host.children.ToArray()) UnityEngine.Object.Destroy(child.gameObject);
                for (int i = 0; i < 5; i++) AddCard(host, i);
                UnityEngine.Canvas.Render();
                Check(Bindings() == 5, "Page changes must release all old bindings");
                foreach (var child in host.children)
                { var c = child.GetComponent<TransitCardView>(); Check(c != null && (c.location.Id == "0" || Info(c).text.StartsWith("en:info1:")), "Fresh pages must be complete in their first render"); }
            }
            Check(Metrics.EmptyActivations == 0, "Repeated paging never shows an empty information label");
            NewWindow(); host = TransitUI.Root.GetComponent<TransitFrame>().cardsHost; card = AddCard(host, 2); UnityEngine.Canvas.Render();
            Check(Bindings() == 1, "Recreated native window releases its old bindings");
            var nativeStatus = card.transform.Find("Status").GetComponent<TMPro.TextMeshProUGUI>(); nativeStatus.text = "Other mod status";
            StationScavenging.StationTransitUI.Stop();
            Check(nativeStatus.text == "Other mod status", "Disabling preserves a later status supplied by another mod");
            Check(card.transform.Find("Events/Viewport/Content").childCount == 2, "Disabling removes only owned information");
            builds = Metrics.Builds; ModHook.Return(); ModHook.Event(false); UnityEngine.Canvas.Render();
            Check(Metrics.Builds == builds, "Disabled presentation leaves no active render/event callbacks");
            StationScavenging.StationTransitUI.Ensure(); UnityEngine.Canvas.Render();
            StationScavenging.StationTransitUI.Stop();
            Check(nativeStatus.text == "Other mod status", "Normal cleanup restores the status seen before decoration");
            return new Result { Checks = checks, StableFramesAllocatedBytes = allocated, PageChanges = 100, Scope = "Production presentation code with a deterministic scene/render adapter; not an in-game FPS or visual test." };
        }
    }
}
