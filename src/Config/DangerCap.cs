using PeterHan.PLib.Options;

namespace TwitchColony.Config
{
    /// <summary>
    ///     The worst thing chat is allowed to vote for. The values line up 1:1 with
    ///     <see cref="Api.EventDanger"/> — this is a separate type only so the settings screen can
    ///     show friendly labels: EventDanger lives in Shared/, which is compiled into the add-on
    ///     merge-lib too and must stay free of PLib (and of everything else that isn't BCL).
    /// </summary>
    public enum DangerCap
    {
        [Option("Harmless only", "Cosmetic events and pure upside. Nothing can hurt the colony.")]
        Harmless = 0,

        [Option("Up to annoying", "Adds events that waste time or interrupt work, but break nothing.")]
        Annoying = 1,

        [Option("Up to costly", "Adds events that cost real resources: floods, blackouts, dead crops.")]
        Costly = 2,

        [Option("Up to dangerous", "Adds events that wreck the colony if you don't react. No instant deaths.")]
        Dangerous = 3,

        [Option("Anything, including deadly", "Everything, up to and including events that kill duplicants.")]
        Deadly = 4,
    }
}
