using System.Text;
using TMPro;

namespace TwitchColony.UI
{
    /// <summary>
    ///     Drops characters the bubble font can't draw.
    ///
    ///     The game's fonts have no emoji, and TextMeshPro renders a missing glyph as a hollow box.
    ///     That matters more here than in most mods: this one puts *Twitch chat* on screen, and
    ///     viewers type emoji constantly — without this, a cheering chat turns the colony into a
    ///     field of little squares. Dropping them is the honest option; we can't invent glyphs the
    ///     font doesn't have, and a box says nothing at all.
    /// </summary>
    internal static class Glyphs
    {
        /// <summary>
        ///     <paramref name="text"/> with every character the font can't render removed. ASCII is
        ///     always kept without asking — it always draws, and a font that answered "no" to plain
        ///     letters would leave us deleting the message itself.
        /// </summary>
        public static string KeepRenderable(string text, TMP_Text label)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var font = label != null ? label.font : null;
            if (font == null)
            {
                return text; // Nothing to ask; better to show a box than to swallow the message.
            }

            var kept = new StringBuilder(text.Length);
            var dropped = false;

            foreach (var c in text)
            {
                if (c < 128 || Renders(font, c))
                {
                    kept.Append(c);
                }
                else
                {
                    dropped = true;
                }
            }

            if (!dropped)
            {
                return text;
            }

            // Emoji are surrogate pairs, so dropping them can leave the spaces they sat between.
            return kept.ToString().Trim();
        }

        private static bool Renders(TMP_FontAsset font, char c)
        {
            try
            {
                // searchFallbacks: the game chains fonts for other alphabets, and a glyph found there
                // still draws. tryAddCharacter stays false — we're asking a question, not asking the
                // atlas to grow.
                return font.HasCharacter(c, true, false);
            }
            catch
            {
                return true; // If the font won't answer, leave the text alone.
            }
        }
    }
}
