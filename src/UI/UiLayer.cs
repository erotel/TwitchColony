using UnityEngine;

namespace TwitchColony.UI
{
    /// <summary>
    ///     Keeps our overlays underneath the game's own interface.
    ///
    ///     We used to give our canvases a sorting order of ~30000 to sit above the world. That does
    ///     work — it also puts them above everything else, so chat bubbles and critter name tags
    ///     floated on top of the pause menu.
    ///
    ///     Our things are annotations on the world (a nickname over a critter, a vote panel), so they
    ///     belong below the game's screens, exactly like the game's own world labels. A menu should
    ///     cover them.
    /// </summary>
    internal static class UiLayer
    {
        /// <summary>
        ///     Sort <paramref name="canvas"/> just below the game's UI. <paramref name="depth"/>
        ///     orders our own canvases against each other — bigger means further back.
        /// </summary>
        public static void PutBelowGameUi(Canvas canvas, int depth)
        {
            if (canvas != null)
            {
                canvas.sortingOrder = GameUiOrder() - depth;
            }
        }

        /// <summary>
        ///     The sorting order of the canvas the game's screens live on. Asked for rather than
        ///     hard-coded: a number that happens to work today is a number that breaks on an update.
        /// </summary>
        private static int GameUiOrder()
        {
            try
            {
                var manager = GameScreenManager.Instance;
                var overlay = manager != null ? manager.ssOverlayCanvas : null;
                if (overlay != null && overlay.TryGetComponent<Canvas>(out var canvas))
                {
                    return canvas.sortingOrder;
                }
            }
            catch
            {
                // Too early, or no game: 0 is where Unity puts a canvas by default, and being one
                // below that is still the right side of the game's UI.
            }

            return 0;
        }
    }
}
