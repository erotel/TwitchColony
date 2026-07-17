using System.Collections.Generic;

namespace TwitchColony.Api
{
    /// <summary>
    ///     A snapshot of a registered event's state, handed back by
    ///     <see cref="TwitchColonyApi.TryGetEventData"/>. Everything you need to roll your own weighted
    ///     choice over a set of events and then trigger the winner.
    ///
    ///     This type lives only in the merge-lib — it never crosses into Twitch Colony. The mod hands
    ///     the values over as a plain dictionary and this side reads them into the typed shape below,
    ///     which is why an add-on's own struct is fine here where it wouldn't be on the boundary.
    /// </summary>
    public sealed class EventDataInfo
    {
        internal EventDataInfo(IDictionary<string, object> data)
        {
            Id = EventContext.GetString(data, EventData.Id);
            DisplayName = EventContext.GetString(data, EventData.DisplayName);
            GroupId = ReadString(data, EventData.GroupId); // null stays null, unlike GetString's ""
            Weight = EventContext.GetInt(data, EventData.Weight);
            Danger = ToDanger(EventContext.GetInt(data, EventData.Danger));
            ConditionMet = ReadBool(data, EventData.ConditionMet);
            WithinDangerCap = ReadBool(data, EventData.WithinDangerCap);
            Eligible = ReadBool(data, EventData.Eligible);
        }

        /// <summary>The event's stable id.</summary>
        public string Id { get; }

        /// <summary>What chat sees in the vote options.</summary>
        public string DisplayName { get; }

        /// <summary>The event's group id, or null if it stands on its own.</summary>
        public string GroupId { get; }

        /// <summary>
        ///     The event's configured draw weight (see <see cref="EventWeight"/>, though a mod may use
        ///     any value). 0 means it's never offered by a vote. This is the stable weight, not the
        ///     one temporarily damped by a recent group — what you want for your own weighted roll.
        /// </summary>
        public int Weight { get; }

        /// <summary>How much the event can hurt the colony.</summary>
        public EventDanger Danger { get; }

        /// <summary>
        ///     Whether the event's condition passes right now. False means it would sit a vote out
        ///     this moment — the "is it allowed" check you'd run before including it in a roll.
        /// </summary>
        public bool ConditionMet { get; }

        /// <summary>
        ///     Whether the event's danger is within the streamer's cap for the current cycle. Note
        ///     <see cref="TwitchColonyApi.TriggerEvent"/> ignores this cap, so respect it yourself if
        ///     you don't want to fire past what the streamer allowed.
        /// </summary>
        public bool WithinDangerCap { get; }

        /// <summary>
        ///     Whether the event would actually be offered in a vote drawn right now: weight above
        ///     zero, condition met, and within the danger cap. The single flag most callers want.
        /// </summary>
        public bool Eligible { get; }

        private static string ReadString(IDictionary<string, object> data, string key)
        {
            return data != null && data.TryGetValue(key, out var v) ? v as string : null;
        }

        private static bool ReadBool(IDictionary<string, object> data, string key)
        {
            return data != null && data.TryGetValue(key, out var v) && v is bool b && b;
        }

        private static EventDanger ToDanger(int value)
        {
            if (value < (int)EventDanger.None) return EventDanger.None;
            if (value > (int)EventDanger.Deadly) return EventDanger.Deadly;
            return (EventDanger)value;
        }
    }
}
