namespace TwitchColony.Api
{
    /// <summary>
    ///     The keys of the dictionary <c>TryGetEventData</c> hands back — the mod fills it, the
    ///     merge-lib reads it into a typed <c>EventDataInfo</c>. Declared once here and compiled into
    ///     both assemblies (like <see cref="EventContext"/>) so the two can't disagree on a key name.
    ///
    ///     It travels as a plain <c>Dictionary&lt;string, object&gt;</c> for the usual reason: a type
    ///     of ours would be two unrelated types across the boundary, and a new key can be added later
    ///     without breaking an add-on built against an older version.
    /// </summary>
    public static class EventData
    {
        /// <summary>The event's stable id. string.</summary>
        public const string Id = "id";

        /// <summary>What chat sees in the vote options. string.</summary>
        public const string DisplayName = "displayName";

        /// <summary>The event's group id, or null if it stands on its own. string.</summary>
        public const string GroupId = "groupId";

        /// <summary>Its configured draw weight (see EventWeight); 0 = never offered by a vote. int.</summary>
        public const string Weight = "weight";

        /// <summary>How much it can hurt the colony (see EventDanger). int.</summary>
        public const string Danger = "danger";

        /// <summary>Whether its condition passes right now — false = it'd sit this vote out. bool.</summary>
        public const string ConditionMet = "conditionMet";

        /// <summary>Whether its danger is within the streamer's current (cycle-scaled) cap. bool.</summary>
        public const string WithinDangerCap = "withinDangerCap";

        /// <summary>
        ///     Whether it would actually be offered in a vote drawn right now: weight above zero, its
        ///     condition met, and within the danger cap. The one flag most callers want. bool.
        /// </summary>
        public const string Eligible = "eligible";
    }
}
