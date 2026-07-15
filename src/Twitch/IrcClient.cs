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

        // Reconnect backoff: doubles per consecutive failure, capped. Reset once a connection stays up.
        private const int BaseBackoffMs = 1000;
        private const int MaxBackoffMs = 30000;
        private static readonly TimeSpan StableConnection = TimeSpan.FromSeconds(30);

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
            var attempt = 0;

            while (running)
            {
                var upSince = System.DateTime.UtcNow;
                var opened = false;

                try
                {
                    OpenConnection();
                    opened = true;
                    upSince = System.DateTime.UtcNow;
                    attempt = 0; // provisional reset; re-evaluated below if the connection dropped quickly
                    ReadLoop();
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
                    CloseSocket();
                }

                if (!running)
                {
                    break;
                }

                // Grow the backoff on rapid failures; keep it small if the connection had stayed up.
                var stayedUp = opened && System.DateTime.UtcNow - upSince > StableConnection;
                attempt = stayedUp ? 0 : attempt + 1;

                var shift = Math.Min(attempt, 5); // cap the exponent so the shift can't overflow
                var delayMs = Math.Min(BaseBackoffMs << shift, MaxBackoffMs);
                Log.Info($"IRC disconnected; reconnecting in {delayMs} ms (attempt {attempt}).");
                SleepInterruptible(delayMs);
            }

            Log.Info("IRC client stopped.");
        }

        /// <summary>Open the TLS socket and perform the Twitch login handshake. Throws on failure.</summary>
        private void OpenConnection()
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
        }

        /// <summary>Read lines until the connection closes (returns) or the client is stopped.</summary>
        private void ReadLoop()
        {
            while (running)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    return; // connection closed by the server
                }

                HandleLine(line);
            }
        }

        private void CloseSocket()
        {
            try { ssl?.Dispose(); } catch { /* ignore */ }
            try { tcp?.Close(); } catch { /* ignore */ }
            reader = null;
            writer = null;
            ssl = null;
            tcp = null;
        }

        /// <summary>Sleep in small slices so <see cref="Stop"/> interrupts the backoff promptly.</summary>
        private void SleepInterruptible(int ms)
        {
            var remaining = ms;
            while (running && remaining > 0)
            {
                var chunk = Math.Min(200, remaining);
                Thread.Sleep(chunk);
                remaining -= chunk;
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
