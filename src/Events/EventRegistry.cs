using System.Collections.Generic;

namespace TwitchColony.Events
{
    /// <summary>
    ///     Holds all registered events and picks a random subset for each vote.
    ///
    ///     MODDING API: other mods can contribute their own events. Reference this assembly
    ///     (TwitchColony.dll, Private=false), subclass <see cref="GameEvent"/>, and call
    ///     <see cref="AddEvent"/> once from your UserMod2.OnLoad — the event then joins the vote
    ///     pool, HUD, and chat announcements automatically. External events persist across colony
    ///     reloads. Advanced: subscribe to <see cref="Registering"/> to add events on each load.
    /// </summary>
    public static class EventRegistry
    {
        private static readonly List<GameEvent> All = new List<GameEvent>();

        // Events contributed by other mods; re-added on every colony load (they survive the clear).
        private static readonly List<GameEvent> External = new List<GameEvent>();

        /// <summary>Raised after the default + external events are registered on each colony load.</summary>
        public static event System.Action Registering;

        /// <summary>
        ///     Add an event from another mod. Persists across colony reloads; duplicates (same
        ///     <see cref="GameEvent.Id"/>) are ignored. Safe to call any time — typically once from
        ///     your UserMod2.OnLoad.
        /// </summary>
        public static void AddEvent(GameEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.Id))
            {
                return;
            }

            if (!External.Exists(e => e.Id == ev.Id))
            {
                External.Add(ev);
            }

            Register(ev); // make it available immediately if a colony is already loaded
            Log.Info($"External event registered: {ev.Id}");
        }

        public static void RegisterDefaults()
        {
            All.Clear();

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
