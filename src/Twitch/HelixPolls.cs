using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TwitchColony.Twitch
{
    /// <summary>
    ///     Small blocking client for the Twitch Helix "polls" endpoints. Call from a background
    ///     thread. Works against the real API and the Twitch CLI mock (base URL ".../mock").
    ///     Independent implementation.
    /// </summary>
    public static class HelixPolls
    {
        public sealed class Choice
        {
            public string Id;
            public string Title;
            public int Votes;
        }

        public sealed class Poll
        {
            public string Id;
            public string Status;
            public readonly List<Choice> Choices = new List<Choice>();
        }

        public static Poll CreatePoll(string baseUrl, string clientId, string broadcasterId, string token,
            string title, IList<string> choices, int durationSeconds)
        {
            var body = new JObject
            {
                ["broadcaster_id"] = broadcasterId,
                ["title"] = Truncate(title, 60),
                ["duration"] = Clamp(durationSeconds, 15, 1800),
            };

            var arr = new JArray();
            foreach (var c in choices)
            {
                arr.Add(new JObject { ["title"] = Truncate(c, 25) });
            }

            body["choices"] = arr;

            var resp = Send("POST", Join(baseUrl, "/polls"), clientId, token, body.ToString());
            return resp != null ? ParseFirst(resp) : null;
        }

        public static Poll GetPoll(string baseUrl, string clientId, string broadcasterId, string token, string pollId)
        {
            var url = Join(baseUrl, $"/polls?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&id={Uri.EscapeDataString(pollId)}");
            var resp = Send("GET", url, clientId, token, null);
            return resp != null ? ParseFirst(resp) : null;
        }

        private static string Send(string method, string url, string clientId, string token, string jsonBody)
        {
            try
            {
                var req = WebRequest.CreateHttp(url);
                req.Method = method;
                req.ContentType = "application/json";
                req.Headers["Client-Id"] = clientId;
                req.Headers["Authorization"] = "Bearer " + token;

                if (jsonBody != null)
                {
                    var data = Encoding.UTF8.GetBytes(jsonBody);
                    using (var s = req.GetRequestStream())
                    {
                        s.Write(data, 0, data.Length);
                    }
                }

                using (var resp = (HttpWebResponse) req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream() ?? throw new IOException("no stream")))
                {
                    var text = r.ReadToEnd();
                    Log.Info($"[Polls] {method} {url} -> {(int) resp.StatusCode}");
                    return text;
                }
            }
            catch (WebException we)
            {
                if (we.Response is HttpWebResponse hr)
                {
                    string err;
                    using (var r = new StreamReader(hr.GetResponseStream() ?? Stream.Null))
                    {
                        err = r.ReadToEnd();
                    }

                    Log.Warn($"[Polls] {method} {url} -> {(int) hr.StatusCode}: {err}");
                }
                else
                {
                    Log.Warn($"[Polls] {method} {url} failed: {we.Message}");
                }

                return null;
            }
            catch (Exception e)
            {
                Log.Warn($"[Polls] {method} {url} failed: {e.Message}");
                return null;
            }
        }

        private static Poll ParseFirst(string json)
        {
            try
            {
                var data = JObject.Parse(json)["data"] as JArray;
                if (data == null || data.Count == 0)
                {
                    Log.Warn("[Polls] Response had no poll data.");
                    return null;
                }

                var first = data[0];
                var poll = new Poll { Id = (string) first["id"], Status = (string) first["status"] };
                if (first["choices"] is JArray choices)
                {
                    foreach (var c in choices)
                    {
                        poll.Choices.Add(new Choice
                        {
                            Id = (string) c["id"],
                            Title = (string) c["title"],
                            Votes = (int?) c["votes"] ?? 0,
                        });
                    }
                }

                return poll;
            }
            catch (Exception e)
            {
                Log.Warn("[Polls] Failed to parse: " + e.Message);
                return null;
            }
        }

        private static string Join(string baseUrl, string path) => baseUrl.TrimEnd('/') + path;

        private static string Truncate(string s, int max)
        {
            s = s ?? "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
