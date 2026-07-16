using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TwitchColony.UI
{
    /// <summary>
    ///     A small persistent name label that tracks an adopted critter, so its owner's nick is always
    ///     visible (like a duplicant's name) — not just the brief bubble shown at adoption time. One label
    ///     per critter; it removes itself when the critter is destroyed. Main thread only. Independent code.
    /// </summary>
    public static class CritterNameTags
    {
        private const float WorldYOffset = -0.35f; // just below the critter (a nameplate at its feet)

        private static Canvas canvas;
        private static readonly Dictionary<Transform, TagFollow> Active = new Dictionary<Transform, TagFollow>();

        /// <summary>Attach (or refresh) a persistent name label under a critter.</summary>
        public static void Show(Transform target, string nick)
        {
            if (target == null || string.IsNullOrEmpty(nick))
            {
                return;
            }

            EnsureCanvas();

            if (Active.TryGetValue(target, out var existing) && existing != null)
            {
                existing.SetText(nick);
                return;
            }

            // A tight dark pill behind the text keeps the nick readable over any background.
            var panelGo = new GameObject("CritterNameTag", typeof(RectTransform));
            panelGo.transform.SetParent(canvas.transform, false);
            var prt = panelGo.GetComponent<RectTransform>();
            prt.pivot = new Vector2(0.5f, 1f); // hang from its top edge, below the critter

            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.raycastTarget = false;

            var layout = panelGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 1, 1);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(panelGo.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 12;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.richText = false;
            tmp.enableWordWrapping = false;

            var follow = panelGo.AddComponent<TagFollow>();
            follow.Init(target, prt, tmp);
            follow.SetText(nick);
            Active[target] = follow;
        }

        /// <summary>Remove all name tags (called on colony load/unload).</summary>
        public static void Clear()
        {
            foreach (var kv in Active)
            {
                if (kv.Value != null)
                {
                    Object.Destroy(kv.Value.gameObject);
                }
            }

            Active.Clear();
        }

        private static void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            var go = new GameObject("TwitchColony.CritterNameTagCanvas");
            Object.DontDestroyOnLoad(go);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29500; // under the chat bubbles (30000), over the vote HUD

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            go.AddComponent<GraphicRaycaster>();
        }

        /// <summary>Tracks a critter and keeps the label pinned just below it every frame.</summary>
        private sealed class TagFollow : MonoBehaviour
        {
            private Transform target;
            private RectTransform rect;
            private TextMeshProUGUI label;

            public void Init(Transform followTarget, RectTransform panelRect, TextMeshProUGUI tmpLabel)
            {
                target = followTarget;
                rect = panelRect;
                label = tmpLabel;
            }

            public void SetText(string text)
            {
                if (label != null)
                {
                    label.text = text;
                }
            }

            private void Update()
            {
                if (target == null)
                {
                    Destroy(gameObject); // critter died / despawned
                    return;
                }

                var cam = CameraController.Instance != null ? CameraController.Instance.baseCamera : Camera.main;
                if (cam == null)
                {
                    return;
                }

                var world = target.position + new Vector3(0f, WorldYOffset, 0f);
                var screen = cam.WorldToScreenPoint(world);
                if (screen.z < 0f)
                {
                    rect.anchoredPosition = new Vector2(-9999, -9999); // behind camera
                    return;
                }

                rect.position = new Vector3(screen.x, screen.y, 0f);
            }

            private void OnDestroy()
            {
                if (target != null)
                {
                    Active.Remove(target);
                }
            }
        }
    }
}
