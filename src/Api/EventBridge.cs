using System;
using System.Collections.Generic;
using TwitchColony.Events;

namespace TwitchColony.Api
{
    /// <summary>
    ///     The mod's public entry point for events contributed by other mods. This is the ONLY thing
    ///     add-ons are meant to call, and its signature is a promise — see <see cref="ApiContract"/>
    ///     before changing anything here.
    ///
    ///     Every parameter is a type both assemblies already share (string, int, System.Action,
    ///     System.Func, object). That's deliberate: an interface or class of ours would be a
    ///     different type in the add-on's assembly than in this one, so the cast would throw — which
    ///     is exactly why the old <see cref="EventRegistry.AddEvent"/> approach (subclass GameEvent)
    ///     couldn't work for anybody but us.
    ///
    ///     Add-ons don't have to reflect for this themselves: the TwitchColony.Api merge-lib wraps it
    ///     in a typed API and quietly does nothing when Twitch Colony isn't installed.
    /// </summary>
    public static class EventBridge
    {
        /// <summary>Bumped only on a breaking change. Add-ons check it before calling in.</summary>
        public static int ApiVersion => ApiContract.Version;

        /// <summary>
        ///     Never called: it exists so the compiler checks our two public methods still match the
        ///     shared contract the merge-lib reflects for. Change a parameter on either without the
        ///     other and this stops the build — which beats add-ons silently failing to register.
        /// </summary>
        private static void AssertSignaturesMatchContract()
        {
            RegisterEventDelegate register = RegisterEvent;
            UnregisterEventDelegate unregister = UnregisterEvent;
            TriggerEventDelegate trigger = TriggerEvent;
            GC.KeepAlive(register);
            GC.KeepAlive(unregister);
            GC.KeepAlive(trigger);
        }

        /// <summary>
        ///     Add an event to the vote pool. Safe to call at any time — typically once from your
        ///     UserMod2.OnLoad. The event survives colony reloads.
        /// </summary>
        /// <param name="id">
        ///     Stable, unique id. Prefix it with your mod name ("mymod.flood"): a clash with an
        ///     existing id is ignored, and yours would be the one to lose.
        /// </param>
        /// <param name="displayName">What chat sees in the vote options.</param>
        /// <param name="groupId">
        ///     Optional. Events sharing a group are pushed down the draw order together once one of
        ///     them fires, so near-identical events don't come up back to back. null = no group.
        ///     Group names are a shared namespace across mods, unlike <paramref name="id"/>: an
        ///     add-on using "flood" joins the built-in floods' bucket on purpose or by accident.
        /// </param>
        /// <param name="weight">Relative chance of being offered. See EventWeight; 0 = never drawn.</param>
        /// <param name="danger">How much it can hurt the colony. See EventDanger.</param>
        /// <param name="action">
        ///     Runs on the main thread when the event wins. The object is a
        ///     Dictionary&lt;string, object&gt; describing what triggered it — read it with
        ///     EventContext, never cast it to a type of your own.
        /// </param>
        /// <param name="condition">
        ///     Optional. Checked when options are drawn; false = not offered this round. Gets the
        ///     same payload shape as <paramref name="action"/>. null = always eligible.
        /// </param>
        /// <param name="owner">Optional mod name, used only to name you in log messages.</param>
        /// <returns>true if it was registered; false if the arguments were unusable.</returns>
        public static bool RegisterEvent(string id, string displayName, string groupId, int weight,
            int danger, Action<object> action, Func<object, bool> condition, string owner)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || action == null)
                {
                    Log.Warn($"Rejected an event from {Describe(owner)}: it needs an id and an action.");
                    return false;
                }

                if (string.IsNullOrEmpty(displayName)) displayName = id;
                if (weight < 0) weight = 0;
                danger = Clamp(danger, (int)EventDanger.None, (int)EventDanger.Deadly);

                var ev = new ApiEvent(id, displayName, groupId, weight, danger, action, condition)
                {
                    Owner = Describe(owner),
                };

                if (!EventRegistry.AddExternal(ev))
                {
                    Log.Warn($"Event id '{id}' from {Describe(owner)} is already taken; ignoring it.");
                    return false;
                }

                Log.Info($"{Describe(owner)} registered event '{id}' " +
                         $"(group={groupId ?? "none"}, weight={weight}, danger={danger}).");
                return true;
            }
            catch (Exception e)
            {
                // Never throw into an add-on's OnLoad: a bug of ours would look like a bug of theirs
                // and could stop their mod loading entirely.
                Log.Warn($"Failed to register an event from {Describe(owner)}: {e}");
                return false;
            }
        }

        /// <summary>
        ///     Fire an event right now by id, skipping the vote. Meant for testing your own events
        ///     while you build them — bind it to a key, call it from a debug menu, whatever.
        ///
        ///     It bypasses the vote, and with it the streamer's danger ceiling and the event's own
        ///     condition, so don't wire it to anything a viewer can reach. It counts as "just
        ///     happened" for the group cooldown, exactly as if chat had voted for it.
        ///
        ///     Must be called on the game's main thread, and only with a colony loaded.
        /// </summary>
        /// <param name="id">Id of a registered event, yours or a built-in one.</param>
        /// <returns>true if the event was found and run; false if there's no such id.</returns>
        public static bool TriggerEvent(string id)
        {
            try
            {
                var ev = EventRegistry.ById(id);
                if (ev == null)
                {
                    Log.Warn($"TriggerEvent: no event with id '{id}'.");
                    return false;
                }

                var context = new Dictionary<string, object>
                {
                    { EventContext.EventId, ev.Id },
                    { EventContext.Cycle, CurrentCycle() },
                    { EventContext.Source, EventContext.SourceDirect },
                    { EventContext.VoteCount, 0 },
                    { EventContext.Voters, new string[0] },
                };

                Log.Info($"TriggerEvent: firing '{ev.Id}' directly.");
                EventRegistry.NoteTriggered(ev);
                ev.Trigger(context);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn($"TriggerEvent('{id}') threw: {e}");
                return false;
            }
        }

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

        /// <summary>Remove a previously registered event. Returns true if it was there.</summary>
        public static bool UnregisterEvent(string id)
        {
            try
            {
                return !string.IsNullOrEmpty(id) && EventRegistry.RemoveExternal(id);
            }
            catch (Exception e)
            {
                Log.Warn($"Failed to unregister event '{id}': {e.Message}");
                return false;
            }
        }

        private static string Describe(string owner) => string.IsNullOrEmpty(owner) ? "an add-on" : owner;

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
