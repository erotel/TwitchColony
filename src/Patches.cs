using System.Threading;
using HarmonyLib;
using TwitchColony.Config;
using TwitchColony.Events;
using TwitchColony.Twitch;
using TwitchColony.UI;
using TwitchColony.Voting;

namespace TwitchColony
{
    /// <summary>
    ///     Boots the mod's runtime when a colony loads: main-thread pump, event registry, Twitch auth,
    ///     IRC connection, and the vote controller. Chat messages are routed to bubbles and voting.
    /// </summary>
    [HarmonyPatch(typeof(Game), "OnSpawn")]
    internal static class Game_OnSpawn_Patch
    {
        internal static IrcClient client;

        /// <summary>
        ///     The Game the current client belongs to. Used to tell "my colony is being unloaded"
        ///     apart from "a colony I already replaced is being unloaded late" — the teardown and
        ///     the next OnSpawn don't have a guaranteed order.
        /// </summary>
        private static Game owner;

        // ReSharper disable once UnusedMember.Local
        private static void Postfix(Game __instance)
        {
            // Never assume the last colony cleaned up after itself: Game.OnCleanUp doesn't run when
            // you go straight from a running colony into a new game, so its IRC client can still be
            // attached here. Tear it down and start fresh rather than bailing out — bailing out left
            // the new colony with no runtime at all, a dead vote loop and a greyed-out menu button.
            // (This also keeps us safe if OnSpawn ever fires twice for one colony: one client only,
            // or two logins with the same nick fight over the connection.)
            Shutdown();

            MainThread.Ensure();
            EventRegistry.RegisterDefaults();
            CritterAdoption.Reset();

            // The controller and HUD are DontDestroyOnLoad, so they outlive the colony that made
            // them and must be told to forget it.
            VoteController.Ensure().ResetForNewColony();
            VoteHud.Ensure();

            var cfg = ModConfig.Instance;

            // Resolve Twitch auth off the main thread (network call).
            new Thread(TwitchAuth.Resolve) { IsBackground = true }.Start();

            client = new IrcClient(cfg.Channel, cfg.Nick, cfg.OauthToken);

            // Let the vote controller + critter adoption + sub rewards post announcements to chat (no-op anonymous).
            VoteController.Ensure().ChatSay = client.SendChat;
            CritterAdoption.ChatSay = client.SendChat;
            SubRewards.ChatSay = client.SendChat;

            client.OnMessage += msg =>
            {
                // IRC callback runs on a background thread; hop to the main thread for game/UI.
                MainThread.Run(() =>
                {
                    if (cfg.EnableBubbles)
                    {
                        SpeechBubbles.TryShow(msg.User, msg.Text);
                    }

                    if (cfg.EnableEvents)
                    {
                        VoteController.Ensure().FeedChat(msg.User, msg.Text);
                    }

                    if (cfg.EnableCritterAdopt)
                    {
                        CritterAdoption.TryHandle(msg.User, msg.Text);
                    }

                    // Test aid (off unless switched on in config.json): lets you fire the whole sub
                    // celebration by typing the command, instead of waiting for a real sub.
                    if (cfg.EnableSubTestCommand && !string.IsNullOrEmpty(cfg.SubTestCommand) &&
                        (msg.Text ?? "").Trim().Equals(cfg.SubTestCommand, System.StringComparison.OrdinalIgnoreCase))
                    {
                        SubRewards.TriggerTest();
                    }
                });
            };
            client.OnSub += sub =>
            {
                // IRC callback runs on a background thread; the reward effect touches the game.
                MainThread.Run(() =>
                {
                    if (ModConfig.Instance.EnableSubRewards)
                    {
                        SubRewards.Handle(sub);
                    }
                });
            };
            client.Start();
            owner = __instance;

            Log.Info("Runtime started for this colony.");
        }

        /// <summary>Drop the Twitch connection of whatever colony was running, if any.</summary>
        internal static void Shutdown()
        {
            if (client == null)
            {
                return;
            }

            client.Stop();
            client = null;
            owner = null;
        }

        /// <summary>
        ///     Called from both teardown paths. Ignores a colony we've already moved on from, so a
        ///     late teardown can't cut the connection of the colony that replaced it.
        /// </summary>
        internal static void ShutdownIfOwnedBy(Game game)
        {
            if (client == null || !ReferenceEquals(owner, game))
            {
                return;
            }

            Shutdown();

            // Stop the vote loop with the colony. It survives the unload (DontDestroyOnLoad), and a
            // vote still counting down would resolve in the main menu and trigger its event with no
            // colony to trigger it in.
            VoteController.Instance?.ResetForNewColony();

            Log.Info("Colony unloaded.");
        }
    }

    /// <summary>Stop the IRC connection cleanly when a colony is unloaded normally.</summary>
    [HarmonyPatch(typeof(Game), "OnCleanUp")]
    internal static class Game_OnCleanUp_Patch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Prefix(Game __instance) => Game_OnSpawn_Patch.ShutdownIfOwnedBy(__instance);
    }

    /// <summary>
    ///     The other teardown path. Klei tears the scene down through OnForcedCleanUp when you go
    ///     straight from a running colony into another game, and OnCleanUp never runs — so patching
    ///     only that one left the bot connected to chat with no colony behind it.
    /// </summary>
    [HarmonyPatch(typeof(Game), "OnForcedCleanUp")]
    internal static class Game_OnForcedCleanUp_Patch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Prefix(Game __instance) => Game_OnSpawn_Patch.ShutdownIfOwnedBy(__instance);
    }
}
