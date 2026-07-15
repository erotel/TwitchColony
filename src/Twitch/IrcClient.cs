using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TwitchColony.Twitch
{
    /// <summary>
    ///     Minimal Twitch IRC client over a raw TLS socket (irc.chat.twitch.tv:6697).
    ///     Raw TCP+SslStream is used deliberately: it is the most reliable transport under the
    ///     game's Mono runtime (WebSocket support there is flaky). Runs its own background thread
    ///     and raises <see cref="OnMessage"/> for every PRIVMSG. This is our own implementation.
    /// </summary>
    public sealed class IrcClient
    {
        private const string Host = "irc.chat.twitch.tv";
        private const int Port = 6697;

        private readonly string channel;
        private readonly string nick;
        private readonly string oauth;

        private TcpClient tcp;
        private SslStream ssl;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread thread;
        private volatile bool running;

        /// <summary>Raised on the background thread for each chat message. Marshal to the main thread yourself.</summary>
        public event Action<ChatMessage> OnMessage;

        public IrcClient(string channel, string nick, string oauth)
        {
            this.channel = (channel ?? "").Trim().ToLowerInvariant();
            this.nick = string.IsNullOrEmpty(nick) ? "justinfan" + new System.Random().Next(10000, 99999) : nick.Trim();
            this.oauth = oauth ?? "";
        }

        public void Start()
        {
            if (running)
            {
                return;
            }

            if (string.IsNullOrEmpty(channel))
            {
                Log.Warn("No channel configured; IRC client not started.");
                return;
            }

            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "TwitchColony-IRC" };
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            try { writer?.Flush(); } catch { /* ignore */ }
            try { ssl?.Dispose(); } catch { /* ignore */ }
            try { tcp?.Close(); } catch { /* ignore */ }
        }

        private void Run()
        {
            try
            {
                tcp = new TcpClient(Host, Port);
                ssl = new SslStream(tcp.GetStream(), false);
                ssl.AuthenticateAsClient(Host);
                reader = new StreamReader(ssl, Encoding.UTF8);
                writer = new StreamWriter(ssl, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };

                // Request tags so we get display-name; PASS is required even anonymous (any value for justinfan).
                writer.WriteLine("CAP REQ :twitch.tv/tags twitch.tv/commands");
                writer.WriteLine("PASS oauth:" + (string.IsNullOrEmpty(oauth) ? "SCHMOOPIIE" : oauth));
                writer.WriteLine("NICK " + nick);
                writer.WriteLine("JOIN #" + channel);
                Log.Info($"IRC connected as {nick}, joined #{channel}.");

                while (running)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break; // connection closed
                    }

                    HandleLine(line);
                }
            }
            catch (Exception e)
            {
                if (running)
                {
                    Log.Warn("IRC connection error: " + e.Message);
                }
            }
            finally
            {
                Log.Info("IRC client stopped.");
            }
        }

        private void HandleLine(string line)
        {
            // Keepalive.
            if (line.StartsWith("PING", StringComparison.Ordinal))
            {
                try { writer.WriteLine("PONG " + line.Substring(4)); } catch { /* ignore */ }
                return;
            }

            var parsed = IrcParser.ParsePrivmsg(line);
            if (parsed != null)
            {
                try { OnMessage?.Invoke(parsed); } catch (Exception e) { Log.Warn("OnMessage handler threw: " + e.Message); }
            }
        }
    }
}
