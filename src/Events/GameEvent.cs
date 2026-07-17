using TwitchColony.Api;

namespace TwitchColony.Events
{
    /// <summary>
    ///     A single thing chat can vote to make happen in the colony. Subclass and override
    ///     <see cref="Trigger()"/> with real gameplay. Kept deliberately tiny so events are easy to add.
    ///
    ///     This is the mod's INTERNAL shape for an event. Other mods must not subclass it: a type
    ///     from this assembly can't be implemented from another one without a hard reference, which
    ///     would take the add-on down whenever Twitch Colony isn't installed. Add-ons go through
    ///     <see cref="Api.EventBridge"/> (or, more comfortably, the TwitchColony.Api merge-lib),
    ///     which lands here as an <see cref="ApiEvent"/>.
    /// </summary>
    public abstract class GameEvent
    {
        /// <summary>Stable id used internally (no spaces).</summary>
        public abstract string Id { get; }

        /// <summary>Human-readable name shown to chat / in the poll.</summary>
        public abstract string DisplayName { get; }

        /// <summary>
        ///     Events that feel like each other share a group id, e.g. all the floods. When one of
        ///     them fires, the whole group is pushed down the draw order for a while, so chat doesn't
        ///     get offered three floods in a row. null = the event stands on its own.
        /// </summary>
        public virtual string GroupId => null;

        /// <summary>How often it's offered, relative to other events. See <see cref="EventWeight"/>.</summary>
        public virtual int Weight => (int)EventWeight.Common;

        /// <summary>How much it can hurt. See <see cref="EventDanger"/>.</summary>
        public virtual int Danger => (int)EventDanger.None;

        /// <summary>
        ///     Checked when the vote options are drawn: return false and the event sits this one out.
        ///     Use it for events that only make sense sometimes. Keep it cheap — it runs over the
        ///     whole pool on every draw — and don't change the world from in here.
        /// </summary>
        public virtual bool CanRun(object context) => true;

        /// <summary>Runs on the main thread when this event wins a vote.</summary>
        public abstract void Trigger();

        /// <summary>
        ///     Same, but told what triggered it (see <see cref="EventContext"/>). Built-in events
        ///     that don't care about the payload just override <see cref="Trigger()"/>.
        /// </summary>
        public virtual void Trigger(object context) => Trigger();
    }
}
