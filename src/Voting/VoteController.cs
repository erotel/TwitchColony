using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using TwitchColony.Config;
using TwitchColony.Events;
using TwitchColony.Twitch;
using UnityEngine;

namespace TwitchColony.Voting
{
    /// <summary>
    ///     Drives voting as a small state machine: it offers a random set of events, collects votes
    ///     (chat counting or a native Twitch poll), triggers the winner, then waits <c>VoteDelay</c>
    ///     seconds and starts the next round automatically. The very first round is kicked off by the
    ///     pause-menu button (see <see cref="PauseMenuPatches"/>). One instance lives per game session.
    ///     Independent implementation.
    /// </summary>
    public sealed class VoteController : MonoBehaviour
    {
        public enum VotingState
        {
            NotStarted,     // Idle; waiting for the pause-menu button to start the first vote.
            VoteInProgress, // A vote is open and collecting chat/poll votes.
            VoteDelay,      // Cooldown between votes; auto-starts the next one when it elapses.
            Error,          // Something went wrong (e.g. no events); machine parked until restarted.
        }

        private static VoteController instance;

        /// <summary>The live controller for this session (null before the colony loads).</summary>
        public static VoteController Instance => instance;

        public VotingState State { get; private set; } = VotingState.NotStarted;

        /// <summary>True while the machine is running a vote or its delay (used to gate the menu button).</summary>
        public bool IsVoteActive => State != VotingState.NotStarted && State != VotingState.Error;

        public float VoteTimeRemaining { get; private set; }
        public float VoteDelayRemaining { get; private set; }

        private List<GameEvent> options = new List<GameEvent>();

        // Chat-vote state: user -> chosen option index (last vote wins).
        private readonly Dictionary<string, int> chatVotes = new Dictionary<string, int>();

        // Poll state.
        private volatile string pollId;

        public static VoteController Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var go = new GameObject("TwitchColony.VoteController");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<VoteController>();
            return instance;
        }

        /// <summary>
        ///     Begin a new vote. Returns whether it actually started. Safe to call from the menu button;
        ///     the state machine also calls it automatically when the delay between votes elapses.
        /// </summary>
        public bool StartVote()
        {
            var cfg = ModConfig.Instance;
            if (!cfg.EnableEvents)
            {
                return false;
            }

            // Never stack a vote on top of a running one.
            if (State == VotingState.VoteInProgress)
            {
                return false;
            }

            options = EventRegistry.PickForVote(Mathf.Clamp(cfg.OptionsPerVote, 2, 5));
            if (options.Count < 2)
            {
                Log.Warn("Not enough events registered to vote.");
                State = VotingState.Error;
                return false;
            }

            chatVotes.Clear();
            pollId = null;
            VoteTimeRemaining = cfg.VotingSeconds;
            State = VotingState.VoteInProgress;

            var sb = new System.Text.StringBuilder("Voting started: ");
            for (var i = 0; i < options.Count; i++)
            {
                sb.Append($"[{i + 1}] {options[i].DisplayName}  ");
            }

            Log.Info(sb.ToString());

            if (cfg.UseTwitchPolls)
            {
                StartTwitchPoll(cfg);
            }

            return true;
        }

        /// <summary>Feed a chat message into the current vote (chat-counting mode).</summary>
        public void FeedChat(string user, string text)
        {
            if (State != VotingState.VoteInProgress || ModConfig.Instance.UseTwitchPolls || string.IsNullOrEmpty(text))
            {
                return;
            }

            var cfg = ModConfig.Instance;
            if (!text.StartsWith(cfg.VoteCommandPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var rest = text.Substring(cfg.VoteCommandPrefix.Length).Trim();
            if (int.TryParse(rest, out var n) && n >= 1 && n <= options.Count)
            {
                chatVotes[user] = n - 1;
            }
        }

        private void Update()
        {
            var cfg = ModConfig.Instance;

            // "Bubbles only" mode (events disabled): park the machine back to idle.
            if (!cfg.EnableEvents)
            {
                if (State != VotingState.NotStarted && State != VotingState.Error)
                {
                    State = VotingState.NotStarted;
                    options.Clear();
                    chatVotes.Clear();
                }

                return;
            }

            switch (State)
            {
                case VotingState.NotStarted:
                case VotingState.Error:
                    break;

                case VotingState.VoteInProgress:
                    if (VoteTimeRemaining > 0f)
                    {
                        // Unscaled: this is real streamer time, not game time.
                        VoteTimeRemaining -= Time.unscaledDeltaTime;
                    }
                    else
                    {
                        FinishVote();
                    }

                    break;

                case VotingState.VoteDelay:
                    if (VoteDelayRemaining > 0f)
                    {
                        // Freeze the countdown while the game is paused, so the delay is real playtime.
                        if (!IsGamePaused())
                        {
                            VoteDelayRemaining -= Time.unscaledDeltaTime;
                        }
                    }
                    else
                    {
                        StartVote();
                    }

                    break;
            }
        }

        /// <summary>Resolve the current vote and enter the delay before the next one auto-starts.</summary>
        private void FinishVote()
        {
            var cfg = ModConfig.Instance;

            if (cfg.UseTwitchPolls && pollId != null)
            {
                FinishFromPoll();
            }
            else
            {
                FinishFromChat();
            }

            // Enter the cooldown; Update() restarts the loop from here once it elapses.
            VoteDelayRemaining = cfg.VoteDelay;
            State = VotingState.VoteDelay;
        }

        private void FinishFromChat()
        {
            var tally = new int[options.Count];
            foreach (var kv in chatVotes)
            {
                tally[kv.Value]++;
            }

            var best = 0;
            for (var i = 1; i < tally.Length; i++)
            {
                if (tally[i] > tally[best])
                {
                    best = i;
                }
            }

            Log.Info($"Chat winner: {options[best].DisplayName} ({tally[best]} votes)");
            TriggerSafely(options[best]);
        }

        /// <summary>Run an event's effect without letting a thrown exception stall the state machine.</summary>
        private static void TriggerSafely(GameEvent ev)
        {
            try
            {
                ev.Trigger();
            }
            catch (System.Exception e)
            {
                Log.Warn($"Event '{ev.DisplayName}' threw: {e.Message}");
            }
        }

        private void StartTwitchPoll(ModConfig cfg)
        {
            var titles = new List<string>();
            foreach (var o in options)
            {
                titles.Add(o.DisplayName);
            }

            var clientId = string.IsNullOrEmpty(cfg.ClientIdOverride) ? TwitchAuth.ClientId : cfg.ClientIdOverride;
            var broadcaster = string.IsNullOrEmpty(cfg.BroadcasterIdOverride) ? TwitchAuth.UserId : cfg.BroadcasterIdOverride;
            var token = TwitchAuth.Token;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(broadcaster) || string.IsNullOrEmpty(token))
            {
                Log.Warn("Poll mode: missing client id / broadcaster id / token. Falling back to chat voting.");
                return;
            }

            var thread = new Thread(() =>
            {
                var poll = HelixPolls.CreatePoll(cfg.HelixBaseUrl, clientId, broadcaster, token,
                    "Colony event", titles, cfg.VotingSeconds);
                if (poll != null)
                {
                    pollId = poll.Id;
                    Log.Info($"Created poll {poll.Id} with {poll.Choices.Count} choices.");
                }
            }) { IsBackground = true };
            thread.Start();
        }

        private void FinishFromPoll()
        {
            var cfg = ModConfig.Instance;
            var clientId = string.IsNullOrEmpty(cfg.ClientIdOverride) ? TwitchAuth.ClientId : cfg.ClientIdOverride;
            var broadcaster = string.IsNullOrEmpty(cfg.BroadcasterIdOverride) ? TwitchAuth.UserId : cfg.BroadcasterIdOverride;
            var token = TwitchAuth.Token;
            var id = pollId;
            var captured = new List<GameEvent>(options);

            var thread = new Thread(() =>
            {
                var poll = HelixPolls.GetPoll(cfg.HelixBaseUrl, clientId, broadcaster, token, id);
                MainThread.Run(() =>
                {
                    if (poll == null || poll.Choices.Count == 0)
                    {
                        Log.Warn("Poll returned no results; picking first option.");
                        TriggerSafely(captured[0]);
                        return;
                    }

                    var best = 0;
                    for (var i = 1; i < poll.Choices.Count && i < captured.Count; i++)
                    {
                        if (poll.Choices[i].Votes > poll.Choices[best].Votes)
                        {
                            best = i;
                        }
                    }

                    Log.Info($"Poll winner: {captured[best].DisplayName} ({poll.Choices[best].Votes} votes)");
                    TriggerSafely(captured[best]);
                });
            }) { IsBackground = true };
            thread.Start();
        }

        /// <summary>True when the in-game pause screen is showing, so the delay countdown can be frozen.</summary>
        private static bool IsGamePaused()
        {
            try
            {
                var ps = PauseScreen.Instance;
                return ps != null && Traverse.Create(ps).Field<bool>("shown").Value;
            }
            catch
            {
                return false;
            }
        }
    }
}
