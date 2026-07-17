using System;
using System.Collections.Generic;
using TwitchColony.Config;
using TwitchColony.UI;
using UnityEngine;

namespace TwitchColony.Events
{
    /// <summary>
    ///     Names critters after Twitch viewers. Two ways in: a viewer types the adopt command, or —
    ///     if auto-adopt is on — the mod quietly names free critters after random recent chatters on
    ///     a timer. Nearest the printing pod first, working outward. A named critter shows chat
    ///     bubbles just like a duplicant (see <see cref="SpeechBubbles"/>).
    ///
    ///     Independent implementation. Everything here touches game objects and MUST run on the main
    ///     thread. Names are display-only and do not survive save/load — critters have no persistent
    ///     name in the base game; auto-adopt re-populates a fresh colony instead.
    /// </summary>
    public static class CritterAdoption
    {
        // Viewer nick (lower-case) -> the critter they adopted, so one viewer keeps one critter.
        // Destroyed critters read back as Unity "null", which lets the viewer adopt again.
        private static readonly Dictionary<string, GameObject> AdoptedByUser =
            new Dictionary<string, GameObject>();

        // Recent chat participants (lower-case nick -> last time we saw them), the pool auto-adopt
        // names critters after. Pruned to a rolling window so we name active chatters, not someone
        // who said one word an hour ago.
        private static readonly Dictionary<string, float> RecentChatters = new Dictionary<string, float>();
        private const float ChatterWindowSeconds = 15f * 60f;

        private static float nextAutoAdoptAt;

        /// <summary>Optional sink for posting confirmations to Twitch chat (wired from the IRC client).</summary>
        public static System.Action<string> ChatSay;

        /// <summary>Clear all state (called when a colony loads/unloads; old critters are gone).</summary>
        public static void Reset()
        {
            AdoptedByUser.Clear();
            RecentChatters.Clear();
            CritterNameTags.Clear();
            // Give the streamer a moment after load before the first automatic naming.
            nextAutoAdoptAt = Time.unscaledTime + 10f;
        }

        /// <summary>Remember that someone spoke, so auto-adopt has a pool to name critters after.</summary>
        public static void NoteChatter(string user)
        {
            var key = (user ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(key))
            {
                RecentChatters[key] = Time.unscaledTime;
            }
        }

        /// <summary>If the chat line is the adopt command, adopt a free critter for the viewer.</summary>
        public static void TryHandle(string user, string text)
        {
            var cfg = ModConfig.Instance;
            if (!cfg.EnableCritterAdopt || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (string.IsNullOrEmpty(cfg.AdoptCommand) ||
                !text.Trim().StartsWith(cfg.AdoptCommand, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AdoptManual(user.Trim(), cfg);
        }

        /// <summary>
        ///     Called every frame from the vote controller. When auto-adopt is on and the timer is up,
        ///     names one free critter after one free chatter. One per interval keeps it from flooding.
        /// </summary>
        public static void Tick()
        {
            var cfg = ModConfig.Instance;
            if (!cfg.EnableCritterAdopt || !cfg.EnableAutoAdopt || Time.unscaledTime < nextAutoAdoptAt)
            {
                return;
            }

            // Reschedule whatever happens, so a colony with no free critters or an empty chat doesn't
            // retry every single frame.
            nextAutoAdoptAt = Time.unscaledTime + Mathf.Max(5, cfg.AutoAdoptIntervalSeconds);

            var user = PickFreeChatter();
            if (user == null)
            {
                return; // nobody in chat who doesn't already have a body
            }

            var critter = PickFreeCritter();
            if (critter == null)
            {
                return; // nothing left to name
            }

            NameCritter(user, critter, cfg, auto: true);
        }

        /// <summary>The critter a viewer currently owns, or null. Used by the speech bubbles.</summary>
        public static GameObject FindAdoptedCritter(string user)
        {
            var key = (user ?? "").Trim().ToLowerInvariant();
            if (AdoptedByUser.TryGetValue(key, out var go) && go != null)
            {
                return go;
            }

            return null;
        }

        private static void AdoptManual(string user, ModConfig cfg)
        {
            if (AlreadyHasBody(user))
            {
                // Owns a critter already, or is a duplicant: one viewer, one body in the colony.
                if (SpeechBubbles.FindMinionByName(user) != null && cfg.AnnounceInChat)
                {
                    ChatSay?.Invoke($"{user} is already a duplicant here!");
                }

                return;
            }

            var critter = PickFreeCritter();
            if (critter == null)
            {
                Log.Info($"{user} tried to adopt, but no free critter is available.");
                if (cfg.AnnounceInChat)
                {
                    ChatSay?.Invoke($"Sorry {user}, no free critters right now!");
                }

                return;
            }

            NameCritter(user, critter, cfg, auto: false);
        }

        /// <summary>The naming itself, shared by manual and automatic adoption.</summary>
        private static void NameCritter(string user, GameObject critter, ModConfig cfg, bool auto)
        {
            var sel = critter.GetComponent<KSelectable>();
            if (sel == null)
            {
                return;
            }

            var species = Util.StripTextFormatting(sel.GetName() ?? "critter").Trim();

            sel.SetName(user); // plain nick, so bubble matching by name is straightforward
            AdoptedByUser[user.ToLowerInvariant()] = critter;

            Log.Info($"{user} {(auto ? "was given" : "adopted")} a {species}.");
            SpeechBubbles.ShowRaw(critter.transform, "adopted by " + user);
            if (cfg.ShowAdoptedNameTag)
            {
                CritterNameTags.Show(critter.transform, user);
            }

            if (cfg.AnnounceInChat)
            {
                ChatSay?.Invoke(auto
                    ? $"{user} has been given a {species}! Say hi \\o/"
                    : $"{user} adopted a {species}!");
            }
        }

        /// <summary>True if this viewer already has a critter or a duplicant carrying their nick.</summary>
        private static bool AlreadyHasBody(string user)
        {
            return FindAdoptedCritter(user) != null || SpeechBubbles.FindMinionByName(user) != null;
        }

        /// <summary>A random recent chatter who doesn't already have a body, or null.</summary>
        private static string PickFreeChatter()
        {
            var cutoff = Time.unscaledTime - ChatterWindowSeconds;
            var free = new List<string>();

            foreach (var pair in new List<KeyValuePair<string, float>>(RecentChatters))
            {
                if (pair.Value < cutoff)
                {
                    RecentChatters.Remove(pair.Key); // aged out of the window
                }
                else if (!AlreadyHasBody(pair.Key))
                {
                    free.Add(pair.Key);
                }
            }

            return free.Count == 0 ? null : free[UnityEngine.Random.Range(0, free.Count)];
        }

        /// <summary>
        ///     The free critter nearest the printing pod, so naming fills in from the base outward
        ///     instead of tagging something off in a far corner nobody's watching. Null if none free.
        /// </summary>
        private static GameObject PickFreeCritter()
        {
            var taken = new HashSet<GameObject>();
            foreach (var kv in AdoptedByUser)
            {
                if (kv.Value != null)
                {
                    taken.Add(kv.Value);
                }
            }

            var brains = Components.Brains?.Items;
            if (brains == null)
            {
                return null;
            }

            var pods = PrintingPods();
            GameObject best = null;
            var bestDistance = float.MaxValue;

            foreach (var brain in brains)
            {
                if (!(brain is CreatureBrain))
                {
                    continue; // critters only, not duplicant brains
                }

                var go = brain.gameObject;
                if (go == null || taken.Contains(go) || go.GetComponent<KSelectable>() == null)
                {
                    continue;
                }

                var distance = NearestPodDistance(go.transform.position, pods);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = go;
                }
            }

            return best;
        }

        private static List<Vector3> PrintingPods()
        {
            var pods = new List<Vector3>();
            try
            {
                var items = Components.Telepads?.Items;
                if (items != null)
                {
                    foreach (var pad in items)
                    {
                        if (pad != null)
                        {
                            pods.Add(pad.transform.position);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't read the printing pod position: " + e.Message);
            }

            return pods;
        }

        /// <summary>Distance to the closest pod, or 0 when there are none (so order stays stable).</summary>
        private static float NearestPodDistance(Vector3 position, List<Vector3> pods)
        {
            if (pods.Count == 0)
            {
                return 0f;
            }

            var best = float.MaxValue;
            foreach (var pod in pods)
            {
                var d = (position - pod).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }
    }
}
