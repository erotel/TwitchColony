using System;
using TwitchColony.Api;

namespace TwitchColony.Events
{
    /// <summary>
    ///     An event registered from another mod through <see cref="Api.EventBridge"/>. It holds
    ///     nothing but delegates and BCL values, which is the whole point: everything an add-on
    ///     hands us is a type both assemblies already agree on.
    /// </summary>
    internal sealed class ApiEvent : GameEvent
    {
        private readonly Action<object> action;
        private readonly Func<object, bool> condition;

        public ApiEvent(string id, string displayName, string groupId, int weight, int danger,
            Action<object> action, Func<object, bool> condition)
        {
            Id = id;
            DisplayName = displayName;
            GroupId = string.IsNullOrEmpty(groupId) ? null : groupId;
            Weight = weight;
            Danger = danger;
            this.action = action;
            this.condition = condition;
        }

        public override string Id { get; }
        public override string DisplayName { get; }
        public override string GroupId { get; }
        public override int Weight { get; }
        public override int Danger { get; }

        /// <summary>Which mod registered it, for log lines that name a culprit.</summary>
        public string Owner { get; set; } = "another mod";

        public override bool CanRun(object context)
        {
            if (condition == null) return true;

            try
            {
                return condition(context);
            }
            catch (Exception e)
            {
                // A broken condition must not take the draw down with it, and an event that can't
                // say whether it may run doesn't run.
                Log.Warn($"Condition of event '{Id}' (from {Owner}) threw, skipping it: {e.Message}");
                return false;
            }
        }

        public override void Trigger() => Trigger(null);

        public override void Trigger(object context) => action(context);
    }
}
