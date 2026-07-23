using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PeterHan.PLib.Options;
using UnityEngine;

namespace TwitchColony.Config
{
    /// <summary>
    ///     All user-tunable settings for the mod, persisted as JSON and editable in-game via
    ///     Mods → Twitch Colony → the gear icon (PLib options dialog).
    ///     This is our own independent implementation — field names and layout are our design.
    ///
    ///     PLib's options dialog only reads public PROPERTIES, which is why everything here is an
    ///     auto-property rather than a field. The JSON key names are unchanged, so existing config
    ///     files keep working.
    ///
    ///     Deliberately NOT exposed in the in-game dialog: OauthToken (a chat token is a password,
    ///     and this mod's users are literally screen-sharing while they play — see the notice
    ///     property below) and the Helix dev overrides (only used for the Twitch CLI mock).
    /// </summary>
    [ModInfo("https://github.com/erotel/TwitchColony")]
    [ConfigFile(IndentOutput: true, SharedConfigLocation: true)]
    public sealed class ModConfig : IOptions
    {
        // PLib collects categories into a SortedList, so they are shown in alphabetical order of
        // these strings — declaration order is ignored. The numbers force the order we want, with
        // the connection settings (the ones you must fill in) first instead of buried in the middle.
        private const string CAT_TWITCH = "1. Twitch connection";
        private const string CAT_BUBBLES = "2. Chat bubbles";
        private const string CAT_ADOPT = "3. Critter adoption";
        private const string CAT_VOTING = "4. Voting";
        private const string CAT_SUBS = "5. Twitch subs";

        // ---- Twitch connection ----
        [Option("Channel", "Twitch channel to join (your channel login, lowercase).", CAT_TWITCH)]
        public string Channel { get; set; } = "";

        [Option("Bot nick", "Login of the account the token belongs to. Leave EMPTY to read chat " +
            "anonymously — anonymous mode shows bubbles and counts chat votes, but cannot post to chat.",
            CAT_TWITCH)]
        public string Nick { get; set; } = "";

        /// <summary>
        ///     Static notice rendered by PLib as a text block (see TextBlockOptionsEntry: a read-only
        ///     LocText property that returns null). The token itself stays out of this dialog on
        ///     purpose — see the class docs.
        ///
        ///     Keep the lines SHORT and break them by hand. PLib lays the dialog out in a two-column
        ///     grid whose label column is as wide as its widest label wants to be, and a long
        ///     single-line notice makes that column swallow the whole dialog — pushing every
        ///     checkbox and text field off the right edge (which is exactly what happened in 1.4.0).
        /// </summary>
        [JsonIgnore]
        [Option("<b>OAuth token</b> goes in token.txt,\n" +
            "not here — it would be readable on\n" +
            "stream. Use the MANUAL CONFIG button\n" +
            "below to open the folder.",
            "The token is only needed to post to chat or run native Twitch polls.", CAT_TWITCH)]
        public LocText TokenNotice => null;

        /// <summary>
        ///     Chat OAuth token WITHOUT the "oauth:" prefix (empty = anonymous). Read from token.txt,
        ///     never from config.json — see <see cref="LoadToken"/> for why.
        /// </summary>
        [JsonIgnore] public string OauthToken => oauthToken;

        [JsonIgnore] private static string oauthToken = "";

        // ---- Chat bubbles ----
        [Option("Enable chat bubbles", "Show chat messages as speech bubbles above the duplicant " +
            "whose name matches the viewer's nick.", CAT_BUBBLES)]
        public bool EnableBubbles { get; set; } = true;

        [Option("Bubble command", "Only messages starting with this prefix become bubbles. " +
            "Leave EMPTY to turn every chat message into a bubble.", CAT_BUBBLES)]
        public string BubblePrefix { get; set; } = "!say";

        [Option("Max bubble length", "Longer messages are cut off at this many characters.", CAT_BUBBLES)]
        [Limit(20, 200)]
        public int MaxBubbleLength { get; set; } = 100;

        [Option("Bubble duration", "How many seconds a bubble stays on screen.", CAT_BUBBLES, Format = "F0")]
        [Limit(1, 15)]
        public float BubbleSeconds { get; set; } = 4f;

        [Option("Per-viewer cooldown", "Seconds a viewer must wait before their next bubble. " +
            "Anti-spam.", CAT_BUBBLES, Format = "F0")]
        [Limit(0, 60)]
        public float BubbleCooldownSeconds { get; set; } = 5f;

        [Option("Bubble font size", "Text size inside the bubble.", CAT_BUBBLES)]
        [Limit(6, 40)]
        public int BubbleFontSize { get; set; } = 10;

        [Option("Bubble width", "Wrap width in UI units.", CAT_BUBBLES)]
        [Limit(50, 400)]
        public int BubbleMaxWidth { get; set; } = 100;

        public string BubbleFont { get; set; } = "";  // Name of a game TMP font ("" = TMP default). See Player.log for available names.

        // Where the streamer dragged the vote panel to, relative to the top centre of the screen.
        // Not on the settings screen on purpose: you set this by dragging the panel, and a pair of
        // coordinate boxes would be a worse way to say the same thing.
        public float VoteHudX { get; set; } = 0f;
        public float VoteHudY { get; set; } = -12f;

        // ---- Critter adoption ----
        [Option("Enable critter adoption", "Viewers can adopt a critter with a chat command; the " +
            "critter is renamed to their nick.", CAT_ADOPT)]
        public bool EnableCritterAdopt { get; set; } = true;

        [Option("Adopt command", "Chat command that adopts a random un-adopted critter.", CAT_ADOPT)]
        public string AdoptCommand { get; set; } = "!adopt";

        [Option("Show name tag", "Show a permanent name label under each adopted critter.", CAT_ADOPT)]
        public bool ShowAdoptedNameTag { get; set; } = true;

        [Option("Auto-adopt from chat", "Without waiting for anyone to type the command, quietly name " +
            "free critters after random people from chat — nearest the printing pod first, working " +
            "outward. Stops when critters or chatters run out.", CAT_ADOPT)]
        public bool EnableAutoAdopt { get; set; } = true;

        [Option("Auto-adopt every (seconds)", "Pause between automatic namings, so a busy chat doesn't " +
            "get the whole ranch named in one go.", CAT_ADOPT)]
        [Limit(5, 600)]
        public int AutoAdoptIntervalSeconds { get; set; } = 45;

        // ---- Voting ----
        [Option("Enable events & voting", "Master switch for the whole event/voting system.", CAT_VOTING)]
        public bool EnableEvents { get; set; } = true;

        [Option("Start after cycles", "Wait this many cycles after loading a colony before the FIRST " +
            "vote starts automatically, so you have time to get set up. 0 = don't start on its own " +
            "(use the pause-menu button).", CAT_VOTING)]
        [Limit(0, 100)]
        public int StartAfterCycles { get; set; } = 0;

        [Option("Highest danger allowed", "The worst thing chat can ever vote for. Events are tagged " +
            "by what they actually do, and anything above this is never offered.", CAT_VOTING)]
        [JsonConverter(typeof(StringEnumConverter))]
        public DangerCap MaxEventDanger { get; set; } = DangerCap.Deadly;

        [Option("Ramp danger with cycles", "Start gentle and work up: a young colony is only offered " +
            "safe events, and the nastier ones unlock as it survives. Never goes past 'Highest " +
            "danger allowed'. Turn off to allow everything from cycle 1.", CAT_VOTING)]
        public bool ScaleDifficultyWithCycles { get; set; } = true;

        [Option("Full danger at cycle", "The cycle by which everything up to your danger cap is " +
            "unlocked. Earlier cycles unlock proportionally less.", CAT_VOTING)]
        [Limit(5, 200)]
        public int MaxDangerAtCycle { get; set; } = 30;

        [Option("Use native Twitch polls", "Run a real Twitch poll instead of counting chat messages. " +
            "Requires an Affiliate/Partner account and a token with poll scopes.", CAT_VOTING)]
        public bool UseTwitchPolls { get; set; } = false;

        [Option("Voting window", "How many seconds viewers have to vote.", CAT_VOTING)]
        [Limit(15, 300)]
        public int VotingSeconds { get; set; } = 60;

        [Option("Delay between votes", "Seconds from the end of one vote to the start of the next.",
            CAT_VOTING, Format = "F0")]
        [Limit(0, 3600)]
        public float VoteDelay { get; set; } = 540f;

        [Option("Options per vote", "How many events to offer. Twitch polls allow 2-5.", CAT_VOTING)]
        [Limit(2, 5)]
        public int OptionsPerVote { get; set; } = 3;

        [Option("Vote command", "Chat-vote command, e.g. \"!vote 2\".", CAT_VOTING)]
        public string VoteCommandPrefix { get; set; } = "!vote";

        [Option("Announce in chat", "Post the options and the winner to chat. Needs a bot nick and a " +
            "token with chat:edit.", CAT_VOTING)]
        public bool AnnounceInChat { get; set; } = true;

        [Option("Show the in-game vote panel", "The floating panel with the options, live tally and " +
            "countdown. Turn it off if your stream overlay shows the vote from votes.txt instead. " +
            "Event and sub banners still appear.", CAT_VOTING)]
        public bool ShowVoteHud { get; set; } = true;

        [Option("Write votes.txt for OBS", "Keep the current vote written to votes.txt next to " +
            "config.json. Point an OBS Text source with 'Read from file' at it and the vote shows in " +
            "your overlay — styled and placed however you like, visible on any scene.", CAT_VOTING)]
        public bool WriteVotesFile { get; set; } = false;

        [Option("Surprise box camera", "The surprise-box event pans and zooms the camera to the box.",
            CAT_VOTING)]
        public bool SurpriseBoxZoom { get; set; } = true;

        [Option("Show the event list button", "Adds a pause-menu button that lists every event and " +
            "lets you fire one on the spot. For trying things out and for building your own events — " +
            "it ignores the vote, your danger limit and the events' own conditions, so leave it off " +
            "while you're live unless you fancy one-click 'kill a duplicant'.", CAT_VOTING)]
        public bool ShowEventBrowser { get; set; } = false;

        // ---- Twitch subs ----
        [Option("Celebrate subs", "Show a NEW SUB banner and make the duplicants cheer when someone " +
            "subs, resubs, or gifts a sub.", CAT_SUBS)]
        public bool EnableSubRewards { get; set; } = true;

        [Option("Sub celebration cooldown", "Minimum seconds between celebrations, so a sub train " +
            "doesn't spam them.", CAT_SUBS, Format = "F0")]
        [Limit(0, 120)]
        public float SubRewardCooldownSeconds { get; set; } = 12f;

        [Option("Sub emoji duration", "How long the 🎉 bubble stays over the duplicants when someone " +
            "subs. Longer than the banner, so it's unmistakable. 0 turns the bubble off.", CAT_SUBS,
            Format = "F0")]
        [Limit(0, 30)]
        public float SubCelebrateBubbleSeconds { get; set; } = 10f;

        // Test aid, deliberately file-only and off by default: with it on, typing SubTestCommand in
        // chat fires the whole sub celebration as if a real sub came in. Not in the settings UI —
        // it's for you while testing, not something a viewer should find.
        public bool EnableSubTestCommand { get; set; } = false;
        public string SubTestCommand { get; set; } = "!sub";

        // ---- Twitch Helix (native polls; overrides only needed for the CLI mock) ----
        public string HelixBaseUrl { get; set; } = "https://api.twitch.tv/helix";
        public string ClientIdOverride { get; set; } = ""; // Only for the Twitch CLI mock; empty = read from token validation.
        public string BroadcasterIdOverride { get; set; } = "";

        // ------------------------------------------------------------------

        [JsonIgnore] public static ModConfig Instance { get; private set; } = new ModConfig();

        /// <summary>
        ///     Where the config lived before 1.4.0, when we managed the file ourselves. PLib decides
        ///     the path now (mods/config/TwitchColony/config.json), so old files get migrated once.
        /// </summary>
        [JsonIgnore] private static string LegacyConfigPath =>
            Path.Combine(Util.RootFolder(), "config_twitchcolony", "config.json");

        [JsonIgnore] public static string ConfigPath => POptions.GetConfigFilePath(typeof(ModConfig));

        /// <summary>The token lives in its own file next to config.json. See <see cref="LoadToken"/>.</summary>
        [JsonIgnore] public static string TokenPath =>
            Path.Combine(Path.GetDirectoryName(ConfigPath) ?? "", "token.txt");

        public static void Load()
        {
            try
            {
                MigrateLegacyConfig();

                var path = ConfigPath;
                // ReadSettings returns null both for "no file yet" and "the file is broken". Only the
                // file-on-disk check tells them apart, and the difference matters: re-saving over a
                // config with a typo in it would silently wipe every setting the user has.
                var fileExisted = File.Exists(path);
                var loaded = POptions.ReadSettings<ModConfig>();

                if (loaded != null)
                {
                    Instance = loaded;
                    LoadToken();  // Before the re-save below — it rescues a token still sitting in config.json.
                    Save();       // Re-save so options added by a mod update show up in the file with their defaults.
                    Log.Info("Config loaded from " + path);
                }
                else if (fileExisted)
                {
                    Instance = new ModConfig();
                    LoadToken();
                    Log.Warn("Config at " + path + " could not be parsed (JSON syntax error?). Running " +
                             "with defaults for now and leaving the file untouched — fix or delete it.");
                }
                else
                {
                    Instance = new ModConfig();
                    LoadToken();
                    Save();
                    Log.Info("Wrote default config to " + path);
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
                POptions.WriteSettings(Instance);  // Creates the folder and honors IndentOutput itself.
            }
            catch (Exception e)
            {
                Log.Warn("Failed to save config: " + e.Message);
            }
        }

        /// <summary>
        ///     Loads the OAuth token from its own file, importing it from config.json if that's where
        ///     it still is.
        ///
        ///     Why a separate file: PLib's options dialog reads config.json when it opens and writes
        ///     the whole object back when you press OK. Anything you type into config.json while that
        ///     dialog is open is silently overwritten by its stale snapshot — which is exactly how a
        ///     hand-pasted token disappeared in 1.4.0. Keeping the token out of that file means the
        ///     dialog cannot clobber it. It also keeps the one real secret out of the file we regenerate.
        /// </summary>
        private static void LoadToken()
        {
            try
            {
                oauthToken = ParseTokenFile(TokenPath);
                if (!string.IsNullOrEmpty(oauthToken)) return;

                // No token yet: pick it up from wherever it used to live. Checked even when token.txt
                // exists but is blank, so pasting it into config.json out of old habit still works.
                var imported = ExtractTokenFromJson(ConfigPath);
                if (string.IsNullOrEmpty(imported)) imported = ExtractTokenFromJson(LegacyConfigPath + ".migrated");
                if (string.IsNullOrEmpty(imported)) imported = ExtractTokenFromJson(LegacyConfigPath);

                if (!string.IsNullOrEmpty(imported))
                {
                    oauthToken = imported;
                    WriteTokenFile(imported);
                    Log.Info("Moved your OAuth token out of config.json into " + TokenPath +
                             " — the options screen can't overwrite it there. Edit it in that file from now on.");
                }
                else if (!File.Exists(TokenPath))
                {
                    WriteTokenFile("");  // Leave an explained, empty file so it's findable.
                }
            }
            catch (Exception e)
            {
                Log.Warn("Could not read the OAuth token, continuing anonymously: " + e.Message);
                oauthToken = "";
            }
        }

        /// <summary>First line that isn't blank or a # comment.</summary>
        private static string ParseTokenFile(string path)
        {
            if (!File.Exists(path)) return "";
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length > 0 && !line.StartsWith("#")) return line;
            }

            return "";
        }

        private static void WriteTokenFile(string token)
        {
            var dir = Path.GetDirectoryName(TokenPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(TokenPath,
                "# Twitch OAuth token for Twitch Colony.\n" +
                "# Paste the token on its own line below: no \"oauth:\" prefix, no quotes.\n" +
                "# Leave it empty to read chat anonymously (bubbles and chat voting still work).\n" +
                "#\n" +
                "# Keep this file private — anyone with the token can post to chat as you.\n" +
                "# It lives here rather than in config.json because the in-game options screen\n" +
                "# rewrites config.json and would overwrite whatever you typed there.\n" +
                "\n" + token + "\n");
        }

        /// <summary>
        ///     Digs an old OauthToken value out of a config file's raw JSON. The property is
        ///     [JsonIgnore] now, so normal deserialization would skip straight past it.
        /// </summary>
        private static string ExtractTokenFromJson(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                var token = JObject.Parse(File.ReadAllText(path))["OauthToken"];
                return token?.Type == JTokenType.String ? ((string)token ?? "").Trim() : "";
            }
            catch
            {
                return "";  // Broken or unreadable file: nothing to import, and not our problem here.
            }
        }

        /// <summary>
        ///     Moves a pre-1.4.0 config into the location PLib expects, once. Values are read with
        ///     our own JSON layout (unchanged), written to the new path, and the old file is renamed
        ///     rather than deleted — if this goes wrong the user's settings are still on disk.
        /// </summary>
        private static void MigrateLegacyConfig()
        {
            try
            {
                var newPath = ConfigPath;
                if (File.Exists(newPath) || !File.Exists(LegacyConfigPath)) return;

                var legacy = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(LegacyConfigPath));
                if (legacy == null)
                {
                    Log.Warn("Old config at " + LegacyConfigPath + " is unreadable; starting fresh.");
                    return;
                }

                Instance = legacy;
                Save();
                if (!File.Exists(newPath))
                {
                    Log.Warn("Could not write the migrated config; leaving the old file in place.");
                    return;
                }

                var backup = LegacyConfigPath + ".migrated";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(LegacyConfigPath, backup);
                Log.Info("Migrated your settings to " + newPath + " (old file kept as " + backup + ").");
            }
            catch (Exception e)
            {
                Log.Warn("Config migration failed, continuing with whatever loads: " + e.Message);
            }
        }

        // ---- IOptions ----
        // Implemented explicitly on purpose: a public CreateOptions() would drag PLib's IOptionsEntry
        // (and the PUI types behind it) into our public API surface, and ILRepack then has to leave
        // those types public instead of internalizing them.

        /// <summary>PLib adds these on top of the [Option] properties; we have no custom entries.</summary>
        IEnumerable<IOptionsEntry> IOptions.CreateOptions() => null;

        /// <summary>
        ///     Called by PLib after the user hits OK and the new values are already on disk. PLib
        ///     built its own instance for the dialog, so publish it as the live one: everything that
        ///     reads ModConfig.Instance per tick (bubbles, voting, adoption, subs) picks the change up
        ///     immediately. The IRC connection is the exception — it is opened once when the colony
        ///     loads, so channel/nick/token changes need a colony reload.
        /// </summary>
        void IOptions.OnOptionsChanged()
        {
            Instance = this;
            Log.Info("Settings updated in-game. (Channel/nick/token changes apply after you reload the colony.)");
        }
    }
}
