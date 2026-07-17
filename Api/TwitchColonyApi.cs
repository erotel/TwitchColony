using System;
using System.Reflection;

namespace TwitchColony.Api
{
    /// <summary>
    ///     Register your own events with Twitch Colony.
    ///
    ///     ILMerge this library into your mod's DLL (don't ship it loose — two mods shipping
    ///     different versions of a loose TwitchColony.Api.dll would fight over which one loads).
    ///
    ///     Twitch Colony does not have to be installed. Everything here reflects into the main mod
    ///     at call time and quietly does nothing if it isn't there, so your mod keeps working on its
    ///     own — no hard reference, no TypeLoadException, nothing to catch.
    ///
    ///     <code>
    ///     public override void OnLoad(Harmony harmony) {
    ///         base.OnLoad(harmony);
    ///         TwitchColonyApi.RegisterEvent(
    ///             id: "mymod.confetti",
    ///             displayName: "Confetti!",
    ///             action: ctx => Confetti.Drop(),
    ///             danger: EventDanger.None,
    ///             owner: "My Mod");
    ///     }
    ///     </code>
    /// </summary>
    public static class TwitchColonyApi
    {
        private const string LogPrefix = "[TwitchColony.Api] ";

        /// <summary>Zoom value meaning "don't touch the zoom, the streamer picked it".</summary>
        public const float KeepZoom = 0f;

        /// <summary>Close enough to see one thing clearly. The default when you point at an object.</summary>
        public const float CloseZoom = 10f;

        private static bool resolved;
        private static MethodInfo registerMethod;
        private static MethodInfo unregisterMethod;
        private static MethodInfo triggerMethod;
        private static MethodInfo bannerMethod;
        private static MethodInfo bannerAtTargetMethod;
        private static MethodInfo bannerAtPositionMethod;
        private static MethodInfo bubbleMethod;
        private static int installedVersion;

        /// <summary>True when Twitch Colony is installed and speaking a version we understand.</summary>
        public static bool IsAvailable
        {
            get
            {
                Resolve();
                return registerMethod != null;
            }
        }

        /// <summary>Contract version of the installed Twitch Colony, or 0 if it isn't there.</summary>
        public static int InstalledApiVersion
        {
            get
            {
                Resolve();
                return installedVersion;
            }
        }

        /// <summary>
        ///     Add an event to Twitch Colony's vote pool. Call it once, from your UserMod2.OnLoad;
        ///     the event survives colony reloads.
        /// </summary>
        /// <param name="id">
        ///     Stable, unique id — prefix it with your mod name ("mymod.confetti"). If it collides
        ///     with an id already registered, yours is refused.
        /// </param>
        /// <param name="displayName">What chat sees in the vote options.</param>
        /// <param name="action">
        ///     Runs on the game's main thread when your event wins, so it may touch the game freely.
        ///     The argument describes what triggered it: read it with <see cref="EventContext"/>
        ///     (<c>EventContext.GetStrings(ctx, EventContext.Voters)</c> and friends). Never cast it
        ///     to a type of your own — it's a plain Dictionary&lt;string, object&gt;.
        /// </param>
        /// <param name="groupId">
        ///     Optional. Events that feel alike should share a group: when one fires, the whole group
        ///     is less likely to be offered for the next few votes. Unlike <paramref name="id"/>,
        ///     group names are a shared namespace: "flood" puts your events in the same bucket as
        ///     Twitch Colony's floods (often what you want), while "mymod.floods" keeps them damping
        ///     only each other.
        /// </param>
        /// <param name="weight">How often it's offered relative to other events.</param>
        /// <param name="danger">
        ///     How much it can hurt the colony. Tag it honestly — streamers filter on this to keep a
        ///     run from being ruined by something they didn't opt into.
        /// </param>
        /// <param name="condition">
        ///     Optional. Checked when the options are drawn; return false to sit this vote out. Keep
        ///     it cheap and side-effect free. See <see cref="EventConditions"/> for ready-made ones.
        /// </param>
        /// <param name="owner">Your mod's name. Only used so log lines can name you.</param>
        /// <returns>
        ///     true if Twitch Colony accepted the event; false if it isn't installed, speaks a
        ///     version we don't know, or rejected the arguments. Never throws.
        /// </returns>
        public static bool RegisterEvent(string id, string displayName, Action<object> action,
            string groupId = null, EventWeight weight = EventWeight.Common,
            EventDanger danger = EventDanger.None, Func<object, bool> condition = null,
            string owner = null)
        {
            Resolve();
            if (registerMethod == null)
            {
                return false; // Twitch Colony isn't installed — that's a normal, quiet outcome.
            }

            try
            {
                return (bool)registerMethod.Invoke(null, new object[]
                {
                    id, displayName, groupId, (int)weight, (int)danger, action, condition, owner,
                });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "Could not register event '" + id + "': " +
                                             (e.InnerException ?? e).Message);
                return false;
            }
        }

        /// <summary>
        ///     Fire an event right now by id, skipping the vote — for testing your events while you
        ///     build them. Bind it to a key, call it from your debug menu, whatever suits.
        ///
        ///     <code>
        ///     if (Input.GetKeyDown(KeyCode.F9)) TwitchColonyApi.TriggerEvent("mymod.confetti");
        ///     </code>
        ///
        ///     It skips the vote, and with it the streamer's danger ceiling and your own condition,
        ///     so don't wire it to anything a viewer can reach. Call it on the game's main thread,
        ///     with a colony loaded.
        /// </summary>
        /// <param name="id">Id of a registered event — yours, or one of Twitch Colony's own.</param>
        /// <returns>
        ///     true if the event ran; false if Twitch Colony isn't installed or has no such id.
        ///     Never throws.
        /// </returns>
        public static bool TriggerEvent(string id)
        {
            Resolve();
            if (triggerMethod == null)
            {
                return false;
            }

            try
            {
                return (bool)triggerMethod.Invoke(null, new object[] { id });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "Could not trigger event '" + id + "': " +
                                             (e.InnerException ?? e).Message);
                return false;
            }
        }

        /// <summary>
        ///     Show a banner across the top of the screen — the same one Twitch Colony announces vote
        ///     winners and new subs with. Use it to warn the streamer what just landed on them, or to
        ///     deliver the punchline of an event.
        ///
        ///     <code>
        ///     TwitchColonyApi.ShowBanner("&lt;b&gt;Nobody expects the Spanish Inquisition!&lt;/b&gt;", 6f);
        ///     </code>
        ///
        ///     TextMeshPro rich text works (<c>&lt;color=#RRGGBB&gt;</c>, <c>&lt;b&gt;</c>, newlines).
        ///     Emoji don't: the game's fonts have none, so anything unrenderable is dropped rather
        ///     than drawn as a hollow box. A second banner replaces the first.
        ///
        ///     Main thread, colony loaded.
        /// </summary>
        /// <param name="message">Keep it short — it's a banner, not a paragraph.</param>
        /// <param name="seconds">How long it stays up. Clamped to 1–30.</param>
        /// <returns>true if it was shown; false if Twitch Colony isn't installed. Never throws.</returns>
        public static bool ShowBanner(string message, float seconds = 5f)
        {
            Resolve();
            if (bannerMethod == null)
            {
                return false;
            }

            try
            {
                return (bool)bannerMethod.Invoke(null, new object[] { message, seconds });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "Could not show banner: " +
                                             (e.InnerException ?? e).Message);
                return false;
            }
        }

        /// <summary>
        ///     A banner the streamer can click to look at whatever it's about — the camera pans to
        ///     the object, keeping their zoom. It follows the object, so a moving one still works.
        ///
        ///     <code>
        ///     TwitchColonyApi.ShowBanner("&lt;b&gt;Something is loose in the base!&lt;/b&gt;", 8f, critter);
        ///     </code>
        ///
        ///     Half the value of a warning is "…and it's over there", and the streamer shouldn't have
        ///     to go hunting. The banner says "click to look" so they know it's there.
        /// </summary>
        /// <param name="message">Keep it short — it's a banner, not a paragraph.</param>
        /// <param name="seconds">How long it stays up. Clamped to 1–30.</param>
        /// <param name="panTo">Where to take them. Nothing pans if it's null or gets destroyed.</param>
        /// <param name="orthographicSize">
        ///     Zoom to arrive at — smaller is closer, and it's clamped to what the game allows.
        ///     Defaults to zooming in on the thing, since panning to a critter the streamer is zoomed
        ///     out from just centres a dot. Pass <see cref="KeepZoom"/> to leave their zoom alone.
        /// </param>
        /// <returns>true if it was shown; false if Twitch Colony isn't installed. Never throws.</returns>
        public static bool ShowBanner(string message, float seconds, UnityEngine.GameObject panTo,
            float orthographicSize = CloseZoom)
        {
            return Banner(bannerAtTargetMethod, message, seconds, panTo, orthographicSize);
        }

        /// <summary>Same, but for a fixed spot rather than an object — a cell you flooded, say.</summary>
        /// <param name="message">Keep it short — it's a banner, not a paragraph.</param>
        /// <param name="seconds">How long it stays up. Clamped to 1–30.</param>
        /// <param name="panTo">The world position to pan to when clicked.</param>
        /// <param name="orthographicSize">
        ///     Zoom to arrive at. Defaults to <see cref="KeepZoom"/> — for a place rather than a
        ///     thing, only you know how much of it the streamer needs to see.
        /// </param>
        /// <returns>true if it was shown; false if Twitch Colony isn't installed. Never throws.</returns>
        public static bool ShowBanner(string message, float seconds, UnityEngine.Vector3 panTo,
            float orthographicSize = KeepZoom)
        {
            return Banner(bannerAtPositionMethod, message, seconds, panTo, orthographicSize);
        }

        private static bool Banner(MethodInfo method, string message, float seconds, object panTo,
            float orthographicSize)
        {
            Resolve();
            if (method == null)
            {
                return false;
            }

            try
            {
                return (bool)method.Invoke(null,
                    new[] { message, (object)seconds, panTo, orthographicSize });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "Could not show banner: " +
                                             (e.InnerException ?? e).Message);
                return false;
            }
        }

        /// <summary>
        ///     Show a speech bubble above something — the same bubble viewers' chat messages appear
        ///     in. Works over anything with a transform: a duplicant, a critter, a building.
        ///
        ///     <code>
        ///     TwitchColonyApi.ShowBubble(dupe.gameObject, "well, this is unexpected");
        ///     </code>
        ///
        ///     It follows the streamer's own bubble settings (font, size, lifetime), so it looks like
        ///     part of the mod. One bubble per object — showing another replaces it. Emoji are
        ///     dropped, as with banners.
        ///
        ///     Main thread, colony loaded.
        /// </summary>
        /// <param name="target">What to float it above. Nothing happens if it's null or destroyed.</param>
        /// <param name="text">What it says.</param>
        /// <returns>true if a bubble appeared; false if Twitch Colony isn't installed. Never throws.</returns>
        public static bool ShowBubble(UnityEngine.GameObject target, string text)
        {
            Resolve();
            if (bubbleMethod == null)
            {
                return false;
            }

            try
            {
                return (bool)bubbleMethod.Invoke(null, new object[] { target, text });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "Could not show bubble: " +
                                             (e.InnerException ?? e).Message);
                return false;
            }
        }

        /// <summary>Remove an event you registered earlier. Returns true if it was there.</summary>
        public static bool UnregisterEvent(string id)
        {
            Resolve();
            if (unregisterMethod == null)
            {
                return false;
            }

            try
            {
                return (bool)unregisterMethod.Invoke(null, new object[] { id });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "Could not unregister event '" + id + "': " +
                                             (e.InnerException ?? e).Message);
                return false;
            }
        }

        /// <summary>
        ///     Find the main mod's bridge once and remember the answer, including "it isn't there".
        ///     Matching the method by its exact parameter types is the real compatibility check: if a
        ///     future Twitch Colony changes the signature, we find nothing and no-op rather than
        ///     throwing a MissingMethodException into the add-on's face.
        /// </summary>
        private static void Resolve()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;

            try
            {
                var bridge = FindBridgeType();
                if (bridge == null)
                {
                    return; // Not installed. Silent: this is the expected case for most players.
                }

                var versionProperty = bridge.GetProperty(ApiContract.VersionPropertyName,
                    BindingFlags.Public | BindingFlags.Static);
                if (versionProperty != null)
                {
                    installedVersion = Convert.ToInt32(versionProperty.GetValue(null, null));
                }

                // The signatures come from the shared contract delegates, never hand-typed here:
                // that's what guarantees we ask for exactly what the main mod compiled against.
                registerMethod = bridge.GetMethod(ApiContract.RegisterMethodName,
                    BindingFlags.Public | BindingFlags.Static, null,
                    ParametersOf(typeof(RegisterEventDelegate)), null);

                unregisterMethod = bridge.GetMethod(ApiContract.UnregisterMethodName,
                    BindingFlags.Public | BindingFlags.Static, null,
                    ParametersOf(typeof(UnregisterEventDelegate)), null);

                // Added after v1 shipped, so an older Twitch Colony simply won't have it. Missing =
                // TriggerEvent no-ops, rather than the whole API refusing to work.
                triggerMethod = bridge.GetMethod(ApiContract.TriggerMethodName,
                    BindingFlags.Public | BindingFlags.Static, null,
                    ParametersOf(typeof(TriggerEventDelegate)), null);

                bannerMethod = bridge.GetMethod(ApiContract.ShowBannerMethodName,
                    BindingFlags.Public | BindingFlags.Static, null,
                    ParametersOf(typeof(ShowBannerDelegate)), null);

                // Overloads of the same name; the delegates pick the right one by signature.
                bannerAtTargetMethod = bridge.GetMethod(ApiContract.ShowBannerMethodName,
                    BindingFlags.Public | BindingFlags.Static, null,
                    ParametersOf(typeof(ShowBannerAtTargetDelegate)), null);

                bannerAtPositionMethod = bridge.GetMethod(ApiContract.ShowBannerMethodName,
                    BindingFlags.Public | BindingFlags.Static, null,
                    ParametersOf(typeof(ShowBannerAtPositionDelegate)), null);

                bubbleMethod = bridge.GetMethod(ApiContract.ShowBubbleMethodName,
                    BindingFlags.Public | BindingFlags.Static, null,
                    ParametersOf(typeof(ShowBubbleDelegate)), null);

                if (registerMethod == null)
                {
                    UnityEngine.Debug.LogWarning(LogPrefix + "Twitch Colony is installed but speaks API v" +
                                                 installedVersion + ", and this add-on was built against v" +
                                                 ApiContract.Version + ". Its events will be skipped; " +
                                                 "update one of the two.");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "Could not reach Twitch Colony, skipping its events: " +
                                             e.Message);
                registerMethod = null;
            }
        }

        /// <summary>The parameter types of a delegate, i.e. the signature to look for.</summary>
        private static Type[] ParametersOf(Type delegateType)
        {
            var invoke = delegateType.GetMethod("Invoke");
            return Array.ConvertAll(invoke.GetParameters(), p => p.ParameterType);
        }

        private static Type FindBridgeType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetName().Name != ApiContract.ModAssemblyName)
                    {
                        continue;
                    }

                    var type = assembly.GetType(ApiContract.BridgeTypeName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // A broken or dynamic assembly can throw just for being asked its name. Skip it.
                }
            }

            return null;
        }
    }
}
