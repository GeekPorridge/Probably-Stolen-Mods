using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace StationScavenging
{
    // Complete a native page before Canvas lays it out; retain cards and scroll.
    public sealed class StationTransitUI : MonoBehaviour
    {
        private sealed class CardBinding
        {
            internal TransitCardView Card;
            internal Region Region;
            internal TextMeshProUGUI Status, Info;
            internal string OriginalStatus, AppliedStatus;
            internal void Dispose()
            {
                if (Status != null && Status.text == AppliedStatus) Status.text = OriginalStatus;
                if (Info != null) UnityEngine.Object.Destroy(Info.gameObject);
            }
        }

        private static StationTransitUI instance;
        private readonly List<CardBinding> bindings = new List<CardBinding>(5);
        private readonly List<TransitCardView> cards = new List<TransitCardView>(10);
        private readonly string[] statuses = new string[5], information = new string[5];
        private readonly bool[] priorities = new bool[5];
        private GameObject windowRoot;
        private TransitFrame frame;
        private Transform firstCard, lastCard;
        private int childCount = -1;
        private string locale;
        private bool wasOpen, dirty = true, refreshing;

        internal static void Ensure()
        {
            if (instance != null || !ModEntry.Enabled || CustomUIManager.Instance == null) return;
            var root = new GameObject("StationScavengingTransitPresentation");
            instance = root.AddComponent<StationTransitUI>();
            UnityEngine.Object.DontDestroyOnLoad(root);
        }

        internal static void Invalidate() { if (instance != null) instance.dirty = true; }
        private void OnConfigApplied(string modId) { if (modId == ModEntry.ModId) dirty = true; }
        private void OnEventChanged(string locationId, string eventId) { dirty = true; }
        private void OnEnable()
        {
            Canvas.preWillRenderCanvases += BeforeCanvas;
            ModHook.OnReturningStoreLate += Invalidate;
            ModHook.OnModConfigApplied += OnConfigApplied;
            ModHook.OnAfterhourEventStarted += OnEventChanged;
            ModHook.OnAfterhourEventEnded += OnEventChanged;
            dirty = true;
        }
        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= BeforeCanvas;
            ModHook.OnReturningStoreLate -= Invalidate;
            ModHook.OnModConfigApplied -= OnConfigApplied;
            ModHook.OnAfterhourEventStarted -= OnEventChanged;
            ModHook.OnAfterhourEventEnded -= OnEventChanged;
        }
        internal static void Stop()
        {
            if (instance == null) return;
            instance.enabled = false;
            instance.Clear();
            UnityEngine.Object.Destroy(instance.gameObject);
            instance = null;
        }
        private void OnDestroy() { Clear(); }
        private void Clear()
        {
            foreach (var binding in bindings) binding.Dispose();
            bindings.Clear();
            cards.Clear();
            windowRoot = null;
            frame = null;
            firstCard = lastCard = null;
            childCount = -1;
            wasOpen = false;
            dirty = true;
        }

        private void BeforeCanvas()
        {
            if (refreshing) return;
            if (!ModEntry.Enabled || !PlayerStore.HasInstance || !TransitUI.IsOpen)
            {
                // Closing animations keep their text; no clear/recreate cycle.
                wasOpen = false;
                return;
            }
            var root = TransitUI.Root;
            if (root == null) return;
            refreshing = true;
            try
            {
                if (root != windowRoot)
                {
                    Clear();
                    windowRoot = root;
                    frame = root.GetComponent<TransitFrame>();
                }
                if (frame == null || frame.cardsHost == null) return;
                if (!wasOpen) dirty = true;
                wasOpen = true;
                string currentLocale = LocHelper.GetCurrentLocaleCode();
                if (currentLocale != locale) { locale = currentLocale; dirty = true; }
                bool refreshText = dirty;
                if (refreshText) CacheText();

                var host = frame.cardsHost;
                int count = host.childCount;
                Transform first = count == 0 ? null : host.GetChild(0);
                Transform last = count == 0 ? null : host.GetChild(count - 1);
                if (count != childCount || first != firstCard || last != lastCard)
                {
                    childCount = count;
                    firstCard = first;
                    lastCard = last;
                    Rebind(host);
                }
                if (refreshText)
                    foreach (var binding in bindings) Apply(binding);
            }
            finally { refreshing = false; }
        }

        private void CacheText()
        {
            // Translations and game-state strings change only on an opening,
            // language change or actual state/config/event change, not per tick.
            StationGame.EnsureRun();
            for (int i = 0; i <= 4; i++)
            {
                var region = (Region)i;
                string key = StationFlavor.StatusKey(region);
                statuses[i] = key == null ? LocHelper.GetLocalizedUI("ui_transit_unavailable") : ModEntry.L(key);
                information[i] = StationFlavor.CardInfo(region);
                priorities[i] = region == Region.Lower || !StationGame.IsUnlocked(region);
            }
            dirty = false;
        }

        private void Rebind(Transform host)
        {
            cards.Clear();
            host.GetComponentsInChildren<TransitCardView>(false, cards);
            // Native paging destroys the old page. Release its bindings now
            // instead of retaining every page visited until the map closes.
            for (int i = bindings.Count - 1; i >= 0; i--)
                if (bindings[i].Card == null || !cards.Contains(bindings[i].Card))
                { bindings[i].Dispose(); bindings.RemoveAt(i); }
            foreach (var card in cards)
            {
                Region region;
                if (card.location == null || !TryRegion(card.location.Id, out region)) continue;
                bool bound = false;
                foreach (var binding in bindings)
                    if (binding.Card == card) { bound = true; break; }
                if (bound) continue;
                var created = Bind(card, region);
                if (created != null) bindings.Add(created);
            }
        }

        private void Apply(CardBinding binding)
        {
            int index = (int)binding.Region;
            if (binding.Status != null)
            {
                string status = statuses[index] ?? binding.OriginalStatus;
                if (binding.Status.text != status) binding.Status.text = status;
                binding.AppliedStatus = status;
            }
            string info = information[index];
            if (binding.Info.text != info) binding.Info.text = info;
            var transform = binding.Info.transform;
            if (priorities[index])
            { if (transform.GetSiblingIndex() != 0) transform.SetAsFirstSibling(); }
            else if (transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
            bool visible = !string.IsNullOrEmpty(info);
            if (binding.Info.gameObject.activeSelf != visible) binding.Info.gameObject.SetActive(visible);
        }

        internal static bool TryRegion(string id, out Region region)
        {
            for (int i = 0; i <= 4; i++)
                if (StationRules.LocationId((Region)i) == id) { region = (Region)i; return true; }
            region = Region.Lower;
            return false;
        }

        private CardBinding Bind(TransitCardView card, Region region)
        {
            var content = card.transform.Find("Events/Viewport/Content");
            if (content == null) return null;
            var root = new GameObject("StationScavengingInfo", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(content, false);
            var label = root.AddComponent<TextMeshProUGUI>();
            label.font = frame.fontNotoRegular;
            label.fontSize = 17;
            label.color = new Color(0.72f, 0.76f, 0.73f, 1);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.enableWordWrapping = true;
            label.enableAutoSizing = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.richText = true;
            var localizer = root.AddComponent<TMPLocalizer>();
            localizer.ChangeFontOnly = true;
            localizer.fontRole = FontLoader.FontRole.Noto_Sans_Normal;
            (root.transform as RectTransform).sizeDelta = new Vector2(288, 0);
            var statusTransform = card.transform.Find("Status");
            var status = statusTransform == null ? null : statusTransform.GetComponent<TextMeshProUGUI>();
            var binding = new CardBinding { Card = card, Region = region, Info = label,
                Status = status, OriginalStatus = status == null ? null : status.text };
            // Assign text and ordering while inactive: the first visible frame
            // has final content and native layout measures the correct height.
            Apply(binding);
            return binding;
        }
    }
}
