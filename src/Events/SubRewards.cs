using System.Collections.Generic;
using TwitchColony.Config;
using TwitchColony.Twitch;
using UnityEngine;

namespace TwitchColony.Events
{
    /// <summary>
    ///     Fires a positive colony event when someone subscribes / resubscribes / gifts a sub. A short
    ///     cooldown coalesces sub-trains so a burst of gifts can't flood the colony with events. Runs on
    ///     the main thread (the effect touches game API). Independent implementation.
    /// </summary>
    public static class SubRewards
    {
        // Curated "nice" events (must match ids registered in EventRegistry.RegisterDefaults).
        private static readonly string[] GoodEventIds =
        {
            "heal_all", "cure_diseases", "nirvana", "charge_batteries", "oxygen_wave", "free_harvest",
            "stress_relief", "feast", "research_boost", "spawn_atmo_suit", "spawn_vacillator",
            "rain_gold", "rain_diamond", "rain_material", "skill_points", "surprise_box",
        };

        private static float lastRewardAt = -9999f;

        /// <summary>Optional sink for posting a thank-you to Twitch chat (wired from the IRC client).</summary>
        public static System.Action<string> ChatSay;

        /// <summary>React to a sub notice: trigger a random positive event (rate-limited).</summary>
        public static void Handle(SubNotice sub)
        {
            var cfg = ModConfig.Instance;
            if (!cfg.EnableSubRewards || sub == null)
            {
                return;
            }

            // Coalesce sub-trains / mass gifts so we don't fire dozens of events at once.
            var now = Time.unscaledTime;
            if (now - lastRewardAt < cfg.SubRewardCooldownSeconds)
            {
                Log.Info($"Sub from {sub.Display} noted, but reward is on cooldown.");
                return;
            }

            var ev = PickGoodEvent();
            if (ev == null)
            {
                Log.Warn("Sub reward: no positive event is registered.");
                return;
            }

            lastRewardAt = now;
            var kind = sub.IsGift ? "gifted sub" : sub.IsResub ? "resub" : "sub";
            Log.Info($"Sub reward for {sub.Display} ({kind}, Tier {sub.Tier}): {ev.DisplayName}");
            TriggerSafely(ev);

            if (cfg.AnnounceInChat)
            {
                ChatSay?.Invoke($"Thanks for the {kind}, {sub.Display}! Colony reward: {ev.DisplayName}");
            }
        }

        private static GameEvent PickGoodEvent()
        {
            var pool = new List<GameEvent>();
            foreach (var id in GoodEventIds)
            {
                var e = EventRegistry.ById(id);
                if (e != null)
                {
                    pool.Add(e);
                }
            }

            if (pool.Count == 0)
            {
                return null;
            }

            return pool[Random.Range(0, pool.Count)];
        }

        private static void TriggerSafely(GameEvent ev)
        {
            try
            {
                ev.Trigger();
            }
            catch (System.Exception e)
            {
                Log.Warn($"Sub-reward event '{ev.DisplayName}' threw: {e.Message}");
            }
        }
    }
}
