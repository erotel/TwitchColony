using System.Collections.Generic;
using TwitchColony.UI;
using UnityEngine;

namespace TwitchColony.Events
{
    // ---------------------------------------------------------------------------------------------
    // A few small, SAFE sample events so the vote loop does something visible out of the box.
    // They only log and show a floating bubble over a duplicant — no risky game mutation.
    // Replace the body of Trigger() with real gameplay (spawn items, apply effects, stress, etc.).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Cheer: every duplicant shows a celebratory bubble.</summary>
    public sealed class CheerEvent : GameEvent
    {
        public override string Id => "cheer";
        public override string DisplayName => "Cheer for the colony";

        public override void Trigger()
        {
            Log.Info("Event: Cheer");
            foreach (var identity in SafeMinions())
            {
                SpeechBubbles.ShowRaw(identity.transform, "Hura!");
            }
        }

        internal static IEnumerable<MinionIdentity> SafeMinions()
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
    }

    /// <summary>Roll call: a single random duplicant announces itself. Placeholder for real gameplay.</summary>
    public sealed class RollCallEvent : GameEvent
    {
        public override string Id => "rollcall";
        public override string DisplayName => "Random duplicant speaks up";

        public override void Trigger()
        {
            Log.Info("Event: RollCall");
            var list = new List<MinionIdentity>(CheerEvent.SafeMinions());
            if (list.Count == 0)
            {
                return;
            }

            var pick = list[Random.Range(0, list.Count)];
            SpeechBubbles.ShowRaw(pick.transform, "Tady jsem!");
            // TODO: real effect, e.g. give this dupe a temporary morale/athletics buff.
        }
    }

    /// <summary>No-op announcement, useful as a "nothing happens" filler option. Placeholder.</summary>
    public sealed class QuietEvent : GameEvent
    {
        public override string Id => "quiet";
        public override string DisplayName => "Calm shift (nothing happens)";

        public override void Trigger()
        {
            Log.Info("Event: Quiet (no-op)");
            // TODO: intentionally does nothing; keep or remove.
        }
    }
}
