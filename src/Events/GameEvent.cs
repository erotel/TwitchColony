namespace TwitchColony.Events
{
    /// <summary>
    ///     A single thing chat can vote to make happen in the colony. Subclass and override
    ///     <see cref="Trigger"/> with real gameplay. Kept deliberately tiny so events are easy to add.
    /// </summary>
    public abstract class GameEvent
    {
        /// <summary>Stable id used internally (no spaces).</summary>
        public abstract string Id { get; }

        /// <summary>Human-readable name shown to chat / in the poll.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Runs on the main thread when this event wins a vote.</summary>
        public abstract void Trigger();
    }
}
