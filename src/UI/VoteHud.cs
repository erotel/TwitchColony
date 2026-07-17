using System.Text;
using TMPro;
using TwitchColony.Config;
using TwitchColony.Voting;
using UnityEngine;
using UnityEngine.UI;

namespace TwitchColony.UI
{
    /// <summary>
    ///     Small on-screen panel (top-centre) shown while a vote is running: the options, live chat
    ///     tallies, and the countdown. Independent implementation; screen-space overlay like the bubbles.
    /// </summary>
    public sealed class VoteHud : MonoBehaviour
    {
        private static VoteHud instance;

        private Canvas canvas;
        private GameObject panel;
        private TextMeshProUGUI text;

        // A transient banner (e.g. "NEW SUB") that takes over the panel for a few seconds.
        private static string flashText;
        private static float flashUntil;

        /// <summary>Show a short-lived banner in the HUD, independent of voting (e.g. a sub celebration).</summary>
        public static void Flash(string message, float seconds)
        {
            flashText = message;
            flashUntil = Time.unscaledTime + seconds;
        }

        public static void Ensure()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("TwitchColony.VoteHud");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<VoteHud>();
        }

        private void Build()
        {
            var go = new GameObject("TwitchColony.VoteHudCanvas");
            DontDestroyOnLoad(go);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29000;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            panel = new GameObject("Panel");
            panel.transform.SetParent(canvas.transform, false);

            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.72f);

            // A vertical layout + content-size fitter makes the panel hug the text, with padding.
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -12f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panel.transform, false);
            text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = false;
        }

        private void Update()
        {
            if (canvas == null)
            {
                Build();
            }

            var vc = VoteController.Instance;

            // A flash banner (sub celebration) takes priority and shows even if voting is off.
            string content;
            if (Time.unscaledTime < flashUntil)
            {
                content = flashText;
            }
            else
            {
                content = vc != null && ModConfig.Instance.EnableEvents ? BuildContent(vc) : null;
            }

            var show = content != null;

            if (panel.activeSelf != show)
            {
                panel.SetActive(show);
            }

            if (show)
            {
                // Same reason as the speech bubbles: the game's fonts have no emoji, and a missing
                // glyph draws as a hollow box. Rich-text markup is plain ASCII, so it survives.
                text.text = Glyphs.KeepRenderable(content, text);
            }
        }

        /// <summary>The panel text: the live vote while one runs, otherwise the winner banner for a few seconds.</summary>
        private static string BuildContent(VoteController vc)
        {
            if (vc.State == VoteController.VotingState.VoteInProgress)
            {
                var opts = vc.CurrentOptions;
                var tally = ModConfig.Instance.UseTwitchPolls ? null : vc.CurrentChatTally();

                var sb = new StringBuilder();
                sb.Append("VOTE  ").Append(Mathf.CeilToInt(vc.VoteTimeRemaining)).Append("s\n");
                for (var i = 0; i < opts.Count; i++)
                {
                    sb.Append('[').Append(i + 1).Append("] ").Append(opts[i].DisplayName);
                    if (tally != null && i < tally.Length)
                    {
                        sb.Append("  - ").Append(tally[i]);
                    }

                    sb.Append('\n');
                }

                sb.Append(tally != null ? "<size=70%>type !vote N in chat</size>" : "<size=70%>vote in the Twitch poll</size>");
                return sb.ToString();
            }

            // For a few seconds after a vote resolves, show what won.
            if (!string.IsNullOrEmpty(vc.LastWinnerName) && Time.unscaledTime - vc.LastWinnerAt < 6f)
            {
                return "<color=#8CE04A>WINNER</color>\n" + vc.LastWinnerName;
            }

            // Idle with a cycle-based auto-start pending: tell the streamer voting is coming.
            if (vc.State == VoteController.VotingState.NotStarted && vc.CyclesUntilStart > 0)
            {
                return "<size=80%>Twitch votes start in " + vc.CyclesUntilStart + " cycle" +
                       (vc.CyclesUntilStart == 1 ? "" : "s") + "</size>";
            }

            return null;
        }
    }
}
