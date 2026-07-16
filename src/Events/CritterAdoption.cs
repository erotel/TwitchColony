using System;
using System.Collections.Generic;
using TwitchColony.Config;
using TwitchColony.UI;
using UnityEngine;

namespace TwitchColony.Events
{
    /// <summary>
    ///     Lets Twitch viewers "adopt" a critter with a chat command (default <c>!adopt</c>): a random
    ///     un-adopted critter is renamed to the viewer's Twitch nick. Named critters then also show chat
    ///     bubbles, exactly like duplicants (see <see cref="SpeechBubbles"/>). Independent implementation.
    ///     All methods here touch game objects and MUST be called on the main thread.
    /// </summary>
    public static class CritterAdoption
    {
        // Viewer nick (lower-case) -> the critter they adopted, so one viewer keeps one critter.
        // Destroyed critters read back as Unity "null", which lets the viewer adopt again.
        private static readonly Dictionary<string, GameObject> AdoptedByUser =
            new Dictionary<string, GameObject>();

        /// <summary>Optional sink for posting confirmations to Twitch chat (wired from the IRC client).</summary>
        public static System.Action<string> ChatSay;

        /// <summary>Clear adoptions (called when a colony loads/unloads; old critters are gone).</summary>
        public static void Reset()
        {
            AdoptedByUser.Clear();
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

            Adopt(user.Trim(), cfg);
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

        private static void Adopt(string user, ModConfig cfg)
        {
            var key = user.ToLowerInvariant();

            // Keep the critter a viewer already owns (if still alive).
            if (AdoptedByUser.TryGetValue(key, out var existing) && existing != null)
            {
                return;
            }

            var critter = PickUnadopted();
            if (critter == null)
            {
                Log.Info($"{user} tried to adopt, but no free critter is available.");
                return;
            }

            var sel = critter.GetComponent<KSelectable>();
            if (sel == null)
            {
                return;
            }

            // Read the species name before we overwrite it, for the announcement.
            var species = Util.StripTextFormatting(sel.GetName() ?? "critter").Trim();

            sel.SetName(user); // plain nick, so bubble matching by name is straightforward
            AdoptedByUser[key] = critter;

            Log.Info($"{user} adopted a {species}.");
            SpeechBubbles.ShowRaw(critter.transform, "🐾 " + user); // quick visual cue over the critter
            if (cfg.AnnounceInChat)
            {
                ChatSay?.Invoke($"{user} adopted a {species}!");
            }
        }

        /// <summary>Pick a random living critter that nobody has adopted yet, or null if none free.</summary>
        private static GameObject PickUnadopted()
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

            var candidates = new List<GameObject>();
            foreach (var brain in brains)
            {
                // Only critters (not duplicant brains).
                if (!(brain is CreatureBrain))
                {
                    continue;
                }

                var go = brain.gameObject;
                if (go == null || taken.Contains(go) || go.GetComponent<KSelectable>() == null)
                {
                    continue;
                }

                candidates.Add(go);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
    }
}
