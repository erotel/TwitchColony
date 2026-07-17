using HarmonyLib;
using KMod;
using TwitchColony.Api;
using UnityEngine;

namespace TwitchColonyExampleAddon
{
    /// <summary>
    ///     A tiny add-on that puts its own events into Twitch Colony's vote pool. It exists to prove
    ///     the API works end to end from a genuinely separate mod — registration, the vote draw, the
    ///     payload and conditions — and to serve as a copy-paste starting point.
    ///
    ///     Note what this mod does NOT do: it never references TwitchColony.dll. Everything goes
    ///     through TwitchColony.Api.dll (merged into this DLL), which reflects into Twitch Colony at
    ///     runtime. Uninstall Twitch Colony and this mod still loads happily and registers nothing.
    /// </summary>
    public sealed class ExampleMod : UserMod2
    {
        private const string Tag = "[TwitchColonyExample] ";
        private const string ModName = "Example Add-on";

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            Debug.Log(Tag + "Twitch Colony available: " + TwitchColonyApi.IsAvailable +
                      ", API v" + TwitchColonyApi.InstalledApiVersion);

            // A plain event. This is what a real add-on looks like.
            var registered = TwitchColonyApi.RegisterEvent(
                id: "example.hello",
                displayName: "Hello from another mod!",
                action: OnHello,
                groupId: "example.demo",
                // A real add-on uses EventWeight.Common. This one is cranked absurdly high on
                // purpose so it shows up in almost every vote while testing — otherwise it would be
                // one of ~73 events and you'd be clicking through votes all evening waiting for it.
                weight: (EventWeight)500,
                danger: EventDanger.None,
                owner: ModName);

            // Same, but it sits out the early game. Proves conditions are honoured at draw time:
            // this one must NOT appear in the options before cycle 5.
            TwitchColonyApi.RegisterEvent(
                id: "example.late",
                displayName: "Hello from cycle 5 onwards",
                action: ctx => Debug.Log(Tag + "The late event fired."),
                groupId: "example.demo",
                weight: (EventWeight)500,
                danger: EventDanger.Small,
                condition: EventConditions.FromCycle(5),
                owner: ModName);

            Debug.Log(Tag + (registered
                ? "Events registered. Look for them in the vote options."
                : "Twitch Colony isn't installed (or speaks a different API version) — " +
                  "registering nothing, carrying on."));
        }

        /// <summary>
        ///     Runs on the game's main thread when chat votes for the event. The payload is a plain
        ///     dictionary — read it with EventContext, never cast it.
        /// </summary>
        private static void OnHello(object context)
        {
            var cycle = EventContext.GetInt(context, EventContext.Cycle, -1);
            var votes = EventContext.GetInt(context, EventContext.VoteCount);
            var source = EventContext.GetString(context, EventContext.Source);
            var voters = EventContext.GetStrings(context, EventContext.Voters);

            Debug.Log(Tag + $"Hello fired on cycle {cycle} via {source} with {votes} vote(s). " +
                      $"Voters: {(voters.Length > 0 ? string.Join(", ", voters) : "(none reported)")}");
        }
    }
}
