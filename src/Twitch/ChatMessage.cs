namespace TwitchColony.Twitch
{
    /// <summary>A single chat message parsed from the Twitch IRC stream.</summary>
    public sealed class ChatMessage
    {
        public string User;      // Sender login (lowercase).
        public string Display;   // Display name if provided in tags, else User.
        public string Text;      // Message body.

        public ChatMessage(string user, string display, string text)
        {
            User = user;
            Display = string.IsNullOrEmpty(display) ? user : display;
            Text = text;
        }
    }
}
