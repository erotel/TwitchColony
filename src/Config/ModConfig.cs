using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace TwitchColony.Config
{
    /// <summary>
    ///     All user-tunable settings for the mod, persisted as JSON next to the game's mod config.
    ///     This is our own independent implementation — field names and layout are our design.
    /// </summary>
    public sealed class ModConfig
    {
        // ---- Twitch connection ----
        public string Channel = "";           // Twitch channel (lowercase login) to join.
        public string Nick = "";              // Bot/user login for the chat connection (empty = anonymous read-only).
        public string OauthToken = "";        // Chat OAuth token WITHOUT the "oauth:" prefix (empty = anonymous).

        // ---- Chat bubbles ----
        public bool EnableBubbles = true;     // Show chat as speech bubbles above matching duplicants.
        public string BubblePrefix = "!say";  // Only messages starting with this prefix become bubbles ("" = all messages).
        public int MaxBubbleLength = 100;     // Hard cap on displayed characters.
        public float BubbleSeconds = 4f;      // How long a bubble stays on screen.
        public float BubbleCooldownSeconds = 5f; // Per-user cooldown to prevent spam.
        public int BubbleFontSize = 10;
        public int BubbleMaxWidth = 100;      // Wrap width in UI units.
        public string BubbleFont = "";        // Name of a game TMP font to use ("" = TMP default). See Player.log for available names.

        // ---- Critter adoption ----
        public bool EnableCritterAdopt = true; // Viewers can "adopt" a critter (rename it to their nick) via chat.
        public string AdoptCommand = "!adopt"; // Chat command that adopts a random free critter for the viewer.
        public bool ShowAdoptedNameTag = true; // Show a persistent name label under each adopted critter.

        // ---- Voting ----
        public bool EnableEvents = true;      // Master switch for the event/voting system.
        public int StartAfterCycles = 0;      // Cycles to wait after the colony loads before the FIRST vote auto-starts (0 = manual, use the pause-menu button). Gives the streamer time to prepare.
        public bool UseTwitchPolls = false;   // Use native Twitch polls instead of counting chat votes.
        public int VotingSeconds = 60;        // Voting window length.
        public float VoteDelay = 540f;        // Seconds between the end of one vote and the start of the next (auto-restart).
        public int OptionsPerVote = 3;        // How many events to offer (2-5 for polls).
        public string VoteCommandPrefix = "!vote"; // Chat-vote command, e.g. "!vote 2".
        public bool AnnounceInChat = true;    // Post vote options + winner to chat (needs Nick + token with chat:edit).
        public bool SurpriseBoxZoom = true;   // Surprise-box event pans/zooms the camera to the box.

        // ---- Twitch subs ----
        public bool EnableSubRewards = true;       // Show a "NEW SUB" banner + make dupes cheer when someone subs/resubs/gifts.
        public float SubRewardCooldownSeconds = 12f; // Min gap between sub celebrations (coalesces sub-trains).

        // ---- Twitch Helix (native polls; overrides only needed for the CLI mock) ----
        public string HelixBaseUrl = "https://api.twitch.tv/helix";
        public string ClientIdOverride = ""; // Only for the Twitch CLI mock; empty = read from token validation.
        public string BroadcasterIdOverride = "";

        // ------------------------------------------------------------------

        [JsonIgnore] public static ModConfig Instance { get; private set; } = new ModConfig();

        [JsonIgnore] private static string ConfigDir =>
            Path.Combine(Util.RootFolder(), "config_twitchcolony");

        [JsonIgnore] private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

        public static void Load()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    Instance = JsonConvert.DeserializeObject<ModConfig>(json) ?? new ModConfig();
                    Log.Info("Config loaded.");
                }
                else
                {
                    Instance = new ModConfig();
                    Save();
                    Log.Info("Wrote default config to " + ConfigPath);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Failed to load config, using defaults: " + e.Message);
                Instance = new ModConfig();
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(Instance, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Warn("Failed to save config: " + e.Message);
            }
        }
    }
}
