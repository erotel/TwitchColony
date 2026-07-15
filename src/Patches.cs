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

        // ReSharper disable once UnusedMember.Local
        private static void Postfix()
        {
            MainThread.Ensure();
            EventRegistry.RegisterDefaults();
            VoteController.Ensure();
            VoteHud.Ensure();

            var cfg = ModConfig.Instance;

            // Resolve Twitch auth off the main thread (network call).
            new Thread(TwitchAuth.Resolve) { IsBackground = true }.Start();

            client = new IrcClient(cfg.Channel, cfg.Nick, cfg.OauthToken);

            // Let the vote controller post announcements to chat (no-op when connected anonymously).
            VoteController.Ensure().ChatSay = client.SendChat;

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
                });
            };
            client.Start();

            Log.Info("Runtime started for this colony.");
        }
    }

    /// <summary>Stop the IRC connection cleanly when leaving the game.</summary>
    [HarmonyPatch(typeof(Game), "OnCleanUp")]
    internal static class Game_OnCleanUp_Patch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Prefix()
        {
            Game_OnSpawn_Patch.client?.Stop();
            Game_OnSpawn_Patch.client = null;
            Log.Info("Colony unloaded.");
        }
    }
}
