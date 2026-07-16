namespace TwitchColony.Twitch
{
    /// <summary>
    ///     A subscription-related Twitch event parsed from an IRC <c>USERNOTICE</c> line (sub / resub /
    ///     gifted sub). These arrive on the normal chat stream once the tags+commands caps are requested,
    ///     so no Helix API or special scopes are needed. Our own type.
    /// </summary>
    public sealed class SubNotice
    {
        public string User;             // Actor login (lowercase): the subscriber, or the gifter for a gift.
        public string Display;          // Display name if provided, else User.
        public string MsgId;            // "sub", "resub", "subgift", "anonsubgift", ...
        public int CumulativeMonths;    // Total months (resub); 0/1 for a brand-new sub.
        public string SubPlan;          // "Prime", "1000", "2000", "3000" (Tier 1/2/3).
        public string SystemMsg;        // Twitch's ready-made human-readable line.

        public bool IsResub => MsgId == "resub";
        public bool IsGift => MsgId == "subgift" || MsgId == "anonsubgift";

        /// <summary>Tier as a small number (1/2/3); Prime counts as Tier 1.</summary>
        public int Tier =>
            SubPlan == "3000" ? 3 : SubPlan == "2000" ? 2 : 1;
    }
}
