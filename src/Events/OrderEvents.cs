using UnityEngine;

namespace TwitchColony.Events
{
    // "Order completion" events — instantly finish player-queued work. Vanilla API only:
    // Workable.InstantlyFinish (inherited by Constructable/Diggable) and Research.GetActiveResearch.

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

        public override void Trigger()
        {
            Log.Info("Event: CompleteBuildings");
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

                    c.InstantlyFinish(null);
                    done++;
                }
                catch (System.Exception e)
                {
                    Log.Warn("CompleteBuildings: " + e.Message);
                }
            }

            Log.Info($"CompleteBuildings: finished {done}, skipped {skipped} (no material).");
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
                    d.InstantlyFinish(null);
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
