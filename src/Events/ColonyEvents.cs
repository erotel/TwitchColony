using System.Collections.Generic;
using TwitchColony.Api;
using System.Linq;
using HarmonyLib;
using Klei.AI;
using TwitchColony.UI;
using UnityEngine;

namespace TwitchColony.Events
{
    // ---------------------------------------------------------------------------------------------
    // Real colony events driven by chat votes. These use only vanilla game API (Db amounts, Health,
    // Research) — no assets or code from the original mod (see CREDITS.md). Each Trigger() runs on
    // the main thread. Effects are deliberately reversible-ish / bounded so a stream stays playable.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Shared helpers for the colony events.</summary>
    internal static class DupeUtil
    {
        /// <summary>All live duplicant identities (never null).</summary>
        public static IEnumerable<MinionIdentity> Live()
        {
            var items = Components.LiveMinionIdentities?.Items;
            if (items == null)
            {
                yield break;
            }

            foreach (var m in items)
            {
                if (m != null)
                {
                    yield return m;
                }
            }
        }

        public static int LiveCount()
        {
            var items = Components.LiveMinionIdentities?.Items;
            return items?.Count ?? 0;
        }

        /// <summary>Apply a delta to a Klei.AI amount (stress, calories, …) on every live duplicant.</summary>
        public static void ApplyAmountToAll(Amount amount, float delta, string bubble = null)
        {
            foreach (var id in Live())
            {
                var inst = amount.Lookup(id.gameObject);
                if (inst != null)
                {
                    inst.ApplyDelta(delta);
                }

                if (!string.IsNullOrEmpty(bubble))
                {
                    SpeechBubbles.ShowRaw(id.transform, bubble);
                }
            }
        }
    }

    /// <summary>Stress spike: every duplicant gains stress. Negative event.</summary>
    public sealed class StressSpikeEvent : GameEvent
    {
        public override string Id => "stress_spike";
        public override int Danger => (int)EventDanger.Small;
        public override string DisplayName => "Stress spike (+stress)";

        public override void Trigger()
        {
            Log.Info("Event: StressSpike");
            DupeUtil.ApplyAmountToAll(Db.Get().Amounts.Stress, +25f, "😱");
        }
    }

    /// <summary>Group therapy: every duplicant loses stress. Positive event.</summary>
    public sealed class StressReliefEvent : GameEvent
    {
        public override string Id => "stress_relief";
        public override string DisplayName => "Group therapy (-stress)";

        public override void Trigger()
        {
            Log.Info("Event: StressRelief");
            DupeUtil.ApplyAmountToAll(Db.Get().Amounts.Stress, -50f, "😌");
        }
    }

    /// <summary>Feast: top up everyone's calories. Positive event.</summary>
    public sealed class FeastEvent : GameEvent
    {
        public override string Id => "feast";
        public override string DisplayName => "Surprise feast (feed everyone)";

        public override void Trigger()
        {
            Log.Info("Event: Feast");
            DupeUtil.ApplyAmountToAll(Db.Get().Amounts.Calories, +4000f, "🍖");
        }
    }

    /// <summary>Exhaustion: drain everyone's stamina. Negative event.</summary>
    public sealed class ExhaustionEvent : GameEvent
    {
        public override string Id => "exhaustion";
        public override int Danger => (int)EventDanger.Small;
        public override string DisplayName => "Sudden exhaustion (-stamina)";

        public override void Trigger()
        {
            Log.Info("Event: Exhaustion");
            DupeUtil.ApplyAmountToAll(Db.Get().Amounts.Stamina, -50f, "😴");
        }
    }

    /// <summary>Gotta go: fill everyone's bladder. Mild/funny event.</summary>
    public sealed class FullBladderEvent : GameEvent
    {
        public override string Id => "full_bladder";
        public override int Danger => (int)EventDanger.Small;
        public override string DisplayName => "Gotta go! (full bladder)";

        public override void Trigger()
        {
            Log.Info("Event: FullBladder");
            DupeUtil.ApplyAmountToAll(Db.Get().Amounts.Bladder, +100f, "🚽");
        }
    }

    /// <summary>Eureka: instantly complete a random available research. Positive event.</summary>
    public sealed class ResearchBoostEvent : GameEvent
    {
        public override string Id => "research_boost";
        public override string DisplayName => "Eureka! (free research)";

        public override void Trigger()
        {
            Log.Info("Event: ResearchBoost");

            if (Research.Instance == null)
            {
                Log.Warn("ResearchBoost: no Research instance.");
                return;
            }

            // Pick from the lowest tier of techs whose prerequisites are done and that aren't complete yet.
            var available = Db.Get().Techs.resources
                .Where(t => !t.IsComplete() && t.ArePrerequisitesComplete())
                .ToList();
            if (available.Count == 0)
            {
                Log.Info("ResearchBoost: nothing left to research.");
                return;
            }

            var minTier = available.Min(t => t.tier);
            var pool = available.Where(t => t.tier == minTier).ToList();
            var tech = pool[Random.Range(0, pool.Count)];

            Research.Instance.GetOrAdd(tech).Purchased();
            Game.Instance.Trigger((int) GameHashes.ResearchComplete, tech);
            Log.Info($"ResearchBoost: completed tech '{tech.Id}'.");
        }
    }

    /// <summary>Grim reaper: kill a random duplicant. Dangerous — needs at least two dupes so a colony survives.</summary>
    public sealed class KillDupeEvent : GameEvent
    {
        // Health.Kill is not public; bind it once via Harmony's reflection helpers.
        private static readonly System.Action<Health> KillHealth =
            AccessTools.MethodDelegate<System.Action<Health>>(AccessTools.DeclaredMethod(typeof(Health), "Kill"));

        public override string Id => "kill_dupe";
        public override int Danger => (int)EventDanger.Deadly;
        public override string DisplayName => "Grim reaper (kill a dupe)";

        public override void Trigger()
        {
            Log.Info("Event: KillDupe");

            var dupes = DupeUtil.Live().ToList();
            if (dupes.Count < 2)
            {
                Log.Warn("KillDupe: need at least 2 dupes, skipping.");
                return;
            }

            var victim = dupes[Random.Range(0, dupes.Count)];
            if (victim.TryGetComponent<Health>(out var health) && KillHealth != null)
            {
                SpeechBubbles.ShowRaw(victim.transform, "💀");
                KillHealth(health);
                Log.Info($"KillDupe: killed '{victim.GetProperName()}'.");
            }
            else
            {
                Log.Warn("KillDupe: victim had no Health / Kill unavailable.");
            }
        }
    }
}
