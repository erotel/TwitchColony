namespace TwitchColony.Api
{
    /// <summary>
    ///     How often an event should turn up in the vote options, relative to the others. Crossing
    ///     the assembly boundary it travels as a plain int (see <see cref="ApiContract"/>).
    /// </summary>
    public enum EventWeight
    {
        /// <summary>Registered, but never offered by the random draw. For events you trigger yourself.</summary>
        Never = 0,

        /// <summary>Offered a quarter as often as <see cref="Common"/>.</summary>
        Rare = 1,

        /// <summary>Offered half as often as <see cref="Common"/>.</summary>
        Uncommon = 2,

        /// <summary>The default, and what every built-in event uses unless it says otherwise.</summary>
        Common = 4,

        /// <summary>Offered twice as often as <see cref="Common"/>. Use sparingly.</summary>
        VeryCommon = 8,
    }
}
