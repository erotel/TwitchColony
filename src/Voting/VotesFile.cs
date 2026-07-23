using System.IO;
using System.Text;
using TwitchColony.Config;
using UnityEngine;

namespace TwitchColony.Voting
{
    /// <summary>
    ///     Mirrors the live vote into a plain-text votes.txt next to config.json, for streaming
    ///     software: an OBS Text source with "Read from file" auto-refreshes, so the streamer can
    ///     style and place the vote display in their overlay — bigger font than the in-game panel,
    ///     anywhere on the canvas, and it survives scene switches. Off by default
    ///     (<see cref="ModConfig.WriteVotesFile"/>); the in-game panel can be turned off separately
    ///     (<see cref="ModConfig.ShowVoteHud"/>) so the streamer picks one, the other, or both.
    /// </summary>
    internal static class VotesFile
    {
        // What the file currently says, so unchanged content costs no disk write. Null means
        // "unknown" (nothing written this session), which forces the first write and thereby
        // overwrites whatever a previous session left behind.
        private static string lastWritten;
        private static float nextWriteAt;
        private static bool writeFailedLogged;

        internal static string FilePath => Path.Combine(
            Path.GetDirectoryName(ModConfig.ConfigPath) ?? "", "votes.txt");

        /// <summary>Called every frame by the vote controller. Throttled; writes only on change.</summary>
        internal static void Tick(VoteController vc)
        {
            if (!ModConfig.Instance.WriteVotesFile)
            {
                // Turned off mid-vote: blank the file once, or the overlay keeps showing a vote
                // that is no longer being displayed anywhere the streamer can see.
                if (!string.IsNullOrEmpty(lastWritten))
                {
                    WriteRaw("");
                }

                return;
            }

            if (Time.unscaledTime < nextWriteAt)
            {
                return;
            }

            nextWriteAt = Time.unscaledTime + 0.5f;
            WriteRaw(BuildContent(vc));
        }

        /// <summary>Blank the file when the colony goes away, so the overlay doesn't show a ghost vote.</summary>
        internal static void Clear()
        {
            if (!string.IsNullOrEmpty(lastWritten))
            {
                WriteRaw("");
            }
        }

        /// <summary>
        ///     The overlay text. Plain text on purpose — no TMP markup, no emoji: this is rendered by
        ///     OBS, not by the game, and the streamer does the styling there. Empty = hide (an empty
        ///     file draws nothing in a Text source).
        /// </summary>
        private static string BuildContent(VoteController vc)
        {
            if (vc == null || !ModConfig.Instance.EnableEvents)
            {
                return "";
            }

            if (vc.State == VoteController.VotingState.VoteInProgress)
            {
                var opts = vc.CurrentOptions;
                var tally = ModConfig.Instance.UseTwitchPolls ? null : vc.CurrentChatTally();

                var sb = new StringBuilder();
                for (var i = 0; i < opts.Count; i++)
                {
                    sb.Append(i + 1).Append(") ").Append(opts[i].DisplayName);
                    if (tally != null && i < tally.Length)
                    {
                        sb.Append("  (").Append(tally[i]).Append(')');
                    }

                    sb.Append('\n');
                }

                sb.Append(tally != null
                        ? "type " + ModConfig.Instance.VoteCommandPrefix + " N"
                        : "vote in the Twitch poll")
                    .Append("  -  ")
                    .Append(Mathf.CeilToInt(Mathf.Max(0f, vc.VoteTimeRemaining)))
                    .Append("s left");
                return sb.ToString();
            }

            // Same grace period as the in-game panel: show what won for a few seconds.
            if (!string.IsNullOrEmpty(vc.LastWinnerName) && Time.unscaledTime - vc.LastWinnerAt < 6f)
            {
                return "Winner: " + vc.LastWinnerName;
            }

            return "";
        }

        private static void WriteRaw(string content)
        {
            if (content == lastWritten)
            {
                return;
            }

            try
            {
                File.WriteAllText(FilePath, content);
                lastWritten = content;
                writeFailedLogged = false;
            }
            catch (System.Exception e)
            {
                // Log once per streak of failures (locked file, dead drive), not twice a second.
                if (!writeFailedLogged)
                {
                    writeFailedLogged = true;
                    Log.Warn("Could not write " + FilePath + ": " + e.Message);
                }
            }
        }
    }
}
