# Twitch Colony

Twitch chat integration for **Oxygen Not Included** — an independent mod inspired by
asquared31415's *Twitch Integration* (see `CREDITS.md`).

Two features, both opt-in via config:

- **Chat bubbles** — a chat message shows as a speech bubble above the duplicant whose
  name matches the chatter's Twitch nick. Opt-in per message via a configurable prefix.
- **Event voting** — chat votes (by counting `!vote N` messages) or via a **native Twitch
  poll** to decide which colony event fires.

Voting runs as a state machine: a **"Start Twitch Votes"** button in the in-game pause menu
kicks off the first round; after that each round auto-restarts after `VoteDelay` seconds
(the countdown freezes while the game is paused).

---

## Events

The colony ships with ~50 voteable events, grouped by file in `src/Events/`:

- **Duplicant amounts** (`ColonyEvents.cs`) — stress spike / relief, feast, exhaustion, full
  bladder, free research, kill a random dupe.
- **Attributes** (`AttributeEvents.cs`) — athletics / construction / excavation / strength
  up & down (our own `Klei.AI` effects, registered in `EventHelpers.cs`), sleepy dupes,
  turbo dupes (30 s all-round speed/work boost), slow dupes (30 s sluggish), nap time
  (drains stamina to zero so dupes pass out on the spot).
- **Instant orders** (`OrderEvents.cs`) — finish all planned buildings whose materials are
  available in the colony (storage or loose on the ground; the rest are skipped), finish all
  outstanding dig orders, complete the current research.
- **World / elements** (`WorldEvents.cs`) — global warming, ice age, oxygen removal, element
  floods (water, polluted water, ethanol, oil, lava, molten gold, nuclear waste), element
  dumps (common / metal / exotic / gas), poopsplosion, fart, tile heat/chill.
- **Spawn / misc** (`SpawnEvents.cs`) — spawn Puft / Pokeshell / Gassy Moo comet / Atmo Suit /
  Vacillator charge, rain Morbs / Slicksters / Pacu / Beetas / gold / diamonds, free skill
  points, snazzy suit, solar eclipse, "surprise" (fires a random other event).
- **Surprise box** (`SurpriseBoxEvent.cs`) — spawns a box above a printing pod, optionally
  pans/zooms the camera to it (`SurpriseBoxZoom` config), counts down ~5 s, then bursts out
  1–5 random pickupable items with a launch arc.
- **Support / disruption** (`UtilityEvents.cs`) — heal all, cure diseases, zen nirvana
  (stress wipe + morale), charge / drain (blackout) batteries, oxygen wave, ripen crops /
  blight, demolish a random building, gather dupes at the pod, scatter dupes, interrupt all work.

**Attribution & independence.** These events are **independent reimplementations** of the
*ideas* behind asquared31415's *Twitch Integration* events, written from scratch against
vanilla game API only. No source code or assets from the original mod are copied or bundled
(the original ships no license — all rights reserved — so copying it is not permitted; see
`CREDITS.md`).

**Not (yet) reimplemented**, because they depend on large custom systems or assets that would
have to be recreated from scratch: Pocket Dimension (custom worldgen), critter Morph, Geyser
tuning, Party Time (needs a render tint patch), Uninsulate/tile registries, Spice Food (DLC),
Banshee Wail chores.

To add events **inside this repo**, subclass `GameEvent` and register them in
`EventRegistry.RegisterDefaults()`.

---

## Modding API — add events from another mod

Other mods can contribute their own events without touching this repo. Your event joins the
vote pool, the on-screen HUD, and the chat announcements automatically — you only implement its
effect.

1. Reference `TwitchColony.dll` from your project (`Private=false`, so you don't bundle a copy):
   ```xml
   <Reference Include="TwitchColony">
     <HintPath>path\to\TwitchColony.dll</HintPath>
     <Private>false</Private>
   </Reference>
   ```
2. Subclass `GameEvent` and register it once from your `UserMod2.OnLoad`:
   ```csharp
   using HarmonyLib;
   using KMod;
   using TwitchColony.Events;

   public sealed class MyEvent : GameEvent
   {
       public override string Id => "mymod.confetti";        // unique; duplicate Ids are ignored
       public override string DisplayName => "Confetti storm"; // shown in the vote / HUD / chat
       public override void Trigger()                          // runs on the main thread when it wins
       {
           // your effect here, using vanilla game API
       }
   }

   public sealed class MyMod : UserMod2
   {
       public override void OnLoad(Harmony harmony)
       {
           base.OnLoad(harmony);
           EventRegistry.AddEvent(new MyEvent());
       }
   }
   ```

Notes:
- `AddEvent` persists across colony reloads; call it once at load. Duplicate `Id`s are ignored.
- Advanced: subscribe to `EventRegistry.Registering` to add events on every colony load.
- The public API is intentionally tiny and stable: `GameEvent` (`Id`, `DisplayName`, `Trigger`)
  and `EventRegistry.AddEvent` / `Registering`. `Trigger()` is always called on the main thread.
- Twitch Colony must be installed and enabled for your events to appear; it stays a single DLL.

> **Not runtime-tested.** The build server cannot run the game, so events are compile-verified
> only. Verify behavior on a Windows machine with the game and watch `Player.log` for
> `[TwitchColony]` lines. Effect strengths / element masses may need balancing.

---

## Building (Linux dev server or any machine with .NET SDK 8)

1. Put the game's reference DLLs in `./lib/` (copied from the ONI install, never committed):
   `…/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data/Managed/` → `./lib/`
2. Build:
   ```bash
   ./build.sh
   ```
   Output lands in `dist/TwitchColony/` (one `TwitchColony.dll` + the two yaml files).
   No wine — the game DLLs are referenced, never bundled. [PLib](https://github.com/peterhaneve/ONIMods)
   (the in-game settings screen) is ILRepacked into the DLL, so the mod still ships as a
   single assembly.

## Installing & configuring

Settings live behind the **gear icon** next to the mod in the game's Mods list; `config.json`
still works if you prefer files.

See **[INSTALL.md](INSTALL.md)** for the full guide — Workshop and manual install, the
settings screen and where `config.json` lives, copy-paste config recipes (bubbles only /
chat voting / chat announcements / native polls), a field-by-field reference, OAuth token
scopes, and troubleshooting.

Short version for a manual/local install: copy the **contents** of `dist/TwitchColony/`
(`TwitchColony.dll` + both yaml files) into
`…\Documents\Klei\OxygenNotIncluded\mods\Local\TwitchColony\` (with `mod_info.yaml`
directly in that folder), enable the mod, load a colony, set `Channel` in the generated
`config.json`, and click **"Start Twitch Votes"** in the pause menu. Watch
`…\Documents\Klei\OxygenNotIncluded\Player.log` for lines tagged `[TwitchColony]`.

## Layout

```
src/
  TwitchColonyMod.cs      UserMod2 entry + Harmony bootstrap
  Patches.cs              Game.OnSpawn hook that starts the runtime
  MainThread.cs           background→main-thread dispatcher
  Config/ModConfig.cs     JSON config
  Twitch/                 IRC client + parser, Helix polls, token auth
  Voting/VoteController.cs chat + poll voting
  Events/                 event base, registry, sample events
  UI/SpeechBubbles.cs     chat speech bubbles
```
