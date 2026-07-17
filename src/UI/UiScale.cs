using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TwitchColony.UI
{
    /// <summary>
    ///     Keeps our canvases the same size as the game's own UI.
    ///
    ///     Our windows used to scale by themselves and ignore the game's UI scale setting entirely,
    ///     which nobody notices at 1080p and looks silly at 4K: Sgt_Imalas sent a screenshot of the
    ///     vote panel as a postage stamp next to the game's buttons at 150% scale.
    ///
    ///     Rather than work the size out ourselves, we ask the game. KCanvasScaler.GetCanvasScale()
    ///     is the exact number it applies to its own canvases — the player's scale setting times a
    ///     step chosen from the screen height — so matching it means we're right at every resolution
    ///     and setting, including ones nobody has tried yet.
    /// </summary>
    internal static class UiScale
    {
        /// <summary>How often to re-ask the game, in seconds. The setting changes about never.</summary>
        private const float PollInterval = 1f;

        private static readonly List<CanvasScaler> Tracked = new List<CanvasScaler>();
        private static KCanvasScaler gameScaler;
        private static float lastPoll = float.NegativeInfinity;
        private static float lastScale = 1f;

        /// <summary>Match this canvas to the game's UI now, and keep it matched.</summary>
        public static void Track(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }

            Tracked.Add(scaler);
            Apply(scaler, Current());
        }

        /// <summary>Re-check the game's scale and push it to every canvas we own. Cheap; poll it.</summary>
        public static void Refresh()
        {
            if (Time.unscaledTime - lastPoll < PollInterval)
            {
                return;
            }

            var scale = Current();
            for (var i = Tracked.Count - 1; i >= 0; i--)
            {
                if (Tracked[i] == null)
                {
                    Tracked.RemoveAt(i); // canvas went away with its scene
                }
                else
                {
                    Apply(Tracked[i], scale);
                }
            }
        }

        private static void Apply(CanvasScaler scaler, float scale)
        {
            // The game uses ConstantPixelSize and drives scaleFactor itself; do exactly the same,
            // or the two scalings fight each other.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            if (!Mathf.Approximately(scaler.scaleFactor, scale))
            {
                scaler.scaleFactor = scale;
            }
        }

        /// <summary>The scale the game is using, or 1 if it won't say.</summary>
        private static float Current()
        {
            lastPoll = Time.unscaledTime;

            try
            {
                if (gameScaler == null)
                {
                    // Only when we've lost it — FindObjectOfType is too slow to run per frame.
                    gameScaler = Object.FindObjectOfType<KCanvasScaler>();
                }

                if (gameScaler != null)
                {
                    var scale = gameScaler.GetCanvasScale();
                    if (scale > 0.01f)
                    {
                        lastScale = scale;
                    }
                }
            }
            catch
            {
                // No scaler yet (main menu, early load): keep whatever we had.
            }

            return lastScale;
        }
    }
}
