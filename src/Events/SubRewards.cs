using TwitchColony.Config;
using TwitchColony.Twitch;
using TwitchColony.UI;
using UnityEngine;

namespace TwitchColony.Events
{
    /// <summary>
    ///     Celebrates a sub / resub / gifted sub: shows a "NEW SUB" banner and makes the duplicants cheer
    ///     (clap / thumbs-up / sing) — no gameplay change. A short cooldown coalesces sub-trains. Runs on
    ///     the main thread (touches game API). Independent implementation.
    /// </summary>
    public static class SubRewards
    {
        private static float lastRewardAt = -9999f;

        /// <summary>Optional sink for posting a thank-you to Twitch chat (wired from the IRC client).</summary>
        public static System.Action<string> ChatSay;

        /// <summary>React to a sub notice: banner + duplicant cheer (rate-limited).</summary>
        public static void Handle(SubNotice sub)
        {
            var cfg = ModConfig.Instance;
            if (!cfg.EnableSubRewards || sub == null)
            {
                return;
            }

            // Coalesce sub-trains / mass gifts so we don't restart every dupe's emote dozens of times.
            var now = Time.unscaledTime;
            if (now - lastRewardAt < cfg.SubRewardCooldownSeconds)
            {
                Log.Info($"Sub from {sub.Display} noted, but celebration is on cooldown.");
                return;
            }

            lastRewardAt = now;

            var kind = sub.IsGift ? "gifted sub" : sub.IsResub ? "resub" : "sub";
            Log.Info($"Sub celebration for {sub.Display} ({kind}, Tier {sub.Tier}).");

            VoteHud.Flash(BuildBanner(sub), 6f);
            CheerAllDupes();

            if (cfg.AnnounceInChat)
            {
                ChatSay?.Invoke($"Thanks for the {kind}, {sub.Display}! The dupes are cheering \\o/");
            }
        }

        private static string BuildBanner(SubNotice sub)
        {
            var line2 = sub.Display;
            if (sub.IsResub && sub.CumulativeMonths > 0)
            {
                line2 += $"  <size=70%>{sub.CumulativeMonths} months</size>";
            }
            else if (sub.IsGift)
            {
                line2 += "  <size=70%>gifted a sub</size>";
            }

            return "<color=#C287FF>★ NEW SUB ★</color>\n" + line2;
        }

        /// <summary>Make every living duplicant play a random celebratory emote (clap / cheer / sing).</summary>
        private static void CheerAllDupes()
        {
            var items = Components.LiveMinionIdentities?.Items;
            if (items == null)
            {
                return;
            }

            var db = Db.Get();
            var emotes = new[]
            {
                db.Emotes.Minion.ClapCheer,
                db.Emotes.Minion.Cheer,
                db.Emotes.Minion.ThumbsUp,
                db.Emotes.Minion.FingerGuns,
                db.Emotes.Minion.Sing,
            };
            var emote = emotes[Random.Range(0, emotes.Length)];

            foreach (var identity in items)
            {
                if (identity == null)
                {
                    continue;
                }

                var provider = identity.GetComponent<ChoreProvider>();
                if (provider == null)
                {
                    continue;
                }

                try
                {
                    // High-priority emote chore preempts most work so the cheer plays promptly.
                    _ = new EmoteChore(provider, db.ChoreTypes.EmoteHighPriority, emote);
                }
                catch (System.Exception e)
                {
                    Log.Warn("Emote chore failed: " + e.Message);
                }
            }
        }
    }
}
