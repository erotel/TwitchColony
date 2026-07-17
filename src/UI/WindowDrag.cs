using UnityEngine;
using UnityEngine.EventSystems;

namespace TwitchColony.UI
{
    /// <summary>
    ///     Makes a window draggable. Drop it on a dialog's root and the thing can be moved with the
    ///     mouse instead of sitting in the middle of the screen on top of whatever you wanted to look
    ///     at — which matters for the event list, since the whole point is watching what an event
    ///     does to the colony.
    ///
    ///     Unity walks up from whatever you grabbed to the first parent that handles dragging, so
    ///     this catches drags on the title, the background and the labels — but not inside the
    ///     scrolling list, where the scroll rect handles them first and scrolls instead. That split
    ///     is what you'd want anyway.
    /// </summary>
    internal sealed class WindowDrag : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        /// <summary>Told the new anchored position when the drag finishes, for anything that wants to remember it.</summary>
        public System.Action<Vector2> Moved;

        private Canvas root;

        public void OnDrag(PointerEventData eventData)
        {
            if (!(transform is RectTransform rect))
            {
                return;
            }

            // The mouse moves in screen pixels; the window lives in canvas units. They're only the
            // same thing at 100% UI scale, which is exactly the assumption that made our vote panel
            // a postage stamp at 4K.
            var scale = Scale();
            rect.anchoredPosition += eventData.delta / scale;
            KeepOnScreen(rect);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (transform is RectTransform rect)
            {
                Moved?.Invoke(rect.anchoredPosition);
            }
        }

        /// <summary>
        ///     Stop the window being dragged off the edge and lost. Clamping the pivot rather than the
        ///     whole rect keeps this honest for any anchoring: whatever the window is pinned to, the
        ///     point you grabbed stays on screen, so you can always drag it back.
        /// </summary>
        private void KeepOnScreen(RectTransform rect)
        {
            // In a screen-space overlay canvas, world position is screen pixels — no conversion, and
            // no assumptions about how this particular window is anchored.
            var pos = rect.position;
            pos.x = Mathf.Clamp(pos.x, 0f, Screen.width);
            pos.y = Mathf.Clamp(pos.y, 0f, Screen.height);
            rect.position = pos;
        }

        private float Scale()
        {
            if (root == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                root = canvas != null ? canvas.rootCanvas : null;
            }

            return root != null && root.scaleFactor > 0.01f ? root.scaleFactor : 1f;
        }
    }
}
