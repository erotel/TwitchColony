using HarmonyLib;
using KMod;
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
            base.OnLoad(harmony);
            ModConfig.Load();
            harmony.PatchAll();
            Log.Info("Loaded.");
        }
    }
}
