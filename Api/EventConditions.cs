using System;

namespace TwitchColony.Api
{
    /// <summary>
    ///     Ready-made conditions for <see cref="TwitchColonyApi.RegisterEvent"/>. All of them read
    ///     the payload Twitch Colony passes at draw time, so none of them touch the game — which is
    ///     what keeps this library safe to load when Twitch Colony isn't installed.
    ///
    ///     Write your own the same way: <c>ctx =&gt; whatever</c>. They're checked every time the
    ///     options are drawn, so keep them cheap and don't change anything from inside one.
    /// </summary>
    public static class EventConditions
    {
        /// <summary>Only from this colony cycle onwards. Good for events that would end a young colony.</summary>
        public static Func<object, bool> FromCycle(int cycle)
        {
            return ctx => EventContext.GetInt(ctx, EventContext.Cycle, -1) >= cycle;
        }

        /// <summary>Only before this colony cycle.</summary>
        public static Func<object, bool> BeforeCycle(int cycle)
        {
            return ctx =>
            {
                var current = EventContext.GetInt(ctx, EventContext.Cycle, -1);
                return current >= 0 && current < cycle;
            };
        }

        /// <summary>All of them must say yes. No conditions = always yes.</summary>
        public static Func<object, bool> All(params Func<object, bool>[] conditions)
        {
            return ctx =>
            {
                if (conditions == null) return true;
                foreach (var condition in conditions)
                {
                    if (condition != null && !condition(ctx)) return false;
                }

                return true;
            };
        }

        /// <summary>Any one of them saying yes is enough. No conditions = always yes.</summary>
        public static Func<object, bool> Any(params Func<object, bool>[] conditions)
        {
            return ctx =>
            {
                if (conditions == null || conditions.Length == 0) return true;
                foreach (var condition in conditions)
                {
                    if (condition != null && condition(ctx)) return true;
                }

                return false;
            };
        }

        /// <summary>Inverts a condition.</summary>
        public static Func<object, bool> Not(Func<object, bool> condition)
        {
            return ctx => condition == null || !condition(ctx);
        }
    }
}
