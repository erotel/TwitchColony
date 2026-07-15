using System.Collections.Generic;
using System.Linq;
using Klei.AI;
using UnityEngine;

namespace TwitchColony.Events
{
    /// <summary>Grid / cell helpers used by the colony events. Everything here is vanilla game API.</summary>
    internal static class Cells
    {
        // Marker so our sim edits are identifiable in the game's cell event log.
        internal static readonly CellElementEvent SpawnEvent =
            new CellElementEvent("TwitchColonySpawn", "Spawned by Twitch Colony", true);

        /// <summary>Cell under a random live duplicant, or <see cref="Grid.InvalidCell"/> if there are none.</summary>
        public static int RandomDupeCell()
        {
            var dupes = DupeUtil.Live().ToList();
            if (dupes.Count == 0)
            {
                return Grid.InvalidCell;
            }

            return Grid.PosToCell(dupes[Random.Range(0, dupes.Count)].transform.position);
        }

        /// <summary>All valid cells in a square of the given radius around a centre cell.</summary>
        public static IEnumerable<int> Square(int center, int radius)
        {
            if (!Grid.IsValidCell(center))
            {
                yield break;
            }

            Grid.CellToXY(center, out var cx, out var cy);
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var c = Grid.XYToCell(cx + dx, cy + dy);
                    if (Grid.IsValidCell(c))
                    {
                        yield return c;
                    }
                }
            }
        }

        /// <summary>Replace the element at a cell (displacing existing mass). Vanilla SimMessages.</summary>
        public static void Place(int cell, SimHashes hash, float mass, float temp = -1f)
        {
            if (!Grid.IsValidCell(cell))
            {
                return;
            }

            var el = ElementLoader.FindElementByHash(hash);
            if (el == null)
            {
                return;
            }

            if (temp <= 0f)
            {
                temp = el.defaultValues.temperature;
            }

            SimMessages.ReplaceAndDisplaceElement(cell, hash, SpawnEvent, mass, temp);
        }

        /// <summary>Nudge a cell's temperature toward a target via an energy delta. Vanilla SimMessages.</summary>
        public static void NudgeTemp(int cell, float target, SimMessages.EnergySourceID src)
        {
            if (!Grid.IsValidCell(cell))
            {
                return;
            }

            var el = Grid.Element[cell];
            if (el == null)
            {
                return;
            }

            var dQ = Grid.Mass[cell] * el.specificHeatCapacity * (target - Grid.Temperature[cell]);
            SimMessages.ModifyEnergy(cell, dQ, 9999f, src);
        }

        /// <summary>World position at the centre of a cell on the given scene layer.</summary>
        public static Vector3 PosOf(int cell, Grid.SceneLayer layer) => Grid.CellToPosCCC(cell, layer);
    }

    /// <summary>
    ///     Our own duplicant effects (attribute buffs/debuffs, drowsiness), registered into the game's
    ///     effect DB the first time they're needed. Independent implementation — no assets/code reused
    ///     from the original mod; just vanilla <see cref="Klei.AI.Effect"/> / <see cref="AttributeModifier"/>.
    /// </summary>
    internal static class ModEffects
    {
        private static bool done;

        public static Effect AthleticsUp, AthleticsDown, ConstructionUp, ConstructionDown,
            ExcavationUp, ExcavationDown, StrengthUp, StrengthDown, Sleepy, Turbo, SlowMo, Zen;

        public static void EnsureRegistered()
        {
            if (done)
            {
                return;
            }

            done = true;
            try
            {
                var attr = Db.Get().Attributes;
                var amounts = Db.Get().Amounts;

                AthleticsUp = Attr("TC_AthleticsUp", "Adrenaline rush", attr.Athletics.Id, +5f, false);
                AthleticsDown = Attr("TC_AthleticsDown", "Sluggish", attr.Athletics.Id, -5f, true);
                ConstructionUp = Attr("TC_ConstructionUp", "In the zone", attr.Construction.Id, +5f, false);
                ConstructionDown = Attr("TC_ConstructionDown", "Butterfingers", attr.Construction.Id, -5f, true);
                ExcavationUp = Attr("TC_ExcavationUp", "Digger's high", attr.Digging.Id, +5f, false);
                ExcavationDown = Attr("TC_ExcavationDown", "Blunt tools", attr.Digging.Id, -5f, true);
                StrengthUp = Attr("TC_StrengthUp", "Pumped up", attr.Strength.Id, +5f, false);
                StrengthDown = Attr("TC_StrengthDown", "Weak knees", attr.Strength.Id, -5f, true);

                Sleepy = new Effect("TC_Sleepy", "Drowsy", "Suddenly overcome by sleepiness.",
                    0.5f * Constants.SECONDS_PER_CYCLE, true, true, true, custom_icon: "status_item_exhausted");
                Sleepy.Add(new AttributeModifier(amounts.Stamina.deltaAttribute.Id, -10f, "Drowsy"));

                // Turbo: a big all-round boost (movement via Athletics, plus work speed) for 30 seconds.
                Turbo = new Effect("TC_Turbo", "Turbocharged", "Supercharged movement and work.",
                    30f, true, true, false);
                Turbo.Add(new AttributeModifier(attr.Athletics.Id, 20f, "Turbocharged"));
                Turbo.Add(new AttributeModifier(attr.Construction.Id, 10f, "Turbocharged"));
                Turbo.Add(new AttributeModifier(attr.Digging.Id, 10f, "Turbocharged"));
                Turbo.Add(new AttributeModifier(attr.Strength.Id, 10f, "Turbocharged"));

                // Slow-mo: the opposite of Turbo — everything crawls for 30 seconds.
                SlowMo = new Effect("TC_SlowMo", "Sluggish", "Everything feels like molasses.",
                    30f, true, true, true);
                SlowMo.Add(new AttributeModifier(attr.Athletics.Id, -12f, "Sluggish"));
                SlowMo.Add(new AttributeModifier(attr.Construction.Id, -6f, "Sluggish"));
                SlowMo.Add(new AttributeModifier(attr.Digging.Id, -6f, "Sluggish"));
                SlowMo.Add(new AttributeModifier(attr.Strength.Id, -6f, "Sluggish"));

                // Zen: a temporary morale (Quality of Life) boost, paired with a stress wipe.
                Zen = new Effect("TC_Zen", "Blissful", "A wave of calm and contentment.",
                    1f * Constants.SECONDS_PER_CYCLE, true, true, false);
                Zen.Add(new AttributeModifier(attr.QualityOfLife.Id, 8f, "Blissful"));

                var db = Db.Get().effects;
                foreach (var e in new[]
                         {
                             AthleticsUp, AthleticsDown, ConstructionUp, ConstructionDown,
                             ExcavationUp, ExcavationDown, StrengthUp, StrengthDown, Sleepy, Turbo, SlowMo, Zen,
                         })
                {
                    if (db.TryGet(e.Id) == null)
                    {
                        db.Add(e);
                    }
                }

                Log.Info("Registered custom effects.");
            }
            catch (System.Exception ex)
            {
                Log.Warn("Failed to register custom effects: " + ex.Message);
            }
        }

        /// <summary>Add an effect to every live duplicant, optionally with a bubble.</summary>
        public static void ApplyToAll(Effect effect, string bubble = null)
        {
            if (effect == null)
            {
                return;
            }

            foreach (var id in DupeUtil.Live())
            {
                if (id.TryGetComponent<Effects>(out var fx))
                {
                    fx.Add(effect, true);
                }

                if (!string.IsNullOrEmpty(bubble))
                {
                    UI.SpeechBubbles.ShowRaw(id.transform, bubble);
                }
            }
        }

        private static Effect Attr(string id, string name, string attrId, float val, bool bad)
        {
            var e = new Effect(id, name, name, 2f * Constants.SECONDS_PER_CYCLE, true, true, bad);
            e.Add(new AttributeModifier(attrId, val, name));
            return e;
        }
    }

    /// <summary>Blocks surface sunlight for a while. Our own tiny timer around vanilla TimeOfDay.SetEclipse.</summary>
    internal sealed class EclipseController : MonoBehaviour
    {
        private float remaining;

        public static void Begin(float seconds)
        {
            if (Game.Instance == null)
            {
                return;
            }

            var go = Game.Instance.gameObject;
            var c = go.GetComponent<EclipseController>() ?? go.AddComponent<EclipseController>();
            c.remaining = Mathf.Max(c.remaining, seconds);
            if (TimeOfDay.Instance != null)
            {
                TimeOfDay.Instance.SetEclipse(true);
            }
        }

        private void Update()
        {
            remaining -= Time.unscaledDeltaTime;
            if (remaining > 0f)
            {
                return;
            }

            if (TimeOfDay.Instance != null)
            {
                TimeOfDay.Instance.SetEclipse(false);
            }

            Destroy(this);
        }
    }

    /// <summary>Drops a number of prefabs (or ore chunks) from above random dupes over time. Our own spawner.</summary>
    internal sealed class RainController : MonoBehaviour
    {
        private Tag prefab;
        private SimHashes ore;
        private bool isOre;
        private float oreMass;
        private int remaining;
        private float interval;
        private float nextAt;

        public static void RainPrefab(Tag prefab, int count, float interval = 0.1f)
        {
            var c = Attach();
            if (c == null)
            {
                return;
            }

            c.prefab = prefab;
            c.remaining = count;
            c.interval = interval;
        }

        public static void RainOre(SimHashes element, int count, float massEach = 10000f, float interval = 0.1f)
        {
            var c = Attach();
            if (c == null)
            {
                return;
            }

            c.isOre = true;
            c.ore = element;
            c.oreMass = massEach;
            c.remaining = count;
            c.interval = interval;
        }

        private static RainController Attach()
        {
            return Game.Instance != null ? Game.Instance.gameObject.AddComponent<RainController>() : null;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextAt)
            {
                return;
            }

            nextAt = Time.unscaledTime + interval;
            try
            {
                SpawnOne();
            }
            catch (System.Exception e)
            {
                Log.Warn("Rain spawn failed: " + e.Message);
                remaining = 0;
            }

            remaining--;
            if (remaining <= 0)
            {
                Destroy(this);
            }
        }

        private void SpawnOne()
        {
            var baseCell = Cells.RandomDupeCell();
            if (!Grid.IsValidCell(baseCell))
            {
                return;
            }

            // Spawn some cells above the dupe so it falls into view.
            Grid.CellToXY(baseCell, out var x, out var y);
            var cell = Grid.XYToCell(x, y + 12);
            if (!Grid.IsValidCell(cell) || Grid.IsSolidCell(cell))
            {
                cell = baseCell;
            }

            if (isOre)
            {
                var el = ElementLoader.FindElementByHash(ore);
                el?.substance?.SpawnResource(Cells.PosOf(cell, Grid.SceneLayer.Ore), oreMass,
                    el.defaultValues.temperature, byte.MaxValue, 0);
                return;
            }

            var prefabGo = Assets.GetPrefab(prefab);
            if (prefabGo == null)
            {
                return;
            }

            var obj = Util.KInstantiate(prefabGo, Cells.PosOf(cell, Grid.SceneLayer.Creatures));
            obj.SetActive(true);
        }
    }
}
