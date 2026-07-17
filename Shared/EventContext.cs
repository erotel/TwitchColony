using System;
using System.Collections.Generic;

namespace TwitchColony.Api
{
    /// <summary>
    ///     Reads the object handed to an event's action.
    ///
    ///     The payload is a plain <c>Dictionary&lt;string, object&gt;</c> — deliberately, not a nice
    ///     context class. A class of ours compiled into two assemblies would be two unrelated types
    ///     at runtime, so an add-on's cast would throw. A dictionary of BCL types always works, and
    ///     new keys can be added later without breaking add-ons built against an older version.
    ///
    ///     Every getter tolerates a null or unexpected payload and returns the fallback: an event
    ///     must never blow up because it was triggered from somewhere that didn't fill a key in.
    /// </summary>
    public static class EventContext
    {
        /// <summary>Id of the event being triggered. string.</summary>
        public const string EventId = "eventId";

        /// <summary>Colony cycle when it fired, or -1 if the clock wasn't up yet. int.</summary>
        public const string Cycle = "cycle";

        /// <summary>Votes this option received. int. 0 when it wasn't a vote (see <see cref="Source"/>).</summary>
        public const string VoteCount = "voteCount";

        /// <summary>
        ///     Nicks that voted for this option. string[]. Empty for native Twitch polls — Twitch
        ///     reports totals, not who voted.
        /// </summary>
        public const string Voters = "voters";

        /// <summary>What set it off: <see cref="SourceChatVote"/>, <see cref="SourcePoll"/>, or other. string.</summary>
        public const string Source = "source";

        /// <summary>Value of <see cref="Source"/> when chat voted by typing the vote command.</summary>
        public const string SourceChatVote = "chatVote";

        /// <summary>Value of <see cref="Source"/> when the winner came from a native Twitch poll.</summary>
        public const string SourcePoll = "twitchPoll";

        /// <summary>Reads a string key, or <paramref name="fallback"/> if it's missing or another type.</summary>
        public static string GetString(object context, string key, string fallback = "")
        {
            return TryGet(context, key, out var value) && value is string s ? s : fallback;
        }

        /// <summary>Reads a numeric key, or <paramref name="fallback"/> if it's missing or not a number.</summary>
        public static int GetInt(object context, string key, int fallback = 0)
        {
            if (!TryGet(context, key, out var value)) return fallback;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>Never returns null, so callers can foreach without checking.</summary>
        public static string[] GetStrings(object context, string key)
        {
            if (TryGet(context, key, out var value))
            {
                if (value is string[] array) return array;
                if (value is IEnumerable<string> items) return new List<string>(items).ToArray();
            }

            return new string[0];
        }

        private static bool TryGet(object context, string key, out object value)
        {
            value = null;
            return context is IDictionary<string, object> map && key != null && map.TryGetValue(key, out value);
        }
    }
}
