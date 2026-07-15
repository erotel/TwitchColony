using System.Collections.Generic;
using System.Threading;
using TwitchColony.Config;
using TwitchColony.Events;
using TwitchColony.Twitch;
using UnityEngine;

namespace TwitchColony.Voting
{
    /// <summary>
    ///     Drives a round of voting: offers a random set of events, collects votes (chat counting or a
    ///     native Twitch poll), then triggers the winning event. One instance lives for the game session.
    ///     Independent implementation.
    /// </summary>
    public sealed class VoteController : MonoBehaviour
    {
        private static VoteController instance;

        private bool voting;
        private float endTime;
        private List<GameEvent> options = new List<GameEvent>();

        // Chat-vote state: user -> chosen option index (last vote wins).
        private readonly Dictionary<string, int> chatVotes = new Dictionary<string, int>();

        // Poll state.
        private volatile string pollId;
        private volatile bool pollResultPending;

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

        public bool IsVoting => voting;

        /// <summary>Begin a new vote if one isn't already running.</summary>
        public void StartVote()
        {
            var cfg = ModConfig.Instance;
            if (voting || !cfg.EnableEvents)
            {
                return;
            }

            options = EventRegistry.PickForVote(Mathf.Clamp(cfg.OptionsPerVote, 2, 5));
            if (options.Count < 2)
            {
                Log.Warn("Not enough events registered to vote.");
                return;
            }

            chatVotes.Clear();
            pollId = null;
            pollResultPending = false;
            voting = true;
            endTime = Time.unscaledTime + cfg.VotingSeconds;

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
        }

        /// <summary>Feed a chat message into the current vote (chat-counting mode).</summary>
        public void FeedChat(string user, string text)
        {
            if (!voting || ModConfig.Instance.UseTwitchPolls || string.IsNullOrEmpty(text))
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
            if (!voting || Time.unscaledTime < endTime)
            {
                return;
            }

            voting = false;

            if (ModConfig.Instance.UseTwitchPolls && pollId != null)
            {
                FinishFromPoll();
            }
            else
            {
                FinishFromChat();
            }
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
            options[best].Trigger();
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
            if (pollResultPending)
            {
                return;
            }

            pollResultPending = true;
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
                        captured[0].Trigger();
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
                    captured[best].Trigger();
                });
            }) { IsBackground = true };
            thread.Start();
        }
    }
}
