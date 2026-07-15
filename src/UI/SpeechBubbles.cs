using System;
using System.Collections.Generic;
using TwitchColony.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TwitchColony.UI
{
    /// <summary>
    ///     Shows chat messages as speech bubbles floating above the duplicant whose name matches the
    ///     chatter's Twitch nick. Independent implementation. Must be called on the main thread.
    /// </summary>
    public static class SpeechBubbles
    {
        private const float WorldYOffset = 1.4f;

        private static Canvas canvas;
        private static readonly Dictionary<string, float> LastShownByUser = new Dictionary<string, float>();

        /// <summary>Try to show a bubble for a chat message. Returns true if a bubble was created.</summary>
        public static bool TryShow(string user, string text)
        {
            var cfg = ModConfig.Instance;
            if (!cfg.EnableBubbles || string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Opt-in prefix (empty prefix => every message qualifies).
            if (!string.IsNullOrEmpty(cfg.BubblePrefix))
            {
                if (!text.StartsWith(cfg.BubblePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                text = text.Substring(cfg.BubblePrefix.Length).TrimStart();
            }

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Per-user cooldown.
            var now = Time.unscaledTime;
            if (LastShownByUser.TryGetValue(user, out var last) && now - last < cfg.BubbleCooldownSeconds)
            {
                return false;
            }

            var target = FindMinionByName(user);
            if (target == null)
            {
                return false;
            }

            if (text.Length > cfg.MaxBubbleLength)
            {
                text = text.Substring(0, cfg.MaxBubbleLength) + "…";
            }

            LastShownByUser[user] = now;
            Spawn(target.transform, text, cfg);
            return true;
        }

        /// <summary>Show a bubble on a specific transform, bypassing prefix/cooldown (used by events).</summary>
        public static void ShowRaw(Transform target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            Spawn(target, text, ModConfig.Instance);
        }

        private static GameObject FindMinionByName(string user)
        {
            var items = Components.LiveMinionIdentities?.Items;
            if (items == null)
            {
                return null;
            }

            foreach (var identity in items)
            {
                if (identity == null)
                {
                    continue;
                }

                var name = Util.StripTextFormatting(identity.GetProperName());
                if (string.Equals(name, user, StringComparison.OrdinalIgnoreCase))
                {
                    return identity.gameObject;
                }
            }

            return null;
        }

        private static void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            var go = new GameObject("TwitchColony.BubbleCanvas");
            UnityEngine.Object.DontDestroyOnLoad(go);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
        }

        private static void Spawn(Transform target, string text, ModConfig cfg)
        {
            EnsureCanvas();

            var panelGo = new GameObject("Bubble");
            panelGo.transform.SetParent(canvas.transform, false);

            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.72f);

            var layout = panelGo.AddComponent<LayoutElement>();
            layout.preferredWidth = cfg.BubbleMaxWidth;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = cfg.BubbleFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;

            var textRect = tmp.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6, 4);
            textRect.offsetMax = new Vector2(-6, -4);

            var follow = panelGo.AddComponent<BubbleFollow>();
            follow.Init(target, cfg.BubbleSeconds);
        }

        /// <summary>Makes a bubble track a world-space target and expire after a lifetime.</summary>
        private sealed class BubbleFollow : MonoBehaviour
        {
            private Transform target;
            private float dieAt;
            private RectTransform rect;

            public void Init(Transform followTarget, float seconds)
            {
                target = followTarget;
                dieAt = Time.unscaledTime + seconds;
                rect = GetComponent<RectTransform>();
            }

            private void Update()
            {
                if (target == null || Time.unscaledTime >= dieAt)
                {
                    Destroy(gameObject);
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
                    rect.anchoredPosition = new Vector2(-9999, -9999); // behind camera: hide off-screen
                    return;
                }

                rect.position = screen;
            }
        }
    }
}
