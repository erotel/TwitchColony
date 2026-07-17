# Adding your own events to Twitch Colony

Your mod can put its own events into Twitch Colony's vote pool. Chat then votes on them next to
the built-in ones, and they show up in the HUD and the chat announcements automatically.

**Your mod does not gain a dependency.** The API library talks to Twitch Colony through
reflection, so if Twitch Colony isn't installed, `RegisterEvent` returns `false` and your mod
carries on as if nothing happened — no crash, no missing assembly, nothing to catch.

- [Quick start](#quick-start)
- [The API](#the-api)
- [What your action receives](#what-your-action-receives)
- [Groups, weight, danger, conditions](#groups-weight-danger-conditions)
- [Why the API looks like this](#why-the-api-looks-like-this)
- [Versioning](#versioning)

---

## Quick start

1. Grab **`TwitchColony.Api.dll`** from the [releases](https://github.com/erotel/TwitchColony/releases)
   (or build it yourself: `./build.sh` → `dist/api/`). It ships for **both** targets — take the one
   matching your mod:

   ```
   net48/TwitchColony.Api.dll            <- classic ONI mod projects
   netstandard2.1/TwitchColony.Api.dll   <- newer mod solutions
   ```

   Mixing them isn't a hard error — a `<Reference>` to the net48 build resolves fine from a
   netstandard2.1 project — but you end up with `mscorlib` dragged in alongside `netstandard` in
   your merged assembly. Take the matching one and it stays clean.
2. Reference it from your mod project **and ILMerge/ILRepack it into your own DLL**:

   ```xml
   <Reference Include="TwitchColony.Api">
     <HintPath>libs\netstandard2.1\TwitchColony.Api.dll</HintPath>
     <Private>false</Private>
   </Reference>
   ```

   Merge it in — don't ship it loose next to your DLL. Two mods shipping different loose copies
   would fight over which one loads, which is the same mess PLib warns about.
3. Register from your `OnLoad`:

   ```csharp
   using TwitchColony.Api;

   public override void OnLoad(Harmony harmony) {
       base.OnLoad(harmony);

       TwitchColonyApi.RegisterEvent(
           id:          "mymod.meteor",           // prefix with your mod name, must be unique
           displayName: "Meteor shower!",         // what chat sees
           action:      ctx => MeteorShower.Start(),
           groupId:     "mymod.disasters",        // optional
           weight:      EventWeight.Uncommon,
           danger:      EventDanger.High,
           owner:       "My Mod");                // only used to name you in the log
   }
   ```

That's it. `RegisterEvent` never throws and returns `true` only if Twitch Colony took the event.

---

## The API

```csharp
bool TwitchColonyApi.RegisterEvent(
    string id,
    string displayName,
    Action<object> action,
    string groupId = null,
    EventWeight weight = EventWeight.Common,
    EventDanger danger = EventDanger.None,
    Func<object, bool> condition = null,
    string owner = null);

bool TwitchColonyApi.UnregisterEvent(string id);
bool TwitchColonyApi.TriggerEvent(string id);     // fire one now, for testing — see below
bool TwitchColonyApi.IsAvailable { get; }        // is Twitch Colony installed and talking to us?
int  TwitchColonyApi.InstalledApiVersion { get; } // 0 when it isn't installed
```

- Call it **once**, from `OnLoad`. Registered events survive colony reloads.
- `action` runs on the **game's main thread**, so it may touch the game freely.
- `id` must be unique. If it's already taken, your event is refused and the log says so — hence
  the `mymod.` prefix.

---

## What your action receives

The `object` your action gets describes what triggered the event. It is a plain
`Dictionary<string, object>` — **never cast it to a type of your own**. Read it with
`EventContext`:

```csharp
action: ctx => {
    int    cycle  = EventContext.GetInt(ctx, EventContext.Cycle, -1);
    int    votes  = EventContext.GetInt(ctx, EventContext.VoteCount);
    string source = EventContext.GetString(ctx, EventContext.Source);   // "chatVote" | "twitchPoll"
    string[] who  = EventContext.GetStrings(ctx, EventContext.Voters);  // empty for native polls

    MeteorShower.Start(intensity: votes);
}
```

| Key | Type | Meaning |
|---|---|---|
| `EventContext.EventId` | `string` | The id you registered. |
| `EventContext.Cycle` | `int` | Colony cycle it fired on, `-1` if unknown. |
| `EventContext.VoteCount` | `int` | Votes this option got. |
| `EventContext.Voters` | `string[]` | Nicks that voted for it. **Empty for native Twitch polls** — Twitch reports totals, not who voted. |
| `EventContext.Source` | `string` | `chatVote` or `twitchPoll`. |

Every getter tolerates missing keys and returns the fallback, so your event can't break because a
key wasn't filled in. New keys may be added later; existing ones won't be removed.

---

## Groups, weight, danger, conditions

**`groupId`** — events that feel alike should share one. When any event in a group fires, the whole
group is pushed down the draw order for the next few votes, so chat isn't offered three variations
of the same thing in a row. All the built-in floods share `"flood"`, for example. `null` = the
event is damped on its own after it fires.

Unlike `id`, group names are a **shared namespace** — and that's a choice you should make on
purpose. Name your group `"flood"` and your floods land in the same damping bucket as the built-in
ones: fire yours, and Twitch Colony's are less likely for a few votes too. That's often exactly
right — a flood is a flood, and the point is not to offer three in a row. If you'd rather your
events only damp each other, prefix the group like you do the id (`"mymod.floods"`).

**`weight`** — how often it's offered relative to others: `Rare` (1), `Uncommon` (2), `Common` (4,
the default), `VeryCommon` (8). `Never` (0) registers the event but keeps it out of the draw —
useful when you trigger it yourself.

**`danger`** — how much it can hurt: `None`, `Small`, `Medium`, `High`, `Deadly`. **Tag it
honestly.** Streamers filter on this to keep a run from being wrecked by something they never opted
into; an event marked `None` that can kill duplicants will ruin somebody's colony.

**`condition`** — checked when the vote options are drawn; return `false` and your event sits that
vote out. It gets the same payload shape as `action` (only `Cycle` is filled in at draw time). Keep
it cheap — it runs over the whole pool every draw — and don't change the world from inside it. A
condition that throws is treated as "no" and logged.

```csharp
// Only from cycle 20 onwards.
condition: EventConditions.FromCycle(20)

// Combine them.
condition: EventConditions.All(EventConditions.FromCycle(20), ctx => MyMod.IsReady)
```

`EventConditions` has `FromCycle`, `BeforeCycle`, `All`, `Any`, `Not` — or just write your own
`ctx => bool`.

---

## Why the API looks like this

Every value crossing between your mod and Twitch Colony is a type both assemblies already share:
`string`, `int`, `System.Action`, `System.Func`, `object`. That isn't laziness, it's the whole
design.

A C# interface or class is only the "same type" if it comes from the *same assembly*. If Twitch
Colony exported an `ITwitchEvent` interface and your mod implemented it, your mod would need a hard
reference to `TwitchColony.dll` — and would die on startup for every player who doesn't have Twitch
Colony installed. If instead the interface were compiled into both mods, the two copies would be
unrelated types at runtime and the cast would throw.

Delegates and BCL types have no such problem, so the bridge takes an `Action<object>` instead of an
event object, and the payload is a dictionary instead of a context class. It's less pretty and it
always works.

Credit where it's due: this design was recommended by **Sgt_Imalas**, who hit exactly these walls
building the mod API for [Oni-Together](https://github.com/Lyraedan/Oxygen_Not_Included_Together).

If you'd rather not reflect at all, `TwitchColony.Api.EventBridge.RegisterEvent` in the main DLL is
the same call — but then you're back to a hard dependency. The merge-lib exists precisely so you
don't need one.

---

## Testing your events without waiting for chat

Waiting out a vote to see whether your event works gets old fast. `TriggerEvent` fires one straight
away by id:

```csharp
// In your mod's Update, behind a debug flag:
if (Input.GetKeyDown(KeyCode.F9)) TwitchColonyApi.TriggerEvent("mymod.confetti");
```

It works on any registered id, including Twitch Colony's own (`"flood_water"`, `"kill_dupe"` …), and
returns false if the id isn't registered or the mod isn't installed.

It **skips the vote entirely**, which means it also skips the streamer's danger ceiling and your
event's own condition — that's the point for testing, and the reason not to wire it to anything a
viewer can reach. Call it on the main thread with a colony loaded.

## A note on emoji

The game's fonts have no emoji, and TextMeshPro draws a missing glyph as a hollow box. Anything the
font can't render is dropped from a speech bubble rather than shown as a square, so if your event
puts an emoji in a bubble, expect it to quietly vanish. Short words work; so does anything the game
already displays elsewhere.

## Licence

Twitch Colony, `TwitchColony.Api.dll` and the example add-on are **MIT licensed** — so yes, you may
merge the API library into your mod and ship it, including on the Workshop, commercially, whatever.
Keep the copyright notice somewhere (a line in your credits or mod description is plenty). Full text
in [LICENSE](LICENSE).

Worth saying out loud, because it bites people in this ecosystem: source being public on GitHub is
**not** permission to use it. A repository with no licence is "all rights reserved" and you may not
legally copy from it, however open it looks. That's the situation with the original Twitch
Integration mod, and it's why this one has a licence file.

## Versioning

`TwitchColonyApi` looks the bridge up by its exact signature. If a future Twitch Colony makes a
breaking change, the lookup finds nothing, your events are skipped with a warning in `Player.log`,
and **nothing throws**. Check `IsAvailable` if you want to know.

Current contract: **v1**.

Found a problem, or need something the API can't express? Open an issue — the contract is meant to
grow, not to be worked around.
