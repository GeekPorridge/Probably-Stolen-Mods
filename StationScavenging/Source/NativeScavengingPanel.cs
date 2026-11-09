using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StationScavenging
{
    // Clone the game's panel, including its anchors, graphics and button styling.
    // Every region gets isolated callbacks; the original panel remains untouched.
    internal sealed class NativeScavengingPanel
    {
        internal readonly GameObject Root;
        private readonly TextMeshProUGUI timer, precious, risk;
        private readonly Button scavenge;

        internal NativeScavengingPanel(GameObject source, Action onScavenge, Action onReturn)
        {
            if (source == null) throw new InvalidOperationException("The native scavenging panel is unavailable.");
            Root = UnityEngine.Object.Instantiate(source, source.transform.parent, false);
            Root.name = "StationScavengingNativePanel";
            Root.SetActive(false);
            try
            {
                var map = MapUIManager.Instance;
                timer = Match(map.scavTimeRemainingTMP, source.transform);
                precious = Match(map.minorWoundChanceTMP, source.transform);
                risk = Match(map.majorWoundChanceTMP, source.transform);
                bool returnBound = false;
                foreach (var original in source.GetComponentsInChildren<Button>(true))
                {
                    bool isSearch = false, isReturn = false;
                    for (int i = 0; i < original.onClick.GetPersistentEventCount(); i++)
                    {
                        string method = original.onClick.GetPersistentMethodName(i);
                        isSearch |= method == "ScavengeButtonClick";
                        isReturn |= method == "LeaveScavenging" || method == "Return";
                    }
                    if (!isSearch && !isReturn) continue;
                    var button = Match(original, source.transform);
                    // RemoveAllListeners does not remove serialized persistent calls.
                    // Replace the event so a click cannot invoke the vanilla loot/injury path too.
                    button.onClick = new Button.ButtonClickedEvent();
                    if (isSearch)
                    {
                        scavenge = button;
                        button.onClick.AddListener(() => onScavenge());
                    }
                    else { button.onClick.AddListener(() => onReturn()); returnBound = true; }
                    var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null) label.text = LocHelper.GetLocalizedUI(isSearch ? "afterhour_scavenge_action" : "afterhour_return_action");
                }
                if (scavenge == null || !returnBound) throw new InvalidOperationException("The native scavenge/return buttons could not be located.");
                FitText(timer); FitText(precious); FitText(risk);
            }
            catch { Dispose(); throw; }
        }

        private T Match<T>(T original, Transform source) where T : Component
        {
            if (original == null) throw new InvalidOperationException("A native panel component is missing.");
            var indices = new Stack<int>();
            Transform cursor = original.transform;
            while (cursor != source)
            {
                if (cursor == null) throw new InvalidOperationException("The native component is outside the panel.");
                indices.Push(cursor.GetSiblingIndex());
                cursor = cursor.parent;
            }
            cursor = Root.transform;
            while (indices.Count > 0) cursor = cursor.GetChild(indices.Pop());
            var result = cursor.GetComponent<T>();
            if (result == null) throw new InvalidOperationException("A cloned panel component is missing.");
            return result;
        }

        private static void FitText(TextMeshProUGUI label)
        {
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Math.Min(11, label.fontSize);
            label.enableAutoSizing = true;
        }

        internal void Refresh(string timeText, string preciousText, string riskText, bool canSearch)
        {
            if (timer.text != timeText) timer.text = timeText;
            if (precious.text != preciousText) precious.text = preciousText;
            if (risk.text != riskText) risk.text = riskText;
            scavenge.interactable = canSearch;
        }

        internal void Dispose()
        {
            if (Root != null) { Root.SetActive(false); UnityEngine.Object.Destroy(Root); }
        }
    }
}
