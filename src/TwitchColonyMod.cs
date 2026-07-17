using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using TwitchColony.Config;

namespace TwitchColony
{
    /// <summary>
    ///     Mod entry point. Loads config and applies Harmony patches. Original inspiration:
    ///     asquared31415's "Twitch Integration". This is an independent reimplementation — its own
    ///     code, architecture, and assets. See CREDITS.md.
    /// </summary>
    public sealed class TwitchColonyMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            // base.OnLoad already calls harmony.PatchAll() for this assembly — don't patch again,
            // or every patch runs twice (was the root cause of duplicate IRC / doubled menu button).
            base.OnLoad(harmony);

            // PLib gives us the settings screen behind the gear icon in the mods list. InitLibrary
            // must run before anything else touches PLib. Our copy is merged into this DLL, so it
            // can't be shadowed by an older loose PLib from some other mod.
            PUtil.InitLibrary(false);
            new POptions().RegisterOptions(this, typeof(ModConfig));

            ModConfig.Load();
            Log.Info("Loaded.");
        }
    }
}
