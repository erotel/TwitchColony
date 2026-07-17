using System;

namespace TwitchColony.Api
{
    /// <summary>
    ///     The one true shape of the registration call. Both sides derive from this single
    ///     declaration instead of spelling the signature out twice: the main mod assigns its method
    ///     to it (so a changed parameter breaks the build, loudly, now) and the merge-lib builds its
    ///     reflection lookup from it (so it can never ask for a signature that was never offered).
    ///     Without this, the two would drift apart silently and add-on events would just stop
    ///     registering with no error anywhere.
    /// </summary>
    internal delegate bool RegisterEventDelegate(string id, string displayName, string groupId,
        int weight, int danger, Action<object> action, Func<object, bool> condition, string owner);

    /// <summary>Same idea for the unregister call.</summary>
    internal delegate bool UnregisterEventDelegate(string id);

    /// <summary>
    ///     The exact names the merge-lib reflects for, and the version of the contract they follow.
    ///
    ///     This file is compiled into BOTH TwitchColony.dll and TwitchColony.Api.dll from the same
    ///     source (see the Compile Include in each csproj) so the two can never drift apart. It is
    ///     shared as source, not as a third DLL: a type compiled into two assemblies has two separate
    ///     runtime identities, so nothing from here may ever be passed across the boundary. Only
    ///     types both sides already share — string, int, bool, System.Action, System.Func, object —
    ///     are allowed in the bridge signature.
    /// </summary>
    internal static class ApiContract
    {
        /// <summary>Assembly to look for. Missing = Twitch Colony isn't installed; the API no-ops.</summary>
        public const string ModAssemblyName = "TwitchColony";

        /// <summary>Full name of the static class in the main mod that the merge-lib reflects into.</summary>
        public const string BridgeTypeName = "TwitchColony.Api.EventBridge";

        public const string RegisterMethodName = "RegisterEvent";
        public const string UnregisterMethodName = "UnregisterEvent";
        public const string VersionPropertyName = "ApiVersion";

        /// <summary>
        ///     Bumped only for a breaking change to the bridge signature. The merge-lib refuses to
        ///     register (rather than throwing) against a main mod whose version it doesn't know, so
        ///     an old add-on can't take the game down with a MissingMethodException.
        /// </summary>
        public const int Version = 1;
    }
}
