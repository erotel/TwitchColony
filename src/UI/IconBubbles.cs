using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace TwitchColony.UI
{
    /// <summary>
    ///     Floats an icon above a duplicant for a fixed time — the party popper on a sub, say.
    ///
    ///     Separate from <see cref="SpeechBubbles"/> because that shows font text, and the whole
    ///     reason this exists is that the game's fonts have no emoji. An icon is a sprite, so it
    ///     always draws. Same trick otherwise: a screen-space bubble re-pinned each frame to where
    ///     the duplicant projects on screen.
    /// </summary>
    internal static class IconBubbles
    {
        private const float WorldYOffset = 1.5f;
        private const float IconSize = 44f;

        private static Canvas canvas;
        private static Sprite subSprite;
        private static bool subSpriteTried;

        // One icon per target, so celebrating twice refreshes rather than stacks.
        private static readonly Dictionary<Transform, IconFollow> Active = new Dictionary<Transform, IconFollow>();

        /// <summary>The party-popper sprite for sub celebrations, loaded once from the embedded PNG.</summary>
        public static Sprite SubCelebrate
        {
            get
            {
                if (!subSpriteTried)
                {
                    subSpriteTried = true;
                    subSprite = LoadEmbedded("TwitchColony.sub_celebrate.png");
                }

                return subSprite;
            }
        }

        /// <summary>Show <paramref name="sprite"/> above <paramref name="target"/> for a while.</summary>
        public static void Show(Transform target, Sprite sprite, float seconds)
        {
            if (target == null || sprite == null || seconds <= 0f)
            {
                return;
            }

            EnsureCanvas();

            if (Active.TryGetValue(target, out var existing) && existing != null)
            {
                existing.Refresh(sprite, seconds);
                return;
            }

            var go = new GameObject("IconBubble", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;

            var rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(IconSize, IconSize);

            var follow = go.AddComponent<IconFollow>();
            follow.Init(target, seconds);
            Active[target] = follow;
        }

        private static void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            var go = new GameObject("TwitchColony.IconBubbleCanvas");
            Object.DontDestroyOnLoad(go);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UiLayer.PutBelowGameUi(canvas, 1); // with the chat bubbles, above tags and the vote HUD
            UiScale.Track(go.AddComponent<CanvasScaler>());
            go.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        ///     Turn an embedded PNG into a sprite. The image ships inside the DLL (see the csproj
        ///     EmbeddedResource) so there's no loose file to lose or for a user to have to install.
        /// </summary>
        private static Sprite LoadEmbedded(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Log.Warn("Icon resource not found: " + resourceName);
                        return null;
                    }

                    byte[] bytes;
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        bytes = memory.ToArray();
                    }

                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!DecodePng(texture, bytes)) // resizes to the PNG's real dimensions
                    {
                        Log.Warn("Failed to decode icon: " + resourceName);
                        return null;
                    }

                    texture.filterMode = FilterMode.Bilinear;
                    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                }
            }
            catch (System.Exception e)
            {
                Log.Warn("Loading icon '" + resourceName + "' threw: " + e.Message);
                return null;
            }
        }

        /// <summary>
        ///     Decode PNG bytes into <paramref name="texture"/> via reflection.
        ///
        ///     The one API for this — ImageConversion.LoadImage — lives in
        ///     UnityEngine.ImageConversionModule, and referencing that at build time drags in a
        ///     netstandard version that clashes with our net48 target (CS1705). The game loads the
        ///     module fine at runtime, so we reach it by reflection and skip the build-time headache.
        /// </summary>
        private static bool DecodePng(Texture2D texture, byte[] bytes)
        {
            var method = System.Type
                .GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                ?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });

            if (method == null)
            {
                Log.Warn("ImageConversion.LoadImage not found; can't decode icons.");
                return false;
            }

            return (bool)method.Invoke(null, new object[] { texture, bytes });
        }

        /// <summary>Pins an icon to a duplicant's screen position and expires it. Same idea as BubbleFollow.</summary>
        private sealed class IconFollow : MonoBehaviour
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

            public void Refresh(Sprite sprite, float seconds)
            {
                if (TryGetComponent<Image>(out var image))
                {
                    image.sprite = sprite;
                }

                dieAt = Time.unscaledTime + seconds;
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

                var screen = cam.WorldToScreenPoint(target.position + new Vector3(0f, WorldYOffset, 0f));
                if (screen.z < 0f)
                {
                    rect.anchoredPosition = new Vector2(-9999, -9999); // behind the camera
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
