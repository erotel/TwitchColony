using UnityEngine;

namespace TwitchColony.Events
{
    // World / element events. Independent reimplementations using only vanilla Grid + SimMessages API.

    /// <summary>Global warming: heat every gas/liquid/ice cell by ~20 K (clamped inside its phase range).</summary>
    public sealed class GlobalWarmingEvent : GameEvent
    {
        public override string Id => "global_warming";
        public override string DisplayName => "Global warming (+heat)";

        public override void Trigger()
        {
            Log.Info("Event: GlobalWarming");
            for (var cell = 0; cell < Grid.CellCount; cell++)
            {
                if (!Grid.IsValidCell(cell))
                {
                    continue;
                }

                var el = Grid.Element[cell];
                if (el == null || !(Grid.IsGas(cell) || Grid.IsLiquid(cell) || el.HasTag(GameTags.IceOre)))
                {
                    continue;
                }

                var target = Mathf.Clamp(Grid.Temperature[cell] + 20f, el.lowTemp + 5f, el.highTemp - 5f);
                target = Mathf.Clamp(target, 1f, 9999f);
                Cells.NudgeTemp(cell, target, SimMessages.EnergySourceID.DebugHeat);
            }
        }
    }

    /// <summary>Ice age: cool every liquid cell toward just below its freezing point (magma excepted).</summary>
    public sealed class IceAgeEvent : GameEvent
    {
        public override string Id => "ice_age";
        public override string DisplayName => "Ice age (-heat)";

        public override void Trigger()
        {
            Log.Info("Event: IceAge");
            for (var cell = 0; cell < Grid.CellCount; cell++)
            {
                if (!Grid.IsValidCell(cell) || !Grid.IsLiquid(cell))
                {
                    continue;
                }

                var el = Grid.Element[cell];
                if (el == null || el.id == SimHashes.Magma)
                {
                    continue;
                }

                var target = Mathf.Clamp(el.lowTemp - 6f, 1f, 9999f);
                if (Grid.Temperature[cell] <= target)
                {
                    continue;
                }

                Cells.NudgeTemp(cell, target, SimMessages.EnergySourceID.DebugCool);
            }
        }
    }

    /// <summary>Suffocation: remove 80% of the mass of every breathable-gas cell.</summary>
    public sealed class ReduceOxygenEvent : GameEvent
    {
        public override string Id => "reduce_oxygen";
        public override string DisplayName => "Suffocation (remove oxygen)";

        private static readonly CellModifyMassEvent MassEvent =
            new CellModifyMassEvent("TwitchColonyMass", "Modified by Twitch Colony");

        public override void Trigger()
        {
            Log.Info("Event: ReduceOxygen");
            for (var cell = 0; cell < Grid.CellCount; cell++)
            {
                if (!Grid.IsValidCell(cell) || Grid.WorldIdx[cell] == ClusterManager.INVALID_WORLD_IDX)
                {
                    continue;
                }

                var el = Grid.Element[cell];
                if (el == null || !el.HasTag(GameTags.Breathable) || Grid.Mass[cell] <= 0.01f)
                {
                    continue;
                }

                var remove = Grid.Mass[cell] * 0.8f;
                SimMessages.ModifyMass(cell, -remove, byte.MaxValue, 0, MassEvent,
                    Grid.Temperature[cell], el.id);
            }
        }
    }

    /// <summary>Base class for "flood a patch near a random dupe with an element".</summary>
    public abstract class FloodEventBase : GameEvent
    {
        protected abstract SimHashes Element { get; }
        protected virtual float Mass => 1000f;
        protected virtual int Radius => 3;
        protected virtual string Bubble => "🌊";

        public override void Trigger()
        {
            Log.Info("Event: Flood " + Element);
            var center = Cells.RandomDupeCell();
            if (!Grid.IsValidCell(center))
            {
                return;
            }

            foreach (var cell in Cells.Square(center, Radius))
            {
                if (!Grid.IsSolidCell(cell))
                {
                    Cells.Place(cell, Element, Mass);
                }
            }
        }
    }

    public sealed class FloodWaterEvent : FloodEventBase
    {
        public override string Id => "flood_water";
        public override string DisplayName => "Flash flood (water)";
        protected override SimHashes Element => SimHashes.Water;
    }

    public sealed class FloodPollutedWaterEvent : FloodEventBase
    {
        public override string Id => "flood_polluted_water";
        public override string DisplayName => "Flash flood (polluted water)";
        protected override SimHashes Element => SimHashes.DirtyWater;
    }

    public sealed class FloodEthanolEvent : FloodEventBase
    {
        public override string Id => "flood_ethanol";
        public override string DisplayName => "Flash flood (ethanol)";
        protected override SimHashes Element => SimHashes.Ethanol;
    }

    public sealed class FloodOilEvent : FloodEventBase
    {
        public override string Id => "flood_oil";
        public override string DisplayName => "Flash flood (crude oil)";
        protected override SimHashes Element => SimHashes.CrudeOil;
    }

    public sealed class FloodLavaEvent : FloodEventBase
    {
        public override string Id => "flood_lava";
        public override string DisplayName => "Flash flood (LAVA!)";
        protected override SimHashes Element => SimHashes.Magma;
        protected override float Mass => 200f;
        protected override string Bubble => "🌋";
    }

    public sealed class FloodGoldEvent : FloodEventBase
    {
        public override string Id => "flood_gold";
        public override string DisplayName => "Flash flood (molten gold)";
        protected override SimHashes Element => SimHashes.MoltenGold;
        protected override float Mass => 500f;
    }

    public sealed class FloodNuclearWasteEvent : FloodEventBase
    {
        public override string Id => "flood_nuclear_waste";
        public override string DisplayName => "Flash flood (nuclear waste)";
        protected override SimHashes Element => SimHashes.NuclearWaste;
        protected override string Bubble => "☢️";
    }

    /// <summary>Base class for "pick a random element from a pool and spawn a 3×3 patch near a dupe".</summary>
    public abstract class ElementPoolEventBase : GameEvent
    {
        protected abstract SimHashes[] Pool { get; }
        protected virtual float Mass => 1400f;

        public override void Trigger()
        {
            Log.Info("Event: ElementPool " + Id);
            var pool = Pool;
            if (pool == null || pool.Length == 0)
            {
                return;
            }

            var element = pool[Random.Range(0, pool.Length)];
            var center = Cells.RandomDupeCell();
            if (!Grid.IsValidCell(center))
            {
                return;
            }

            foreach (var cell in Cells.Square(center, 1))
            {
                if (!Grid.IsSolidCell(cell))
                {
                    Cells.Place(cell, element, Mass);
                }
            }
        }
    }

    public sealed class ElementCommonEvent : ElementPoolEventBase
    {
        public override string Id => "element_common";
        public override string DisplayName => "Element dump (common)";
        protected override SimHashes[] Pool => new[]
        {
            SimHashes.Algae, SimHashes.OxyRock, SimHashes.SlimeMold, SimHashes.IgneousRock,
            SimHashes.Rust, SimHashes.Sand, SimHashes.Ice, SimHashes.Carbon, SimHashes.Dirt, SimHashes.Salt,
        };
    }

    public sealed class ElementMetalEvent : ElementPoolEventBase
    {
        public override string Id => "element_metal";
        public override string DisplayName => "Element dump (metal ore)";
        protected override SimHashes[] Pool => new[]
        {
            SimHashes.Cuprite, SimHashes.FoolsGold, SimHashes.IronOre, SimHashes.Electrum,
            SimHashes.Cobaltite, SimHashes.GoldAmalgam, SimHashes.AluminumOre,
        };
    }

    public sealed class ElementExoticEvent : ElementPoolEventBase
    {
        public override string Id => "element_exotic";
        public override string DisplayName => "Element dump (exotic)";
        protected override float Mass => 1000f;
        protected override SimHashes[] Pool => new[]
        {
            SimHashes.Diamond, SimHashes.Ceramic, SimHashes.Fossil, SimHashes.Graphite,
            SimHashes.Niobium, SimHashes.Tungsten, SimHashes.Resin,
        };
    }

    public sealed class ElementGasEvent : ElementPoolEventBase
    {
        public override string Id => "element_gas";
        public override string DisplayName => "Gas leak (random gas)";
        protected override float Mass => 20f;
        protected override SimHashes[] Pool => new[]
        {
            SimHashes.ChlorineGas, SimHashes.Hydrogen, SimHashes.Methane,
            SimHashes.SourGas, SimHashes.Steam, SimHashes.EthanolGas,
        };
    }

    /// <summary>Poopsplosion: dump polluted dirt on every dupe's tile and its neighbours.</summary>
    public sealed class PoopsplosionEvent : GameEvent
    {
        public override string Id => "poopsplosion";
        public override string DisplayName => "Poopsplosion (polluted dirt)";

        public override void Trigger()
        {
            Log.Info("Event: Poopsplosion");
            foreach (var dupe in DupeUtil.Live())
            {
                var center = Grid.PosToCell(dupe.transform.position);
                foreach (var cell in Cells.Square(center, 1))
                {
                    if (!Grid.IsSolidCell(cell))
                    {
                        Cells.Place(cell, SimHashes.ToxicSand, 1500f);
                    }
                }

                UI.SpeechBubbles.ShowRaw(dupe.transform, "💩");
            }
        }
    }

    /// <summary>Fart: emit a puff of methane at every dupe's tile.</summary>
    public sealed class FartEvent : GameEvent
    {
        public override string Id => "fart";
        public override string DisplayName => "Fart (methane)";

        public override void Trigger()
        {
            Log.Info("Event: Fart");
            foreach (var dupe in DupeUtil.Live())
            {
                var cell = Grid.PosToCell(dupe.transform.position);
                if (Grid.IsValidCell(cell) && !Grid.IsSolidCell(cell))
                {
                    Cells.Place(cell, SimHashes.Methane, 25f);
                }

                UI.SpeechBubbles.ShowRaw(dupe.transform, "💨");
            }
        }
    }

    /// <summary>Base class for changing the temperature of solid tiles around a random dupe.</summary>
    public abstract class TileTempEventBase : GameEvent
    {
        protected abstract float Delta { get; }
        protected abstract SimMessages.EnergySourceID Source { get; }

        public override void Trigger()
        {
            Log.Info("Event: TileTemp " + Id);
            var center = Cells.RandomDupeCell();
            if (!Grid.IsValidCell(center))
            {
                return;
            }

            foreach (var cell in Cells.Square(center, 4))
            {
                if (Grid.IsSolidCell(cell))
                {
                    var target = Mathf.Clamp(Grid.Temperature[cell] + Delta, 1f, 9999f);
                    Cells.NudgeTemp(cell, target, Source);
                }
            }
        }
    }

    public sealed class TileTempUpEvent : TileTempEventBase
    {
        public override string Id => "tile_temp_up";
        public override string DisplayName => "Heat wave (warm tiles)";
        protected override float Delta => +40f;
        protected override SimMessages.EnergySourceID Source => SimMessages.EnergySourceID.DebugHeat;
    }

    public sealed class TileTempDownEvent : TileTempEventBase
    {
        public override string Id => "tile_temp_down";
        public override string DisplayName => "Cold snap (chill tiles)";
        protected override float Delta => -40f;
        protected override SimMessages.EnergySourceID Source => SimMessages.EnergySourceID.DebugCool;
    }
}
