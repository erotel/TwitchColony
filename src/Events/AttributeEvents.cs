namespace TwitchColony.Events
{
    // Attribute buffs/debuffs and drowsiness — apply our own effects (see ModEffects) to every dupe.
    // Independent reimplementation of the original mod's attribute events using vanilla Klei.AI effects.

    public sealed class AthleticsUpEvent : GameEvent
    {
        public override string Id => "attr_athletics_up";
        public override string GroupId => "attribute";
        public override string DisplayName => "Adrenaline rush (+athletics)";
        public override void Trigger() { Log.Info("Event: AthleticsUp"); ModEffects.ApplyToAll(ModEffects.AthleticsUp, "🏃"); }
    }

    public sealed class AthleticsDownEvent : GameEvent
    {
        public override string Id => "attr_athletics_down";
        public override string GroupId => "attribute";
        public override string DisplayName => "Sluggish (-athletics)";
        public override void Trigger() { Log.Info("Event: AthleticsDown"); ModEffects.ApplyToAll(ModEffects.AthleticsDown, "🐌"); }
    }

    public sealed class ConstructionUpEvent : GameEvent
    {
        public override string Id => "attr_construction_up";
        public override string GroupId => "attribute";
        public override string DisplayName => "In the zone (+construction)";
        public override void Trigger() { Log.Info("Event: ConstructionUp"); ModEffects.ApplyToAll(ModEffects.ConstructionUp, "🔨"); }
    }

    public sealed class ConstructionDownEvent : GameEvent
    {
        public override string Id => "attr_construction_down";
        public override string GroupId => "attribute";
        public override string DisplayName => "Butterfingers (-construction)";
        public override void Trigger() { Log.Info("Event: ConstructionDown"); ModEffects.ApplyToAll(ModEffects.ConstructionDown, "🤕"); }
    }

    public sealed class ExcavationUpEvent : GameEvent
    {
        public override string Id => "attr_excavation_up";
        public override string GroupId => "attribute";
        public override string DisplayName => "Digger's high (+excavation)";
        public override void Trigger() { Log.Info("Event: ExcavationUp"); ModEffects.ApplyToAll(ModEffects.ExcavationUp, "⛏️"); }
    }

    public sealed class ExcavationDownEvent : GameEvent
    {
        public override string Id => "attr_excavation_down";
        public override string GroupId => "attribute";
        public override string DisplayName => "Blunt tools (-excavation)";
        public override void Trigger() { Log.Info("Event: ExcavationDown"); ModEffects.ApplyToAll(ModEffects.ExcavationDown, "🪨"); }
    }

    public sealed class StrengthUpEvent : GameEvent
    {
        public override string Id => "attr_strength_up";
        public override string GroupId => "attribute";
        public override string DisplayName => "Pumped up (+strength)";
        public override void Trigger() { Log.Info("Event: StrengthUp"); ModEffects.ApplyToAll(ModEffects.StrengthUp, "💪"); }
    }

    public sealed class StrengthDownEvent : GameEvent
    {
        public override string Id => "attr_strength_down";
        public override string GroupId => "attribute";
        public override string DisplayName => "Weak knees (-strength)";
        public override void Trigger() { Log.Info("Event: StrengthDown"); ModEffects.ApplyToAll(ModEffects.StrengthDown, "🦵"); }
    }

    /// <summary>Sleepy dupes: everyone gets a rapid stamina drain. Negative event.</summary>
    public sealed class SleepyDupesEvent : GameEvent
    {
        public override string Id => "sleepy_dupes";
        public override string DisplayName => "Sleepy dupes (rapid tiredness)";
        public override void Trigger() { Log.Info("Event: SleepyDupes"); ModEffects.ApplyToAll(ModEffects.Sleepy, "💤"); }
    }

    /// <summary>Slow dupes: a big movement/work slowdown for 30 seconds. Negative event.</summary>
    public sealed class SlowDupesEvent : GameEvent
    {
        public override string Id => "slow_dupes";
        public override string DisplayName => "Slow dupes (30s sluggish)";
        public override void Trigger() { Log.Info("Event: SlowDupes"); ModEffects.ApplyToAll(ModEffects.SlowMo, "🐌"); }
    }

    /// <summary>Nap time: drain everyone's stamina to zero so they pass out and sleep on the spot.</summary>
    public sealed class SleepNowEvent : GameEvent
    {
        public override string Id => "sleep_now";
        public override string DisplayName => "Nap time (send dupes to sleep)";
        public override void Trigger()
        {
            Log.Info("Event: SleepNow");
            DupeUtil.ApplyAmountToAll(Db.Get().Amounts.Stamina, -1000f, "😴");
        }
    }
}
