using System;
using System.Collections.Generic;

namespace TwitchColony.Twitch
{
    /// <summary>
    ///     Parses Twitch IRC lines into <see cref="ChatMessage"/>. Handles the optional IRCv3 tag
    ///     prefix ("@key=val;..."), the source prefix (":nick!user@host"), and PRIVMSG payloads.
    ///     Our own parser — intentionally small, covers what the mod needs.
    /// </summary>
    internal static class IrcParser
    {
        public static ChatMessage ParsePrivmsg(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            var rest = line;
            var tags = new Dictionary<string, string>();

            // Optional tags section.
            if (rest[0] == '@')
            {
                var sp = rest.IndexOf(' ');
                if (sp < 0)
                {
                    return null;
                }

                ParseTags(rest.Substring(1, sp - 1), tags);
                rest = rest.Substring(sp + 1);
            }

            // Source prefix.
            string sourceNick = null;
            if (rest.Length > 0 && rest[0] == ':')
            {
                var sp = rest.IndexOf(' ');
                if (sp < 0)
                {
                    return null;
                }

                var source = rest.Substring(1, sp - 1);
                var bang = source.IndexOf('!');
                sourceNick = bang > 0 ? source.Substring(0, bang) : source;
                rest = rest.Substring(sp + 1);
            }

            // Command + params. We only care about PRIVMSG.
            var cmdEnd = rest.IndexOf(' ');
            if (cmdEnd < 0)
            {
                return null;
            }

            var command = rest.Substring(0, cmdEnd);
            if (!string.Equals(command, "PRIVMSG", StringComparison.Ordinal))
            {
                return null;
            }

            // The message text follows the first " :".
            var colon = rest.IndexOf(" :", StringComparison.Ordinal);
            if (colon < 0)
            {
                return null;
            }

            var text = rest.Substring(colon + 2);
            var user = tags.TryGetValue("login", out var login) && !string.IsNullOrEmpty(login) ? login : sourceNick;
            tags.TryGetValue("display-name", out var display);

            if (string.IsNullOrEmpty(user))
            {
                return null;
            }

            return new ChatMessage(user.ToLowerInvariant(), display, text.TrimEnd('\r', '\n'));
        }

        private static void ParseTags(string tagBlock, IDictionary<string, string> into)
        {
            foreach (var pair in tagBlock.Split(';'))
            {
                var eq = pair.IndexOf('=');
                if (eq < 0)
                {
                    into[pair] = "";
                }
                else
                {
                    into[pair.Substring(0, eq)] = pair.Substring(eq + 1);
                }
            }
        }
    }
}
