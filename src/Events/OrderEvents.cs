using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TwitchColony.Events
{
    // "Order completion" events — instantly finish player-queued work. Constructable.FinishConstruction
    // (private, invoked directly) and WorldDamage for digging: both avoid needing a live worker, which
    // is what made the earlier InstantlyFinish(null) approach throw a NullReferenceException.

    /// <summary>
    ///     Instantly finish every planned building whose required materials are actually available in
    ///     the colony — i.e. sitting in storage or loose on the ground (the game's own
    ///     <see cref="BuildingDef.MaterialsAvailable"/> check). Builds with no material on hand (e.g. a
    ///     luxury bed needing plastic you don't have) are skipped rather than built for free.
    /// </summary>
    public sealed class CompleteBuildingsEvent : GameEvent
    {
        public override string Id => "complete_buildings";
        public override string DisplayName => "Instant build (if material available)";

        // Constructable.FinishConstruction(UtilityConnections, WorkerBase) is private; invoke it with
        // nulls to complete the build without a worker (workerForGameplayEvent is only for xp/events).
        private static readonly MethodInfo FinishConstruction =
            AccessTools.DeclaredMethod(typeof(Constructable), "FinishConstruction");

        public override void Trigger()
        {
            Log.Info("Event: CompleteBuildings");
            if (FinishConstruction == null)
            {
                Log.Warn("CompleteBuildings: FinishConstruction method not found.");
                return;
            }

            var all = Object.FindObjectsOfType<Constructable>();
            int done = 0, skipped = 0;
            foreach (var c in all)
            {
                if (c == null)
                {
                    continue;
                }

                try
                {
                    var building = c.GetComponent<Building>();
                    var cell = Grid.PosToCell(c.transform.position);
                    if (building == null || building.Def == null || !Grid.IsValidCell(cell))
                    {
                        continue;
                    }

                    var world = ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]);
                    // Skip builds whose materials aren't available anywhere in the colony (storage/ground).
                    if (world != null && !building.Def.MaterialsAvailable(c.SelectedElementsTags, world))
                    {
                        skipped++;
                        continue;
                    }

                    // Put the chosen materials into the build storage at a sane temperature first —
                    // otherwise FinishConstruction builds from empty storage and the game errors on a
                    // 0 K building ("temperature of zero which has always been an error").
                    SeedBuildMaterials(c, building.Def, cell);
                    FinishConstruction.Invoke(c, new object[] { null, null });
                    done++;
                }
                catch (System.Exception e)
                {
                    Log.Warn("CompleteBuildings: " + e.Message);
                }
            }

            Log.Info($"CompleteBuildings: finished {done}, skipped {skipped} (no material).");
        }

        /// <summary>
        ///     Fill the constructable's build storage with its selected materials (at ambient/room
        ///     temperature) so FinishConstruction produces a building with a valid temperature.
        /// </summary>
        private static void SeedBuildMaterials(Constructable c, BuildingDef def, int cell)
        {
            var storage = Traverse.Create(c).Field<Storage>("storage").Value;
            var tags = c.SelectedElementsTags;
            if (storage == null || tags == null)
            {
                return;
            }

            var temp = Grid.IsValidCell(cell) && Grid.Temperature[cell] > 1f ? Grid.Temperature[cell] : 293.15f;
            var masses = def.Mass;
            for (var i = 0; i < tags.Count; i++)
            {
                var mass = masses != null && i < masses.Length ? masses[i] : 100f;
                var el = ElementLoader.GetElement(tags[i]);
                if (el == null || mass <= 0f)
                {
                    continue;
                }

                storage.AddOre(el.id, mass, temp, byte.MaxValue, 0, false, false);
            }
        }
    }

    /// <summary>Instantly finish every outstanding dig order.</summary>
    public sealed class CompleteDiggingEvent : GameEvent
    {
        public override string Id => "complete_digging";
        public override string DisplayName => "Instant dig (finish dig orders)";

        public override void Trigger()
        {
            Log.Info("Event: CompleteDigging");
            if (WorldDamage.Instance == null)
            {
                Log.Warn("CompleteDigging: no WorldDamage instance.");
                return;
            }

            var all = Object.FindObjectsOfType<Diggable>();
            var done = 0;
            foreach (var d in all)
            {
                if (d == null)
                {
                    continue;
                }

                try
                {
                    var cell = d.GetCell();
                    if (!Grid.IsValidCell(cell) || !Grid.Solid[cell])
                    {
                        continue;
                    }

                    // Big damage value fully mines the natural tile and drops its resources.
                    WorldDamage.Instance.ApplyDamage(cell, 10000f, cell, "TwitchColony", null);
                    done++;
                }
                catch (System.Exception e)
                {
                    Log.Warn("CompleteDigging: " + e.Message);
                }
            }

            Log.Info($"CompleteDigging: finished {done} dig order(s).");
        }
    }

    /// <summary>Instantly complete the research currently being worked on.</summary>
    public sealed class CompleteResearchEvent : GameEvent
    {
        public override string Id => "complete_research";
        public override string DisplayName => "Instant research (finish current)";

        public override void Trigger()
        {
            Log.Info("Event: CompleteResearch");
            if (Research.Instance == null)
            {
                Log.Warn("CompleteResearch: no Research instance.");
                return;
            }

            var ti = Research.Instance.GetActiveResearch();
            if (ti == null || ti.IsComplete())
            {
                Log.Info("CompleteResearch: nothing is currently being researched.");
                return;
            }

            ti.Purchased();
            Game.Instance.Trigger((int) GameHashes.ResearchComplete, ti.tech);
            Log.Info($"CompleteResearch: completed '{ti.tech?.Id}'.");
        }
    }

    /// <summary>Turbo dupes: a big temporary boost to movement and work speed for 30 seconds.</summary>
    public sealed class TurboDupesEvent : GameEvent
    {
        public override string Id => "turbo_dupes";
        public override string DisplayName => "Turbo dupes (30s speed boost)";

        public override void Trigger()
        {
            Log.Info("Event: TurboDupes");
            ModEffects.ApplyToAll(ModEffects.Turbo, "⚡");
        }
    }
}
