# Twitch Colony

Twitch chat integration for **Oxygen Not Included** — an independent mod inspired by
asquared31415's *Twitch Integration* (see `CREDITS.md`).

Two features, both opt-in via config:

- **Chat bubbles** — a chat message shows as a speech bubble above the duplicant whose
  name matches the chatter's Twitch nick. Opt-in per message via a configurable prefix.
- **Event voting** — chat votes (by counting `!vote N` messages) or via a **native Twitch
  poll** to decide which colony event fires.

This repository is the **from-scratch skeleton**: connection, config, bubbles, voting, and a
few safe sample events are in place. Real gameplay events are yours to add (see
`src/Events/SampleEvents.cs`).

---

## Building (Linux dev server or any machine with .NET SDK 8)

1. Put the game's reference DLLs in `./lib/` (copied from the ONI install, never committed):
   `…/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data/Managed/` → `./lib/`
2. Build:
   ```bash
   ./build.sh
   ```
   Output lands in `dist/TwitchColony/` (one `TwitchColony.dll` + the two yaml files).
   No wine, no ILRepack — the mod references the game DLLs instead of bundling them.

## Installing / testing (Windows machine with the game)

Copy the **contents** of `dist/TwitchColony/` into:

```
…\Documents\Klei\OxygenNotIncluded\mods\Local\TwitchColony\
```

`mod_info.yaml` must sit directly in that folder. Enable the mod in-game, load a colony,
and watch `…\Documents\Klei\OxygenNotIncluded\Player.log` for lines tagged `[TwitchColony]`.

## Configuration

On first run the mod writes `config.json` to
`…\Documents\Klei\OxygenNotIncluded\config_twitchcolony\`. Edit it with the game closed.
Key fields: `Channel`, `Nick`, `OauthToken`, `BubblePrefix`, `EnableEvents`,
`UseTwitchPolls`, `VotingSeconds`, `OptionsPerVote`. See `src/Config/ModConfig.cs` for all
fields and defaults.

Anonymous chat read (bubbles + chat voting) works with just `Channel` set. Native Twitch
polls need a token with `channel:manage:polls` + `channel:read:polls` (Affiliate/Partner),
or the Twitch CLI mock via the `*Override` fields.

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
