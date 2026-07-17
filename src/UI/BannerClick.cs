using UnityEngine;
using UnityEngine.EventSystems;

namespace TwitchColony.UI
{
    /// <summary>
    ///     Makes the vote panel clickable, so a banner that knows where its event happened can take
    ///     the streamer there.
    ///
    ///     It sits alongside <see cref="WindowDrag"/> on the same panel and they don't fight: Unity
    ///     stops treating a press as a click once it turns into a drag, so moving the panel never
    ///     yanks the camera at the end of it.
    /// </summary>
    internal sealed class BannerClick : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            {
                VoteHud.PanToFlash();
            }
        }
    }
}
