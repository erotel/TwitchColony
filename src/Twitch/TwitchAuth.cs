using System;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using TwitchColony.Config;

namespace TwitchColony.Twitch
{
    /// <summary>
    ///     Holds the chat token and, for the native poll API, the client_id and broadcaster (user) id.
    ///     In real mode these are read from Twitch's token-validation endpoint. In mock mode (Twitch CLI)
    ///     the config overrides are used and validation is skipped. Independent implementation.
    /// </summary>
    public static class TwitchAuth
    {
        public static string Token { get; private set; } = "";
        public static string ClientId { get; private set; } = "";
        public static string UserId { get; private set; } = "";

        /// <summary>Resolve auth from config. Safe to call on a background thread.</summary>
        public static void Resolve()
        {
            var cfg = ModConfig.Instance;
            Token = cfg.OauthToken ?? "";

            // Mock mode: overrides provided, skip validation.
            if (!string.IsNullOrEmpty(cfg.ClientIdOverride))
            {
                ClientId = cfg.ClientIdOverride;
                UserId = cfg.BroadcasterIdOverride;
                Log.Info("Auth: using config overrides (mock mode).");
                return;
            }

            if (string.IsNullOrEmpty(Token))
            {
                return; // anonymous; polls unavailable but chat/bubbles still work
            }

            try
            {
                var req = WebRequest.CreateHttp("https://id.twitch.tv/oauth2/validate");
                req.Headers["Authorization"] = "Bearer " + Token;
                using (var resp = req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream() ?? Stream.Null))
                {
                    var json = JObject.Parse(r.ReadToEnd());
                    ClientId = (string) json["client_id"] ?? "";
                    UserId = (string) json["user_id"] ?? "";
                    Log.Info("Auth: token validated.");
                }
            }
            catch (Exception e)
            {
                Log.Warn("Auth: token validation failed (chat still works, polls won't): " + e.Message);
            }
        }
    }
}
