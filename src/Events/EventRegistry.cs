using System.Collections.Generic;

namespace TwitchColony.Events
{
    /// <summary>Holds all registered events and picks a random subset for each vote.</summary>
    public static class EventRegistry
    {
        private static readonly List<GameEvent> All = new List<GameEvent>();

        public static void RegisterDefaults()
        {
            All.Clear();
            Register(new CheerEvent());
            Register(new RollCallEvent());
            Register(new QuietEvent());
            // Add your own events here.
            Log.Info($"Registered {All.Count} events.");
        }

        public static void Register(GameEvent ev)
        {
            if (ev != null)
            {
                All.Add(ev);
            }
        }

        public static IReadOnlyList<GameEvent> AllEvents => All;

        /// <summary>Pick up to <paramref name="count"/> distinct random events for a vote.</summary>
        public static List<GameEvent> PickForVote(int count)
        {
            var pool = new List<GameEvent>(All);
            var chosen = new List<GameEvent>();
            count = System.Math.Min(count, pool.Count);
            for (var i = 0; i < count; i++)
            {
                var idx = UnityEngine.Random.Range(0, pool.Count);
                chosen.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            return chosen;
        }

        public static GameEvent ById(string id)
        {
            foreach (var e in All)
            {
                if (e.Id == id)
                {
                    return e;
                }
            }

            return null;
        }
    }
}
