using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using TwitchColony.Api;
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

        /// <summary>The events currently being voted on (empty when idle). For the on-screen HUD.</summary>
        public IReadOnlyList<GameEvent> CurrentOptions => options;

        /// <summary>Name of the most recently triggered winning event (for the HUD banner).</summary>
        public string LastWinnerName { get; private set; }

        /// <summary>Unscaled time when the last winner was announced.</summary>
        public float LastWinnerAt { get; private set; }

        private void AnnounceWinner(GameEvent ev)
        {
            if (ev == null)
            {
                return;
            }

            LastWinnerName = ev.DisplayName;
            LastWinnerAt = Time.unscaledTime;

            if (ModConfig.Instance.AnnounceInChat)
            {
                ChatSay?.Invoke("Winner: " + ev.DisplayName);
            }
        }

        /// <summary>Post the current vote's options to chat (if enabled and a chat sink is wired).</summary>
        private void AnnounceVoteInChat(ModConfig cfg)
        {
            if (!cfg.AnnounceInChat || ChatSay == null)
            {
                return;
            }

            var msg = new System.Text.StringBuilder("Vote! ");
            for (var i = 0; i < options.Count; i++)
            {
                msg.Append(i + 1).Append(": ").Append(options[i].DisplayName);
                if (i < options.Count - 1)
                {
                    msg.Append(" | ");
                }
            }

            msg.Append(cfg.UseTwitchPolls ? "  -> vote in the Twitch poll" : "  -> type " + cfg.VoteCommandPrefix + " N");
            ChatSay(msg.ToString());
        }

        /// <summary>Current chat-vote counts per option; index matches <see cref="CurrentOptions"/>.</summary>
        public int[] CurrentChatTally()
        {
            var tally = new int[options.Count];
            foreach (var kv in chatVotes)
            {
                if (kv.Value >= 0 && kv.Value < tally.Length)
                {
                    tally[kv.Value]++;
                }
            }

            return tally;
        }

        private List<GameEvent> options = new List<GameEvent>();

        // Chat-vote state: user -> chosen option index (last vote wins).
        private readonly Dictionary<string, int> chatVotes = new Dictionary<string, int>();

        // Poll state.
        private volatile string pollId;

        // Cycle at which the colony was first seen; used by the StartAfterCycles auto-start gate.
        private int startCycle = -1;
        private bool cycleGateLogged;

        /// <summary>
        ///     Cycles still to wait before the first vote auto-starts (StartAfterCycles), or 0 once the
        ///     gate has passed / isn't in use. For the on-screen HUD hint.
        /// </summary>
        public int CyclesUntilStart { get; private set; }

        /// <summary>Optional sink for posting messages to Twitch chat (wired from the IRC client).</summary>
        public System.Action<string> ChatSay;

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
        ///     Wipe every trace of the previous colony. This object is DontDestroyOnLoad, so it lives
        ///     for the whole session and would otherwise carry its old state into the next colony —
        ///     which left the machine parked in VoteDelay, greyed out the pause-menu button (it
        ///     disables while a vote is "active"), and made StartAfterCycles do nothing, since that
        ///     gate only ticks in the NotStarted state.
        /// </summary>
        public void ResetForNewColony()
        {
            State = VotingState.NotStarted;
            options = new List<GameEvent>();
            chatVotes.Clear();
            pollId = null;
            VoteTimeRemaining = 0f;
            VoteDelayRemaining = 0f;
            startCycle = -1;
            cycleGateLogged = false;
            CyclesUntilStart = 0;
            ChatSay = null;
            VotesFile.Clear();
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
            AnnounceVoteInChat(cfg);

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
            // This object is DontDestroyOnLoad, so it keeps ticking after the colony is gone — in
            // the main menu, during world generation, everywhere. A vote left running when the
            // colony unloaded would otherwise count down and fire its winning event into nothing
            // (seen in testing: "Hello fired on cycle -1", eleven seconds after "Colony unloaded").
            if (Game.Instance == null)
            {
                if (State != VotingState.NotStarted)
                {
                    ResetForNewColony();
                }

                return;
            }

            var cfg = ModConfig.Instance;

            // Auto-adoption runs independently of voting — it should tick even in "bubbles only" mode.
            CritterAdoption.Tick();

            // Keep votes.txt in step with the vote (before the EnableEvents early-return below, so
            // flipping events off mid-vote still blanks the overlay).
            VotesFile.Tick(this);

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
                    TickStartAfterCycles(cfg);
                    break;

                case VotingState.Error:
                    break;

                case VotingState.VoteInProgress:
                    if (VoteTimeRemaining > 0f)
                    {
                        // Unscaled: this is real streamer time, so the countdown ignores game speed —
                        // chat votes at the same rate whether the sim is paused or on triple speed.
                        // The pause SCREEN is different: the streamer has stepped away from the
                        // colony, and a vote shouldn't resolve (and fire an event) into a menu.
                        if (!IsGamePaused())
                        {
                            VoteTimeRemaining -= Time.unscaledDeltaTime;
                        }
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
                    else if (!StartVote())
                    {
                        // Couldn't fill a vote — everything eligible got filtered out for the moment.
                        // Wait out another delay and try again rather than parking in Error, which
                        // the state machine never leaves on its own: one starved draw would
                        // otherwise end voting for the rest of the session.
                        VoteDelayRemaining = cfg.VoteDelay;
                        State = VotingState.VoteDelay;
                    }

                    break;
            }
        }

        /// <summary>
        ///     While idle, auto-start the first vote once <c>StartAfterCycles</c> cycles have elapsed
        ///     since the colony loaded. Disabled (0) leaves the first start to the pause-menu button.
        ///     The button still works to start earlier.
        /// </summary>
        private void TickStartAfterCycles(ModConfig cfg)
        {
            if (cfg.StartAfterCycles <= 0)
            {
                CyclesUntilStart = 0;
                return;
            }

            var cycle = CurrentCycle();
            if (cycle < 0)
            {
                return; // GameClock not ready yet.
            }

            // Anchor to the cycle we first saw, so "after N cycles" is relative to load — works whether
            // it's a fresh colony (cycle 0) or a save loaded mid-game.
            if (startCycle < 0)
            {
                startCycle = cycle;
            }

            var elapsed = cycle - startCycle;
            CyclesUntilStart = Mathf.Max(0, cfg.StartAfterCycles - elapsed);

            if (!cycleGateLogged)
            {
                cycleGateLogged = true;
                Log.Info($"Voting will auto-start in {CyclesUntilStart} cycle(s) (StartAfterCycles={cfg.StartAfterCycles}).");
            }

            if (elapsed >= cfg.StartAfterCycles)
            {
                Log.Info("StartAfterCycles reached; starting first vote.");
                StartVote();
            }
        }

        /// <summary>Current colony cycle (0-based), or -1 if the game clock isn't available yet.</summary>
        private static int CurrentCycle()
        {
            try
            {
                var clock = GameClock.Instance;
                return clock != null ? clock.GetCycle() : -1;
            }
            catch
            {
                return -1;
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
            AnnounceWinner(options[best]);

            var voters = new List<string>();
            foreach (var kv in chatVotes)
            {
                if (kv.Value == best)
                {
                    voters.Add(kv.Key);
                }
            }

            TriggerSafely(options[best],
                BuildContext(options[best], EventContext.SourceChatVote, tally[best], voters.ToArray()));
        }

        /// <summary>
        ///     What an event's action is told about the vote that ran it. A plain dictionary of BCL
        ///     types on purpose — see <see cref="EventContext"/>. Add keys freely; never remove one,
        ///     add-ons read them by name.
        /// </summary>
        private static Dictionary<string, object> BuildContext(GameEvent ev, string source, int votes,
            string[] voters)
        {
            return new Dictionary<string, object>
            {
                { EventContext.EventId, ev.Id },
                { EventContext.Cycle, CurrentCycle() },
                { EventContext.Source, source },
                { EventContext.VoteCount, votes },
                { EventContext.Voters, voters ?? new string[0] },
            };
        }

        /// <summary>Run an event's effect without letting a thrown exception stall the state machine.</summary>
        private static void TriggerSafely(GameEvent ev, object context)
        {
            // Note it before running: an event that throws still counts as "just happened" for the
            // group cooldown, or a broken event would keep being offered.
            EventRegistry.NoteTriggered(ev);

            try
            {
                ev.Trigger(context);
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
                        AnnounceWinner(captured[0]);
                        TriggerSafely(captured[0], BuildContext(captured[0], EventContext.SourcePoll, 0, null));
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
                    AnnounceWinner(captured[best]);

                    // Twitch reports vote totals, not who voted — hence no voters here.
                    TriggerSafely(captured[best],
                        BuildContext(captured[best], EventContext.SourcePoll, poll.Choices[best].Votes, null));
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
