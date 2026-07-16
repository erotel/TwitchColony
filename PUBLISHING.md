# Publishing to the Steam Workshop

Oxygen Not Included does **not** upload mods through the Steam website. You publish from
**inside the game**, using the special **`Dev`** mods folder. This file records the process
and the store copy so a re-upload is repeatable.

> You need the **Steam version** of the game and a Steam account. Workshop upload is not
> possible on the headless build server — do this on the Windows machine with the game.

## 0. Enable developer mode (required — this is what shows the publish button)

The Workshop publish/manage buttons only appear when the game is in **developer mode**.
Without it, a mod in `Dev\` shows up but has **no upload button** — this is the usual reason
"there's no button".

Create an **empty** file named exactly `debug_enable.txt` in the game's save folder:

```
Documents\Klei\OxygenNotIncluded\debug_enable.txt
```

(the same folder as `Player.log` and `config_twitchcolony\`, **not** inside `mods\`). Watch
the extension — it must end in `.txt` once, not `debug_enable.txt.txt`. Restart the game; the
Mods screen now shows dev-mod management controls.

## 1. Put the built mod in the `Dev` folder

Build (`./build.sh`) so `dist/TwitchColony/` holds `TwitchColony.dll`, `mod_info.yaml`,
`mod.yaml`. Copy those into — note **`Dev`**, not `Local`:

```
Documents\Klei\OxygenNotIncluded\mods\Dev\TwitchColony\
```

`mod_info.yaml` must sit directly in that folder. Only mods under `Dev\` get the
Workshop-publish buttons in-game; `Local\` mods don't.

## 2. Add a preview image

Put a **`preview.png`** in the same `Dev\TwitchColony\` folder (square, **512×512**
recommended; png/jpg). Best is a real in-game screenshot — a chat bubble above a duplicant
plus the vote HUD. This becomes the Workshop thumbnail.

## 3. Publish from the game

1. Launch ONI → **Mods**.
2. Find **Twitch Colony** (dev mods are listed with extra management controls).
3. Click the manage / **⋯** control → **Publish to Steam Workshop** (first time) or
   **Update** (subsequent uploads).
4. Accept the Steam Workshop Legal Agreement if prompted.
5. The game creates the Workshop item and links it to the mod's `staticID` (`TwitchColony`),
   so future **Update** clicks patch the same item — don't re-publish a second copy.

## 4. Finish on the Steam Workshop page

The new item starts **Hidden**. Open it (Steam → your profile → Workshop items), then:

- Set **Visibility** to **Public** when ready.
- Paste the **title / description** (below).
- Add **tags** (below).
- Confirm the preview image looks right.

## 5. Updating later

Bump `version` in `mod_info.yaml`, rebuild, copy the new files over the `Dev\TwitchColony\`
folder, then in-game **Mods → Twitch Colony → Update**. Same Workshop item, new build.

---

## Store copy (paste into the Workshop page)

**Title:** `Twitch Colony`

**Description** (Steam BBCode):

```
[b]Twitch Colony[/b] connects your Oxygen Not Included colony to your Twitch chat.

[b]What it does[/b]
[list]
[*][b]Chat bubbles[/b] — a viewer's message pops up as a speech bubble above the duplicant whose name matches their Twitch nick.
[*][b]Event voting[/b] — chat votes on which colony event fires next, either by typing in chat (!vote 1/2/3) or via a native Twitch poll.
[*][b]70+ events[/b] — stress spikes & relief, instant builds/digs/research, floods & element dumps, critter rain, turbo/slow/sleepy dupes, a surprise box, heal/cure/blackout and more.
[/list]

[b]Getting started[/b]
[list]
[*]Enable the mod and load a colony.
[*]Set your channel in the config file it creates (config_twitchcolony/config.json).
[*]Open the pause menu and click "Start Twitch Votes".
[/list]
Anonymous chat reading (bubbles + chat voting) needs no login at all. Native Twitch polls need an Affiliate/Partner account. Full setup, config reference and a one-click token link are in the INSTALL guide on GitHub.

[b]Other modders[/b] can add their own events via a small public API — see the GitHub README.

[b]Independent mod[/b] inspired by asquared31415's Twitch Integration. Written from scratch; no code or assets from the original are reused. See CREDITS on GitHub.

Source & docs: https://github.com/erotel/TwitchColony
```

**Suggested tags:** `Twitch`, `Interaction`, `Overlay`/`UI`, `Events`, `All DLCs` (mark it
compatible with the base game and expansions since `supportedContent: ALL`).

**Links to set:** point the Workshop item's source/description at
`https://github.com/erotel/TwitchColony`.
