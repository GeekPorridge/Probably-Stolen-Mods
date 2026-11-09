using System;
using System.Collections.Generic;
using UnityEngine;

namespace StationScavenging
{
    internal static class StationArt
    {
        private static readonly Dictionary<Region, GameObject> stages = new Dictionary<Region, GameObject>();
        private static string Name(Region region)
        {
            switch (region)
            {
                case Region.Upper: return "upper";
                case Region.Security: return "security";
                case Region.Revolution: return "revolution";
                case Region.BlackMarket: return "black";
                default: throw new ArgumentOutOfRangeException("region");
            }
        }
        internal static string CoverKey(Region region) { return ModEntry.ModId + ":" + Name(region) + "_cover"; }
        internal static string BackgroundKey(Region region) { return ModEntry.ModId + ":" + Name(region) + "_background"; }
        internal static GameObject Stage(Region region)
        {
            GameObject root;
            if (stages.TryGetValue(region, out root) && root != null) { Fit(root); return root; }
            if (CustomUIManager.Instance == null) return null;
            var sprite = CustomUIManager.Instance.GetSprite(BackgroundKey(region));
            if (sprite == null) return null;
            root = new GameObject("StationScavenging_" + Name(region) + "_Stage");
            root.SetActive(false);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 20;
            stages[region] = root;
            Fit(root);
            return root;
        }
        internal static void FitActive(Region region)
        {
            GameObject stage;
            if (stages.TryGetValue(region, out stage) && stage != null && stage.activeSelf) Fit(stage);
        }
        private static void Fit(GameObject stage)
        {
            var camera = Camera.main;
            var renderer = stage.GetComponent<SpriteRenderer>();
            if (camera == null || !camera.orthographic || renderer == null || renderer.sprite == null) return;
            Vector3 size = renderer.sprite.bounds.size;
            float height = camera.orthographicSize * 2;
            float scale = Math.Max(height / Math.Max(0.001f, size.y), height * camera.aspect / Math.Max(0.001f, size.x));
            stage.transform.localScale = new Vector3(scale, scale, 1);
            stage.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0);
        }
        internal static void Dispose()
        {
            foreach (var stage in stages.Values) if (stage != null) UnityEngine.Object.Destroy(stage);
            stages.Clear();
        }
    }
}
