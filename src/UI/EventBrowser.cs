using System.Collections.Generic;
using PeterHan.PLib.UI;
using TwitchColony.Config;
using TwitchColony.Events;
using UnityEngine;

namespace TwitchColony.UI
{
    /// <summary>
    ///     A window listing every registered event — its group, weight, danger and whether chat could
    ///     be offered it right now — with a button to fire each one.
    ///
    ///     Suggested by Sgt_Imalas, who pointed out that the original Twitch mod did this through the
    ///     game's dev tools, and that mods shipping against those crash on Mac, where they aren't
    ///     built. So this is an ordinary window built with PLib's UI, which we already merge. No dev
    ///     tools involved.
    ///
    ///     It is off by default and gated behind a setting: firing "kill a dupe" is one misclick
    ///     away, which is fine while you're building an event and not fine mid-stream.
    /// </summary>
    internal static class EventBrowser
    {
        private const float RowHeight = 28f;

        /// <summary>Build and show the window. Main thread, colony loaded.</summary>
        public static void Show()
        {
            var events = new List<GameEvent>(EventRegistry.AllEvents);
            // Harmless first, deadly last; alphabetical by id within the same danger tier. Flip the
            // CompareTo operands (a vs b) to list most-dangerous-first instead.
            events.Sort((a, b) =>
            {
                var byDanger = a.Danger.CompareTo(b.Danger);
                if (byDanger != 0)
                {
                    return byDanger;
                }

                return string.Compare(a.Id, b.Id, System.StringComparison.OrdinalIgnoreCase);
            });

            var dialog = new PDialog("TwitchColonyEventBrowser")
            {
                Title = "Twitch Colony — events (" + events.Count + ")",
                Size = new Vector2(760f, 560f),
                MaxSize = new Vector2(900f, 700f),
                SortKey = 200f,
            };
            dialog.AddButton("close", "Close", null);

            dialog.Body.Direction = PanelDirection.Vertical;
            dialog.Body.Alignment = TextAnchor.UpperLeft;
            dialog.Body.FlexSize = Vector2.one;
            dialog.Body.AddChild(Header());
            dialog.Body.AddChild(new PScrollPane("Events")
            {
                Child = BuildList(events),
                ScrollVertical = true,
                ScrollHorizontal = false,
                AlwaysShowVertical = true,
                FlexSize = Vector2.one,
            });

            // PDialog.Show() is just Build() + KScreen.Activate(); do it by hand so we can slip the
            // drag handler in between. Otherwise the window is nailed to the middle of the screen,
            // on top of the colony you opened it to watch.
            var window = dialog.Build();
            if (window == null)
            {
                Log.Warn("Event browser: the window failed to build.");
                return;
            }

            window.AddComponent<WindowDrag>();
            if (window.TryGetComponent<KScreen>(out var screen))
            {
                screen.Activate();
            }
            else
            {
                // Shouldn't happen, but a window nobody can close is worse than no window.
                Log.Warn("Event browser: no KScreen on the dialog; showing it as a plain object.");
                window.SetActive(true);
            }
        }

        private static IUIComponent Header()
        {
            return new PLabel("Hint")
            {
                Text = "Triggering from here skips the vote, your danger limit and the event's own " +
                       "conditions. It's a testing tool.",
                TextStyle = PUITuning.Fonts.TextLightStyle,
                TextAlignment = TextAnchor.MiddleLeft,
                Margin = new RectOffset(6, 6, 4, 6),
            };
        }

        /// <summary>One row per event: description on the left, a trigger button on the right.</summary>
        private static IUIComponent BuildList(List<GameEvent> events)
        {
            var grid = new PGridPanel("EventList") { Margin = new RectOffset(6, 6, 0, 0) };
            grid.AddColumn(new GridColumnSpec(0f, 1f)); // description takes the slack
            grid.AddColumn(new GridColumnSpec(90f));    // button, fixed so they line up

            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                grid.AddRow(new GridRowSpec(RowHeight));

                grid.AddChild(new PLabel("Info" + i)
                {
                    Text = Describe(ev),
                    TextStyle = PUITuning.Fonts.TextLightStyle,
                    TextAlignment = TextAnchor.MiddleLeft,
                    ToolTip = ev.Id,
                }, new GridComponentSpec(i, 0) { Alignment = TextAnchor.MiddleLeft });

                grid.AddChild(new PButton("Fire" + i)
                {
                    Text = "Trigger",
                    // Captured per row, so each button knows its own event.
                    OnClick = _ => Trigger(ev),
                    Margin = new RectOffset(2, 2, 2, 2),
                }.SetKleiPinkStyle(), new GridComponentSpec(i, 1));
            }

            return grid;
        }

        /// <summary>"Flash flood (LAVA!) — flood · deadly · common · blocked: danger limit"</summary>
        private static string Describe(GameEvent ev)
        {
            var parts = new List<string> { DangerName(ev.Danger), WeightName(ev.Weight) };
            if (!string.IsNullOrEmpty(ev.GroupId))
            {
                parts.Insert(0, ev.GroupId);
            }

            parts.Add(Eligibility(ev));
            return ev.DisplayName + "  <color=#8A8A8A>— " + string.Join(" · ", parts.ToArray()) + "</color>";
        }

        /// <summary>
        ///     Why chat can or can't be offered this event right now — the "is it valid" column.
        ///     Answers the question you actually have when an event never seems to come up.
        /// </summary>
        private static string Eligibility(GameEvent ev)
        {
            if (ev.Weight <= 0)
            {
                return "never drawn";
            }

            if (ev.Danger > EventRegistry.AllowedDanger(CurrentCycle()))
            {
                return "<color=#E5A0A0>blocked: danger limit</color>";
            }

            if (!CanRun(ev))
            {
                return "<color=#E5A0A0>blocked: its own condition</color>";
            }

            return "<color=#A0E5A0>can come up</color>";
        }

        private static bool CanRun(GameEvent ev)
        {
            try
            {
                return ev.CanRun(new Dictionary<string, object>
                {
                    { Api.EventContext.Cycle, CurrentCycle() },
                });
            }
            catch
            {
                return false;
            }
        }

        private static void Trigger(GameEvent ev)
        {
            Log.Info($"Event browser: triggering '{ev.Id}'.");
            Api.EventBridge.TriggerEvent(ev.Id);
        }

        private static int CurrentCycle()
        {
            try
            {
                var clock = GameClock.Instance;
                return clock != null ? clock.GetCycle() : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static string DangerName(int danger)
        {
            switch (danger)
            {
                case (int)Api.EventDanger.None: return "harmless";
                case (int)Api.EventDanger.Small: return "annoying";
                case (int)Api.EventDanger.Medium: return "costly";
                case (int)Api.EventDanger.High: return "dangerous";
                default: return "<color=#FF8080>deadly</color>";
            }
        }

        private static string WeightName(int weight)
        {
            if (weight <= 0) return "weight 0";
            if (weight <= (int)Api.EventWeight.Rare) return "rare";
            if (weight <= (int)Api.EventWeight.Uncommon) return "uncommon";
            if (weight <= (int)Api.EventWeight.Common) return "common";
            return "very common";
        }
    }
}
