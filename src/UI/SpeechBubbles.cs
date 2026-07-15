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
    ///     chatter's Twitch nick. At most one bubble per duplicant — a new message refreshes it rather
    ///     than stacking. Independent implementation. Must be called on the main thread.
    /// </summary>
    public static class SpeechBubbles
    {
        private const float WorldYOffset = 1.4f;

        private static Canvas canvas;
        private static readonly Dictionary<string, float> LastShownByUser = new Dictionary<string, float>();

        // One live bubble per target transform (so repeat messages refresh instead of stacking).
        private static readonly Dictionary<Transform, BubbleFollow> Active = new Dictionary<Transform, BubbleFollow>();

        private static readonly Dictionary<string, TMP_FontAsset> FontCache = new Dictionary<string, TMP_FontAsset>();
        private static bool loggedFonts;

        /// <summary>Try to show a bubble for a chat message. Returns true if a bubble was shown.</summary>
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

            // Chat text is untrusted: strip formatting so a chatter can't inject <size>/<color> markup.
            text = Util.StripTextFormatting(text ?? "").Trim();
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

        /// <summary>
        ///     Show a bubble on a specific transform, bypassing prefix/cooldown (used by events).
        ///     Returns the bubble GameObject (or null) so callers can replace/destroy it.
        /// </summary>
        public static GameObject ShowRaw(Transform target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text))
            {
                return null;
            }

            return Spawn(target, text, ModConfig.Instance);
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

            // Scale with resolution so bubbles are a consistent size across displays.
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            go.AddComponent<GraphicRaycaster>();
        }

        /// <summary>Resolve a game TMP font by name; null => TMP default. Logs available names once if not found.</summary>
        private static TMP_FontAsset ResolveFont(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (FontCache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            TMP_FontAsset found = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (f != null && f.name == name)
                {
                    found = f;
                    break;
                }
            }

            if (found == null && !loggedFonts)
            {
                loggedFonts = true;
                var names = new List<string>();
                foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (f != null)
                    {
                        names.Add(f.name);
                    }
                }

                Log.Warn($"BubbleFont '{name}' not found. Available TMP fonts: {string.Join(", ", names.ToArray())}");
            }

            FontCache[name] = found;
            return found;
        }

        private static GameObject Spawn(Transform target, string text, ModConfig cfg)
        {
            EnsureCanvas();

            // Refresh the existing bubble for this target instead of stacking a new one.
            if (Active.TryGetValue(target, out var existing) && existing != null)
            {
                existing.SetText(text);
                return existing.gameObject;
            }

            var panelGo = new GameObject("Bubble");
            panelGo.transform.SetParent(canvas.transform, false);

            // Bottom-centre pivot so the bubble sits above the anchor point.
            var prt = panelGo.GetComponent<RectTransform>() ?? panelGo.AddComponent<RectTransform>();
            prt.pivot = new Vector2(0.5f, 0f);

            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.72f);
            img.raycastTarget = false;

            var layout = panelGo.AddComponent<LayoutElement>();
            layout.preferredWidth = cfg.BubbleMaxWidth;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            var font = ResolveFont(cfg.BubbleFont);
            if (font != null)
            {
                tmp.font = font;
            }

            tmp.fontSize = cfg.BubbleFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            tmp.richText = false; // never interpret markup from chat as rich text

            var textRect = tmp.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6, 4);
            textRect.offsetMax = new Vector2(-6, -4);

            var follow = panelGo.AddComponent<BubbleFollow>();
            follow.Init(target, cfg.BubbleSeconds, tmp);
            follow.SetText(text);

            Active[target] = follow;
            return panelGo;
        }

        /// <summary>Makes a bubble track a world-space target, refresh its text, and expire after a lifetime.</summary>
        private sealed class BubbleFollow : MonoBehaviour
        {
            private Transform target;
            private float lifetime;
            private float dieAt;
            private RectTransform rect;
            private TextMeshProUGUI label;

            public void Init(Transform followTarget, float seconds, TextMeshProUGUI tmpLabel)
            {
                target = followTarget;
                lifetime = seconds;
                dieAt = Time.unscaledTime + seconds;
                rect = GetComponent<RectTransform>();
                label = tmpLabel;
            }

            /// <summary>Set the displayed text and reset the expiry timer.</summary>
            public void SetText(string text)
            {
                if (label != null)
                {
                    label.text = text;
                }

                dieAt = Time.unscaledTime + lifetime;
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
