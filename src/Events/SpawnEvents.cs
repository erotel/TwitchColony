using System.Collections.Generic;
using TwitchColony.Api;
using System.Linq;
using UnityEngine;

namespace TwitchColony.Events
{
    // Spawn / misc events. Independent reimplementations using vanilla prefab IDs and game API.

    /// <summary>Base class for "instantiate one vanilla prefab near a random dupe".</summary>
    public abstract class SpawnPrefabEventBase : GameEvent
    {
        public override string GroupId => "spawn";

        protected abstract string PrefabId { get; }

        public override void Trigger()
        {
            Log.Info("Event: Spawn " + PrefabId);
            var cell = Cells.RandomDupeCell();
            if (!Grid.IsValidCell(cell))
            {
                return;
            }

            var prefab = Assets.GetPrefab((Tag) PrefabId);
            if (prefab == null)
            {
                Log.Warn("Spawn: unknown prefab " + PrefabId);
                return;
            }

            var obj = Util.KInstantiate(prefab, Cells.PosOf(cell, Grid.SceneLayer.Creatures));
            obj.SetActive(true);
        }
    }

    public sealed class SpawnPuftEvent : SpawnPrefabEventBase
    {
        public override string Id => "spawn_puft";
        public override string DisplayName => "Spawn a Puft";
        protected override string PrefabId => PuftConfig.ID;
    }

    public sealed class SpawnPokeshellEvent : SpawnPrefabEventBase
    {
        public override string Id => "spawn_pokeshell";
        public override int Danger => (int)EventDanger.Small;
        public override string DisplayName => "Spawn a Pokeshell";
        protected override string PrefabId => CrabConfig.ID;
    }

    public sealed class SpawnMooCometEvent : SpawnPrefabEventBase
    {
        public override string Id => "spawn_moo_comet";
        public override int Danger => (int)EventDanger.Medium;
        public override string DisplayName => "Spawn a Gassy Moo comet";
        protected override string PrefabId => GassyMooCometConfig.ID;
    }

    public sealed class SpawnAtmoSuitEvent : SpawnPrefabEventBase
    {
        public override string Id => "spawn_atmo_suit";
        public override string DisplayName => "Spawn an Atmo Suit";
        protected override string PrefabId => AtmoSuitConfig.ID;
    }

    public sealed class SpawnVacillatorEvent : SpawnPrefabEventBase
    {
        public override string Id => "spawn_vacillator";
        public override string DisplayName => "Spawn a Vacillator charge";
        protected override string PrefabId => GeneShufflerRechargeConfig.ID;
    }

    /// <summary>Base class for "rain N of a vanilla critter prefab from the sky".</summary>
    public abstract class RainCritterEventBase : GameEvent
    {
        public override int Danger => (int)EventDanger.Small;
        public override string GroupId => "rain";

        protected abstract string PrefabId { get; }
        protected virtual int Count => 10;

        public override void Trigger()
        {
            Log.Info("Event: Rain " + PrefabId);
            RainController.RainPrefab((Tag) PrefabId, Count);
        }
    }

    public sealed class RainMorbEvent : RainCritterEventBase
    {
        public override string Id => "rain_morb";
        public override int Danger => (int)EventDanger.Medium;
        public override string DisplayName => "Raining Morbs";
        protected override string PrefabId => GlomConfig.ID;
    }

    public sealed class RainSlicksterEvent : RainCritterEventBase
    {
        public override string Id => "rain_slickster";
        public override string DisplayName => "Raining Slicksters";
        protected override string PrefabId => OilFloaterConfig.ID;
    }

    public sealed class RainPacuEvent : RainCritterEventBase
    {
        public override string Id => "rain_pacu";
        public override string DisplayName => "Raining Pacu";
        protected override string PrefabId => PacuConfig.ID;
    }

    public sealed class RainBeeEvent : RainCritterEventBase
    {
        public override string Id => "rain_bee";
        public override int Danger => (int)EventDanger.High;
        public override string DisplayName => "Raining Beetas";
        protected override string PrefabId => BeeConfig.ID;
    }

    /// <summary>Rain gold ore chunks from the sky.</summary>
    public sealed class RainGoldEvent : GameEvent
    {
        public override string Id => "rain_gold";
        public override string DisplayName => "Raining gold";
        public override void Trigger()
        {
            Log.Info("Event: RainGold");
            RainController.RainOre(SimHashes.Gold, 50);
        }
    }

    /// <summary>Rain diamond chunks from the sky.</summary>
    public sealed class RainDiamondEvent : GameEvent
    {
        public override string Id => "rain_diamond";
        public override string DisplayName => "Raining diamonds";
        public override void Trigger()
        {
            Log.Info("Event: RainDiamond");
            RainController.RainOre(SimHashes.Diamond, 50);
        }
    }

    /// <summary>Rain a random material (metal ore / metal / gem) from the sky as pickupable chunks.</summary>
    public sealed class RainMaterialEvent : GameEvent
    {
        private static readonly SimHashes[] Pool =
        {
            SimHashes.Cuprite, SimHashes.FoolsGold, SimHashes.IronOre, SimHashes.Electrum,
            SimHashes.Cobaltite, SimHashes.GoldAmalgam, SimHashes.AluminumOre,
            SimHashes.Gold, SimHashes.Niobium, SimHashes.Tungsten, SimHashes.Diamond, SimHashes.Ceramic,
        };

        public override string Id => "rain_material";
        public override string DisplayName => "Raining materials (random)";

        public override void Trigger()
        {
            var mat = Pool[Random.Range(0, Pool.Length)];
            Log.Info("Event: RainMaterial " + mat);
            RainController.RainOre(mat, 40);
        }
    }

    /// <summary>Grant a free skill point to a third of the duplicants.</summary>
    public sealed class SkillPointsEvent : GameEvent
    {
        public override string Id => "skill_points";
        public override string DisplayName => "Free skill points";

        public override void Trigger()
        {
            Log.Info("Event: SkillPoints");
            var dupes = DupeUtil.Live().OrderBy(_ => Random.value).ToList();
            var count = Mathf.CeilToInt(dupes.Count * 0.33f);
            for (var i = 0; i < count && i < dupes.Count; i++)
            {
                if (dupes[i].TryGetComponent<MinionResume>(out var resume))
                {
                    resume.ForceAddSkillPoint();
                    UI.SpeechBubbles.ShowRaw(dupes[i].transform, "⭐");
                }
            }
        }
    }

    /// <summary>Spawn a vanity "snazzy suit" clothing item with a random facade near a dupe.</summary>
    public sealed class SnazzySuitEvent : GameEvent
    {
        public override string Id => "snazzy_suit";
        public override string DisplayName => "Snazzy suit drop";

        public override void Trigger()
        {
            Log.Info("Event: SnazzySuit");
            var cell = Cells.RandomDupeCell();
            if (!Grid.IsValidCell(cell))
            {
                return;
            }

            var prefab = Assets.GetPrefab((Tag) CustomClothingConfig.ID);
            if (prefab == null)
            {
                return;
            }

            var obj = Util.KInstantiate(prefab, Cells.PosOf(cell, Grid.SceneLayer.Front));
            obj.SetActive(true);
        }
    }

    /// <summary>Eclipse: block surface sunlight for three cycles.</summary>
    public sealed class EclipseEvent : GameEvent
    {
        public override string Id => "eclipse";
        public override int Danger => (int)EventDanger.Small;
        public override string DisplayName => "Solar eclipse (darkness)";

        public override void Trigger()
        {
            Log.Info("Event: Eclipse");
            EclipseController.Begin(3f * Constants.SECONDS_PER_CYCLE);
        }
    }

    /// <summary>Surprise: pick a random other registered event and trigger it.</summary>
    public sealed class SurpriseEvent : GameEvent
    {
        public override string Id => "surprise";
        public override string DisplayName => "Surprise! (random event)";

        public override void Trigger()
        {
            // Pick only from what chat would have been allowed to vote for anyway. Picking from
            // every registered event (which is what this used to do) would tunnel straight through
            // the danger cap: "harmless only" could still hand you a lava flood.
            var pick = EventRegistry.PickRandomAllowed(Id);
            if (pick == null)
            {
                Log.Info("Event: Surprise -> nothing is eligible right now.");
                return;
            }

            Log.Info($"Event: Surprise -> {pick.DisplayName}");
            try
            {
                pick.Trigger();
            }
            catch (System.Exception e)
            {
                Log.Warn($"Surprise inner event '{pick.DisplayName}' threw: {e.Message}");
            }
        }
    }
}
