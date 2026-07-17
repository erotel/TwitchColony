namespace TwitchColony.Api
{
    /// <summary>
    ///     How much an event can hurt the colony. Crossing the assembly boundary it travels as a
    ///     plain int (see <see cref="ApiContract"/>).
    ///
    ///     The streamer decides how rough their run gets, so tag honestly: an event tagged
    ///     <see cref="None"/> that can kill duplicants will ruin somebody's colony on a setting
    ///     they picked precisely to stop that happening.
    /// </summary>
    public enum EventDanger
    {
        /// <summary>Cosmetic or pure upside. Nothing can go wrong: bubbles, cheering, free stuff.</summary>
        None = 0,

        /// <summary>Mildly annoying. Some work is lost or interrupted; nothing breaks.</summary>
        Small = 1,

        /// <summary>Costs real resources or time, and a fragile colony could feel it.</summary>
        Medium = 2,

        /// <summary>Can damage the colony badly if the player doesn't react.</summary>
        High = 3,

        /// <summary>Duplicants can die.</summary>
        Deadly = 4,
    }
}
