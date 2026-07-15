using UnityEngine;

namespace TwitchColony
{
    /// <summary>Small logging wrapper so all mod output is tagged and easy to grep in Player.log.</summary>
    internal static class Log
    {
        private const string Tag = "[TwitchColony]";

        public static void Info(string msg) => Debug.Log($"{Tag} {msg}");
        public static void Warn(string msg) => Debug.LogWarning($"{Tag} {msg}");
        public static void Error(string msg) => Debug.LogError($"{Tag} {msg}");
    }
}
