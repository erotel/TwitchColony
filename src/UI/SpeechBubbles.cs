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

            // Match a duplicant first, then an adopted critter carrying the viewer's nick.
            var target = FindMinionByName(user) ?? FindCritterByName(user);
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

        /// <summary>The live duplicant carrying this nick, or null. Also used to stop a viewer who
        /// already has a duplicant from adopting a critter as well.</summary>
        internal static GameObject FindMinionByName(string user)
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

        /// <summary>Find a critter whose (adopted) name matches the chatter's nick, or null.</summary>
        private static GameObject FindCritterByName(string user)
        {
            var brains = Components.Brains?.Items;
            if (brains == null)
            {
                return null;
            }

            foreach (var brain in brains)
            {
                if (!(brain is CreatureBrain))
                {
                    continue;
                }

                var go = brain.gameObject;
                var sel = go != null ? go.GetComponent<KSelectable>() : null;
                if (sel == null)
                {
                    continue;
                }

                var name = Util.StripTextFormatting(sel.GetName() ?? "");
                if (string.Equals(name, user, StringComparison.OrdinalIgnoreCase))
                {
                    return go;
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
            UiLayer.PutBelowGameUi(canvas, 1); // frontmost of ours, still behind the game's screens

            // Scale exactly like the game's own UI, so bubbles sit right at any resolution and
            // follow the player's UI scale setting. (This used to scale by resolution alone, which
            // ignored that setting — at 1080p/100% it's the same number, so nothing moves for most.)
            UiScale.Track(go.AddComponent<CanvasScaler>());

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

            var panelGo = new GameObject("Bubble", typeof(RectTransform));
            panelGo.transform.SetParent(canvas.transform, false);

            // Bottom-centre pivot so the bubble sits above the anchor point.
            var prt = panelGo.GetComponent<RectTransform>();
            prt.pivot = new Vector2(0.5f, 0f);

            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.72f);
            img.raycastTarget = false;

            // Layout group + content-size fitter make the panel (the dark box) hug the text with padding.
            var layout = panelGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(9, 9, 4, 4);
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
            var font = ResolveFont(cfg.BubbleFont);
            if (font != null)
            {
                tmp.font = font;
            }

            tmp.fontSize = cfg.BubbleFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.richText = false; // never interpret markup from chat as rich text

            var textLayout = textGo.AddComponent<LayoutElement>();

            var follow = panelGo.AddComponent<BubbleFollow>();
            follow.Init(target, cfg.BubbleSeconds, tmp, textLayout, cfg.BubbleMaxWidth);
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
            private LayoutElement textLayout;
            private float maxWidth;

            public void Init(Transform followTarget, float seconds, TextMeshProUGUI tmpLabel,
                LayoutElement tmpLayout, float width)
            {
                target = followTarget;
                lifetime = seconds;
                dieAt = Time.unscaledTime + seconds;
                rect = GetComponent<RectTransform>();
                label = tmpLabel;
                textLayout = tmpLayout;
                maxWidth = width > 0f ? width : 100f;
            }

            /// <summary>Set the displayed text and reset the expiry timer.</summary>
            public void SetText(string text)
            {
                if (label != null)
                {
                    // Viewers type emoji the game's font can't draw; without this they'd be boxes.
                    // Measure and wrap the text we're actually going to show, not the original.
                    text = Glyphs.KeepRenderable(text, label);
                    if (string.IsNullOrEmpty(text))
                    {
                        // An emoji-only message: nothing left that this font can draw. Don't leave an
                        // empty box hanging over the duplicant — expire a fresh bubble, and leave an
                        // existing one showing whatever it already said.
                        if (label.text.Length == 0)
                        {
                            dieAt = 0f;
                        }

                        return;
                    }

                    // Short text stays on one snug line; only wrap (at a capped width) when it's too wide.
                    var unconstrained = label.GetPreferredValues(text);
                    if (textLayout != null && unconstrained.x > maxWidth)
                    {
                        textLayout.preferredWidth = maxWidth;
                        label.enableWordWrapping = true;
                    }
                    else
                    {
                        if (textLayout != null)
                        {
                            textLayout.preferredWidth = -1f;
                        }

                        label.enableWordWrapping = false;
                    }

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
