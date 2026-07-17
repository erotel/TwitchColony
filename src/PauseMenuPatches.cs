using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TwitchColony.Voting;
using UnityEngine;

namespace TwitchColony
{
    /// <summary>
    ///     Adds a "Start Twitch Votes" button to the in-game pause menu. Pressing it starts the first
    ///     vote; from then on <see cref="VoteController"/>'s state machine restarts votes on its own
    ///     delay, so the button only needs to kick things off. The button is disabled while a vote is
    ///     active. Independent implementation (same idea as the original mod, our own code).
    /// </summary>
    internal static class PauseMenuPatches
    {
        // global::Action is the game's key-binding enum (it shadows System.Action). NumActions = "no key".
        private static readonly KButtonMenu.ButtonInfo TwitchButtonInfo = new KButtonMenu.ButtonInfo(
            "Start Twitch Votes",
            global::Action.NumActions,
            OnTwitchButtonPressed
        );

        private static ColorStyleSetting twitchButtonStyle;

        private static readonly Color DisabledColor = new Color32(0x6A, 0x69, 0x66, 0xFF);
        private static readonly Color InactiveColor = new Color32(0x91, 0x46, 0xFF, 0xFF);
        private static readonly Color HoverColor = new Color32(0xA2, 0x56, 0xFF, 0xFF);
        private static readonly Color PressedColor = new Color32(0xB5, 0x67, 0xFF, 0xFF);

        /// <summary>
        ///     Set between the click and the vote actually starting a frame later. Without it the
        ///     button springs back to enabled the moment we refresh it: RefreshButtons recomputes the
        ///     state from "is a vote running?", and a frame after the click the answer is still no.
        /// </summary>
        private static bool startPending;

        private static void OnTwitchButtonPressed()
        {
            if (startPending)
            {
                return; // Already on its way; a second click must not stack another vote.
            }

            startPending = true;
            RefreshButtonState();

            // StartVote touches the game; run it next frame on the main thread.
            GameScheduler.Instance.ScheduleNextFrame(
                "TwitchColony.StartVotes",
                _ =>
                {
                    VoteController.Ensure().StartVote();
                    // Whether it worked or not, the state now follows the controller: a started vote
                    // keeps the button disabled, a failed one (no events to offer) re-enables it so
                    // the streamer can try again.
                    startPending = false;
                    RefreshButtonState();
                }
            );
        }

        /// <summary>Recompute the button's enabled state and push it to the screen if it's open.</summary>
        private static void RefreshButtonState()
        {
            TwitchButtonInfo.isEnabled = !startPending &&
                                         (VoteController.Instance == null || !VoteController.Instance.IsVoteActive);

            if (PauseScreen.Instance != null)
            {
                PauseScreen.Instance.RefreshButtons();
            }
        }

        /// <summary>Insert our button into the pause menu each time it is (re)built.</summary>
        [HarmonyPatch(typeof(PauseScreen), "ConfigureButtonInfos")]
        // ReSharper disable once InconsistentNaming
        private static class PauseScreen_ConfigureButtonInfos_Patch
        {
            // ReSharper disable once UnusedMember.Local
            // ___buttons is the private button list on the menu; copy, insert, and reassign it.
            private static void Postfix(PauseScreen __instance, ref IList<KButtonMenu.ButtonInfo> ___buttons)
            {
                var buttons = ___buttons.ToList();

                // ConfigureButtonInfos runs every time the menu is rebuilt; don't add our button twice.
                if (buttons.Contains(TwitchButtonInfo))
                {
                    return;
                }

                var idx = System.Math.Min(4, buttons.Count); // sit near the top, but never past the end
                buttons.Insert(idx, TwitchButtonInfo);
                __instance.SetButtons(buttons);
            }
        }

        /// <summary>Keep the button's enabled state in sync with whether a vote is running, and style it.</summary>
        [HarmonyPatch(typeof(KButtonMenu), nameof(KButtonMenu.RefreshButtons))]
        // ReSharper disable once InconsistentNaming
        private static class KButtonMenu_RefreshButtons_Patch
        {
            // ReSharper disable once UnusedMember.Local
            private static void Prefix(KButtonMenu __instance)
            {
                if (!(__instance is PauseScreen) || TwitchButtonInfo.uibutton == null)
                {
                    return;
                }

                // startPending covers the frame between the click and the vote existing; without it
                // this line would hand the button straight back to the streamer mid-click.
                TwitchButtonInfo.isEnabled = !startPending &&
                                             (VoteController.Instance == null ||
                                              !VoteController.Instance.IsVoteActive);

                // Create the purple style once (or if it was reset), and apply it to the button.
                if (twitchButtonStyle == null ||
                    TwitchButtonInfo.uibutton.bgImage.colorStyleSetting != twitchButtonStyle)
                {
                    twitchButtonStyle = ScriptableObject.CreateInstance<ColorStyleSetting>();
                    twitchButtonStyle.disabledColor = DisabledColor;
                    twitchButtonStyle.inactiveColor = InactiveColor;
                    twitchButtonStyle.hoverColor = HoverColor;
                    twitchButtonStyle.activeColor = PressedColor;
                    TwitchButtonInfo.uibutton.bgImage.colorStyleSetting = twitchButtonStyle;
                }
            }
        }
    }
}
