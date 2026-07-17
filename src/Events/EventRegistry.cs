using System.Collections.Generic;
using TwitchColony.Api;
using TwitchColony.Config;

namespace TwitchColony.Events
{
    /// <summary>
    ///     Holds all registered events and picks a weighted random subset for each vote.
    ///
    ///     MODDING API: other mods contribute events through <see cref="Api.EventBridge"/> — see
    ///     MODDING.md. Don't point add-on authors at <see cref="AddEvent"/>: subclassing
    ///     <see cref="GameEvent"/> forces a hard reference to this assembly, so their mod dies when
    ///     Twitch Colony isn't installed. It stays for our own use and for anyone who accepts that.
    /// </summary>
    public static class EventRegistry
    {
        private static readonly List<GameEvent> All = new List<GameEvent>();

        // Events contributed by other mods; re-added on every colony load (they survive the clear).
        private static readonly List<GameEvent> External = new List<GameEvent>();

        /// <summary>
        ///     Groups that fired recently, and how many more draws they stay damped for. Keeps chat
        ///     from being offered three floods in a row just because the dice said so.
        /// </summary>
        private static readonly Dictionary<string, int> GroupCooldowns = new Dictionary<string, int>();

        /// <summary>Draws over which a fired event's group stays less likely.</summary>
        private const int GroupCooldownDraws = 3;

        /// <summary>Raised after the default + external events are registered on each colony load.</summary>
        public static event System.Action Registering;

        /// <summary>
        ///     Add an event from another mod. Persists across colony reloads; duplicates (same
        ///     <see cref="GameEvent.Id"/>) are ignored. Safe to call any time — typically once from
        ///     your UserMod2.OnLoad.
        /// </summary>
        public static void AddEvent(GameEvent ev)
        {
            if (AddExternal(ev))
            {
                Log.Info($"External event registered: {ev.Id}");
            }
        }

        /// <summary>
        ///     Remember an external event and make it live right away if a colony is already loaded.
        ///     Returns false if it's unusable or the id is taken.
        /// </summary>
        internal static bool AddExternal(GameEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.Id))
            {
                return false;
            }

            if (External.Exists(e => e.Id == ev.Id))
            {
                return false;
            }

            External.Add(ev);
            Register(ev); // make it available immediately if a colony is already loaded
            return true;
        }

        /// <summary>Drop an external event. Returns true if it was registered.</summary>
        internal static bool RemoveExternal(string id)
        {
            var removed = External.RemoveAll(e => e.Id == id) > 0;
            All.RemoveAll(e => e.Id == id);
            return removed;
        }

        public static void RegisterDefaults()
        {
            All.Clear();
            ResetGroupCooldowns(); // A fresh colony shouldn't inherit what the last one rolled.

            // Register our own duplicant effects (attribute buffs, drowsiness) before the events use them.
            ModEffects.EnsureRegistered();

            // --- Duplicant amounts (ColonyEvents.cs) ---
            Register(new StressSpikeEvent());
            Register(new StressReliefEvent());
            Register(new FeastEvent());
            Register(new ExhaustionEvent());
            Register(new FullBladderEvent());
            Register(new ResearchBoostEvent());
            Register(new KillDupeEvent());

            // --- Attribute buffs/debuffs + drowsiness (AttributeEvents.cs) ---
            Register(new AthleticsUpEvent());
            Register(new AthleticsDownEvent());
            Register(new ConstructionUpEvent());
            Register(new ConstructionDownEvent());
            Register(new ExcavationUpEvent());
            Register(new ExcavationDownEvent());
            Register(new StrengthUpEvent());
            Register(new StrengthDownEvent());
            Register(new SleepyDupesEvent());
            Register(new SlowDupesEvent());
            Register(new SleepNowEvent());
            Register(new TurboDupesEvent());

            // --- Instant order completion (OrderEvents.cs) ---
            Register(new CompleteBuildingsEvent());
            Register(new CompleteDiggingEvent());
            Register(new CompleteResearchEvent());

            // --- World / element events (WorldEvents.cs) ---
            Register(new GlobalWarmingEvent());
            Register(new IceAgeEvent());
            Register(new ReduceOxygenEvent());
            Register(new FloodWaterEvent());
            Register(new FloodPollutedWaterEvent());
            Register(new FloodEthanolEvent());
            Register(new FloodOilEvent());
            Register(new FloodLavaEvent());
            Register(new FloodGoldEvent());
            Register(new FloodNuclearWasteEvent());
            Register(new ElementCommonEvent());
            Register(new ElementMetalEvent());
            Register(new ElementExoticEvent());
            Register(new ElementGasEvent());
            Register(new PoopsplosionEvent());
            Register(new FartEvent());
            Register(new TileTempUpEvent());
            Register(new TileTempDownEvent());

            // --- Spawn / misc events (SpawnEvents.cs) ---
            Register(new SpawnPuftEvent());
            Register(new SpawnPokeshellEvent());
            Register(new SpawnMooCometEvent());
            Register(new SpawnAtmoSuitEvent());
            Register(new SpawnVacillatorEvent());
            Register(new RainMorbEvent());
            Register(new RainSlicksterEvent());
            Register(new RainPacuEvent());
            Register(new RainBeeEvent());
            Register(new RainGoldEvent());
            Register(new RainDiamondEvent());
            Register(new RainMaterialEvent());
            Register(new SkillPointsEvent());
            Register(new SnazzySuitEvent());
            Register(new EclipseEvent());
            Register(new SurpriseEvent());
            Register(new SurpriseBoxEvent());

            // --- Support / disruption (UtilityEvents.cs) ---
            Register(new HealAllEvent());
            Register(new CureDiseasesEvent());
            Register(new NirvanaEvent());
            Register(new ChargeBatteriesEvent());
            Register(new OxygenWaveEvent());
            Register(new FreeHarvestEvent());
            Register(new BlackoutEvent());
            Register(new WiltPlantsEvent());
            Register(new DemolishBuildingEvent());
            Register(new GatherDupesEvent());
            Register(new ScatterDupesEvent());
            Register(new InterruptWorkEvent());

            // --- Light flavour / filler ---
            Register(new CheerEvent());
            Register(new QuietEvent());

            // --- Events contributed by other mods ---
            foreach (var ev in External)
            {
                Register(ev);
            }

            try
            {
                Registering?.Invoke();
            }
            catch (System.Exception e)
            {
                Log.Warn("Registering handler threw: " + e.Message);
            }

            Log.Info($"Registered {All.Count} events.");
        }

        /// <summary>Add an event to the current pool if its Id isn't already present.</summary>
        public static void Register(GameEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.Id) || ById(ev.Id) != null)
            {
                return;
            }

            All.Add(ev);
        }

        public static IReadOnlyList<GameEvent> AllEvents => All;

        /// <summary>
        ///     Pick up to <paramref name="count"/> distinct events for a vote, weighted by
        ///     <see cref="GameEvent.Weight"/>, skipping anything whose <see cref="GameEvent.CanRun"/>
        ///     says no, and damping groups that fired recently.
        /// </summary>
        public static List<GameEvent> PickForVote(int count)
        {
            TickGroupCooldowns();

            var context = BuildDrawContext();
            var cycle = EventContext.GetInt(context, EventContext.Cycle, -1);
            var allowed = AllowedDanger(cycle);
            var hardCap = (int)ModConfig.Instance.MaxEventDanger;

            var pool = new List<GameEvent>();
            var weights = new List<int>();
            BuildPool(allowed, context, pool, weights);

            // A vote with one option isn't a vote. If the danger cap plus the events' own conditions
            // starved the pool, open the cap up a step at a time — but never past what the streamer
            // allowed, because that's a promise, not a preference. If we still can't fill two, the
            // caller decides what to do.
            while (pool.Count < 2 && allowed < hardCap)
            {
                allowed++;
                BuildPool(allowed, context, pool, weights);
                Log.Info($"Not enough events to vote on; allowing danger up to {allowed} this round.");
            }

            var chosen = new List<GameEvent>();
            count = System.Math.Min(count, pool.Count);
            for (var i = 0; i < count; i++)
            {
                var idx = TakeWeightedIndex(weights);
                chosen.Add(pool[idx]);
                pool.RemoveAt(idx);
                weights.RemoveAt(idx);
            }

            return chosen;
        }

        /// <summary>
        ///     Pick one random event that is allowed right now — same danger cap, conditions and
        ///     weights as a vote draw. Used by the Surprise event, which would otherwise be a hole
        ///     straight through the danger cap: it fires an event of its own choosing.
        /// </summary>
        internal static GameEvent PickRandomAllowed(string excludeId)
        {
            var context = BuildDrawContext();
            var pool = new List<GameEvent>();
            var weights = new List<int>();
            BuildPool(AllowedDanger(EventContext.GetInt(context, EventContext.Cycle, -1)), context,
                pool, weights, excludeId);

            if (pool.Count == 0)
            {
                return null;
            }

            return pool[TakeWeightedIndex(weights)];
        }

        /// <summary>Everything eligible right now, with the weight it should be drawn at.</summary>
        private static void BuildPool(int allowedDanger, object context, List<GameEvent> pool,
            List<int> weights, string excludeId = null)
        {
            pool.Clear();
            weights.Clear();

            foreach (var ev in All)
            {
                if (ev.Id == excludeId || ev.Danger > allowedDanger)
                {
                    continue;
                }

                var weight = EffectiveWeight(ev);
                if (weight <= 0 || !CanRunSafely(ev, context))
                {
                    continue;
                }

                pool.Add(ev);
                weights.Add(weight);
            }
        }

        /// <summary>
        ///     The worst danger chat may be offered at this cycle. With ramping off it's simply the
        ///     configured cap; with it on, the cap opens up linearly and reaches the configured
        ///     ceiling at MaxDangerAtCycle. An unknown cycle (no clock yet) doesn't restrict
        ///     anything — the alternative would be an empty vote for anyone whose game hasn't
        ///     finished loading.
        /// </summary>
        internal static int AllowedDanger(int cycle)
        {
            var cfg = ModConfig.Instance;
            var cap = (int)cfg.MaxEventDanger;

            // A hand-edited "full danger at cycle 0" means "don't hold anything back", so take it at
            // its word instead of ramping over a single cycle.
            var fullAt = cfg.MaxDangerAtCycle;
            if (!cfg.ScaleDifficultyWithCycles || cycle < 0 || fullAt <= 0 || cycle >= fullAt)
            {
                return cap;
            }

            var allowed = (int)System.Math.Floor((double)cap * cycle / fullAt);
            return System.Math.Min(cap, System.Math.Max(0, allowed));
        }

        /// <summary>
        ///     Note that an event fired, so it and its group are less likely for the next few draws.
        /// </summary>
        internal static void NoteTriggered(GameEvent ev)
        {
            if (ev != null)
            {
                GroupCooldowns[GroupKey(ev)] = GroupCooldownDraws;
            }
        }

        /// <summary>Forget every cooldown — a new colony starts with a clean slate.</summary>
        internal static void ResetGroupCooldowns() => GroupCooldowns.Clear();

        /// <summary>An event with no group is damped on its own, so a lone event can't repeat either.</summary>
        private static string GroupKey(GameEvent ev) =>
            string.IsNullOrEmpty(ev.GroupId) ? "event:" + ev.Id : "group:" + ev.GroupId;

        private static void TickGroupCooldowns()
        {
            if (GroupCooldowns.Count == 0)
            {
                return;
            }

            foreach (var key in new List<string>(GroupCooldowns.Keys))
            {
                var left = GroupCooldowns[key] - 1;
                if (left <= 0)
                {
                    GroupCooldowns.Remove(key);
                }
                else
                {
                    GroupCooldowns[key] = left;
                }
            }
        }

        /// <summary>
        ///     Weight after the group cooldown: halved per draw still on the clock, never to zero.
        ///     Damped, not banned — a recent group is unlikely, not impossible.
        /// </summary>
        private static int EffectiveWeight(GameEvent ev)
        {
            var weight = ev.Weight;
            if (weight <= 0)
            {
                return 0; // EventWeight.Never: registered, but never drawn.
            }

            if (GroupCooldowns.TryGetValue(GroupKey(ev), out var draws) && draws > 0)
            {
                weight >>= System.Math.Min(draws, 8);
            }

            return System.Math.Max(1, weight);
        }

        private static bool CanRunSafely(GameEvent ev, object context)
        {
            try
            {
                return ev.CanRun(context);
            }
            catch (System.Exception e)
            {
                Log.Warn($"Condition of event '{ev.Id}' threw, skipping it: {e.Message}");
                return false;
            }
        }

        /// <summary>What a condition gets to look at when the options are drawn.</summary>
        private static Dictionary<string, object> BuildDrawContext()
        {
            var cycle = -1;
            try
            {
                var clock = GameClock.Instance;
                if (clock != null) cycle = clock.GetCycle();
            }
            catch
            {
                // No clock yet (drawing before a colony is up): conditions just see -1.
            }

            return new Dictionary<string, object> { { EventContext.Cycle, cycle } };
        }

        /// <summary>Index into <paramref name="weights"/>, chance proportional to each weight.</summary>
        private static int TakeWeightedIndex(List<int> weights)
        {
            var total = 0;
            foreach (var w in weights)
            {
                total += w;
            }

            if (total <= 0)
            {
                return UnityEngine.Random.Range(0, weights.Count);
            }

            var roll = UnityEngine.Random.Range(0, total);
            for (var i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }

            return weights.Count - 1; // Unreachable unless the weights changed under us.
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
