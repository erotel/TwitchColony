using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TwitchColony.Events
{
    // Support (help) and disruption events. Independent implementations using vanilla API only:
    // Db amounts / sicknesses, Battery, Growing, Deconstructable, ChoreDriver, dupe teleport.

    // ---------------------------------------------------------------------------------------------
    // Positive
    // ---------------------------------------------------------------------------------------------

    /// <summary>Field medic: fully heal every duplicant.</summary>
    public sealed class HealAllEvent : GameEvent
    {
        public override string Id => "heal_all";
        public override string DisplayName => "Field medic (heal all dupes)";
        public override void Trigger()
        {
            Log.Info("Event: HealAll");
            DupeUtil.ApplyAmountToAll(Db.Get().Amounts.HitPoints, +1000f, "❤️");
        }
    }

    /// <summary>Cure every active disease on every duplicant.</summary>
    public sealed class CureDiseasesEvent : GameEvent
    {
        public override string Id => "cure_diseases";
        public override string DisplayName => "Cure all diseases";
        public override void Trigger()
        {
            Log.Info("Event: CureDiseases");
            var all = Db.Get().Sicknesses.resources;
            foreach (var dupe in DupeUtil.Live())
            {
                var mods = dupe.GetComponent<MinionModifiers>();
                if (mods == null || mods.sicknesses == null || !mods.sicknesses.IsInfected())
                {
                    continue;
                }

                foreach (var s in all)
                {
                    try { mods.sicknesses.Cure(s.Id); }
                    catch { /* ignore individual cure failures */ }
                }

                UI.SpeechBubbles.ShowRaw(dupe.transform, "💊");
            }
        }
    }

    /// <summary>Zen nirvana: wipe everyone's stress and grant a short morale boost.</summary>
    public sealed class NirvanaEvent : GameEvent
    {
        public override string Id => "nirvana";
        public override string DisplayName => "Zen nirvana (calm + happy)";
        public override void Trigger()
        {
            Log.Info("Event: Nirvana");
            DupeUtil.ApplyAmountToAll(Db.Get().Amounts.Stress, -1000f); // clamps to 0
            ModEffects.ApplyToAll(ModEffects.Zen, "🧘");
        }
    }

    /// <summary>Power surge: fill every battery to capacity.</summary>
    public sealed class ChargeBatteriesEvent : GameEvent
    {
        public override string Id => "charge_batteries";
        public override string DisplayName => "Power surge (charge batteries)";
        public override void Trigger()
        {
            Log.Info("Event: ChargeBatteries");
            SetAllBatteries(b => b.Capacity);
        }

        /// <summary>Set each battery's stored charge via a selector (Capacity for full, 0 for empty).</summary>
        internal static void SetAllBatteries(System.Func<Battery, float> value)
        {
            var items = Components.Batteries?.Items;
            if (items == null)
            {
                return;
            }

            foreach (var b in items)
            {
                if (b == null)
                {
                    continue;
                }

                // JoulesAvailable has no public setter; write the backing field directly.
                try { Traverse.Create(b).Field("joulesAvailable").SetValue(value(b)); }
                catch (System.Exception e) { Log.Warn("Battery set failed: " + e.Message); }
            }
        }
    }

    /// <summary>Fresh air: flood a patch of oxygen around every duplicant.</summary>
    public sealed class OxygenWaveEvent : GameEvent
    {
        public override string Id => "oxygen_wave";
        public override string DisplayName => "Oxygen wave (fresh air)";
        public override void Trigger()
        {
            Log.Info("Event: OxygenWave");
            foreach (var dupe in DupeUtil.Live())
            {
                var center = Grid.PosToCell(dupe.transform.position);
                foreach (var cell in Cells.Square(center, 2))
                {
                    if (!Grid.IsSolidCell(cell))
                    {
                        Cells.Place(cell, SimHashes.Oxygen, 1.8f);
                    }
                }
            }
        }
    }

    /// <summary>Bountiful harvest: instantly ripen every crop so it's ready to pick.</summary>
    public sealed class FreeHarvestEvent : GameEvent
    {
        public override string Id => "free_harvest";
        public override string DisplayName => "Bountiful harvest (ripen crops)";
        public override void Trigger()
        {
            Log.Info("Event: FreeHarvest");
            ForEachGrowing(g => g.OverrideMaturityLevel(1f));
        }

        /// <summary>Run an action on the Growing component of every crop.</summary>
        internal static void ForEachGrowing(System.Action<Growing> action)
        {
            var items = Components.Crops?.Items;
            if (items == null)
            {
                return;
            }

            foreach (var crop in items)
            {
                if (crop == null)
                {
                    continue;
                }

                var g = crop.GetComponent<Growing>();
                if (g == null)
                {
                    continue;
                }

                try { action(g); }
                catch (System.Exception e) { Log.Warn("Growing op failed: " + e.Message); }
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Negative / chaos
    // ---------------------------------------------------------------------------------------------

    /// <summary>Blackout: drain every battery to empty.</summary>
    public sealed class BlackoutEvent : GameEvent
    {
        public override string Id => "blackout";
        public override string DisplayName => "Blackout (drain batteries)";
        public override void Trigger()
        {
            Log.Info("Event: Blackout");
            ChargeBatteriesEvent.SetAllBatteries(_ => 0f);
        }
    }

    /// <summary>Blight: set every crop's growth back to zero.</summary>
    public sealed class WiltPlantsEvent : GameEvent
    {
        public override string Id => "wilt_plants";
        public override string DisplayName => "Blight (set back crops)";
        public override void Trigger()
        {
            Log.Info("Event: WiltPlants");
            FreeHarvestEvent.ForEachGrowing(g => g.OverrideMaturityLevel(0f));
        }
    }

    /// <summary>Wrecking ball: instantly demolish one random building (drops its materials).</summary>
    public sealed class DemolishBuildingEvent : GameEvent
    {
        public override string Id => "demolish_building";
        public override string DisplayName => "Wrecking ball (demolish a building)";
        public override void Trigger()
        {
            Log.Info("Event: DemolishBuilding");
            var candidates = new List<Deconstructable>();
            var items = Components.BuildingCompletes?.Items;
            if (items == null)
            {
                return;
            }

            foreach (var bc in items)
            {
                if (bc == null)
                {
                    continue;
                }

                var d = bc.GetComponent<Deconstructable>();
                if (d != null && d.allowDeconstruction && !d.HasBeenDestroyed)
                {
                    candidates.Add(d);
                }
            }

            if (candidates.Count == 0)
            {
                Log.Info("DemolishBuilding: nothing to demolish.");
                return;
            }

            var pick = candidates[Random.Range(0, candidates.Count)];
            try
            {
                pick.ForceDestroyAndGetMaterials();
                Log.Info("DemolishBuilding: destroyed " + pick.name);
            }
            catch (System.Exception e)
            {
                Log.Warn("DemolishBuilding failed: " + e.Message);
            }
        }
    }

    /// <summary>Roll call: teleport every duplicant to a printing pod.</summary>
    public sealed class GatherDupesEvent : GameEvent
    {
        public override string Id => "gather_dupes";
        public override string DisplayName => "Roll call (gather dupes at pod)";
        public override void Trigger()
        {
            Log.Info("Event: GatherDupes");
            var pads = Components.Telepads?.Items;
            if (pads == null || pads.Count == 0)
            {
                Log.Warn("GatherDupes: no telepad found.");
                return;
            }

            var pos = pads[Random.Range(0, pads.Count)].transform.position;
            foreach (var dupe in DupeUtil.Live())
            {
                dupe.transform.position = pos + new Vector3(Random.Range(-1.5f, 1.5f), 0f, 0f);
                UI.SpeechBubbles.ShowRaw(dupe.transform, "📣");
            }
        }
    }

    /// <summary>Teleport chaos: scatter every duplicant to a random open cell.</summary>
    public sealed class ScatterDupesEvent : GameEvent
    {
        public override string Id => "scatter_dupes";
        public override string DisplayName => "Teleport chaos (scatter dupes)";
        public override void Trigger()
        {
            Log.Info("Event: ScatterDupes");
            foreach (var dupe in DupeUtil.Live())
            {
                var cell = RandomOpenCell();
                if (!Grid.IsValidCell(cell))
                {
                    continue;
                }

                dupe.transform.position = Grid.CellToPosCBC(cell, Grid.SceneLayer.Move);
                UI.SpeechBubbles.ShowRaw(dupe.transform, "🌀");
            }
        }

        private static int RandomOpenCell()
        {
            for (var i = 0; i < 300; i++)
            {
                var c = Random.Range(0, Grid.CellCount);
                if (Grid.IsValidCell(c) && !Grid.IsSolidCell(c) &&
                    Grid.WorldIdx[c] != ClusterManager.INVALID_WORLD_IDX)
                {
                    return c;
                }
            }

            return Grid.InvalidCell;
        }
    }

    /// <summary>Distraction: make every duplicant drop whatever they're currently doing.</summary>
    public sealed class InterruptWorkEvent : GameEvent
    {
        public override string Id => "interrupt_work";
        public override string DisplayName => "Distraction (interrupt all work)";
        public override void Trigger()
        {
            Log.Info("Event: InterruptWork");
            foreach (var dupe in DupeUtil.Live())
            {
                var driver = dupe.GetComponent<ChoreDriver>();
                if (driver == null || !driver.HasChore())
                {
                    continue;
                }

                try
                {
                    driver.StopChore();
                    UI.SpeechBubbles.ShowRaw(dupe.transform, "❓");
                }
                catch (System.Exception e)
                {
                    Log.Warn("InterruptWork: " + e.Message);
                }
            }
        }
    }
}
