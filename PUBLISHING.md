# Publishing to the Steam Workshop

Oxygen Not Included is **not** published from the Steam website, and **not** from an in-game
button. It has a **separate uploader application** — the *Oxygen Not Included Uploader*
(Steam **App ID 636750**) — that you own for free because you own the game. This file records
the process and the store copy so a re-upload is repeatable.

> Do this on the Windows machine with the game + Steam. Not possible on the headless build
> server.
>
> There is **no publish button inside the game**, and `debug_enable.txt` / developer mode is
> **not** involved. If you were looking for a button in the Mods screen, that's why it isn't
> there — publishing lives in the uploader tool below.

## 1. Build & stage the mod files

Build (`./build.sh`) so `dist/TwitchColony/` holds `TwitchColony.dll`, `mod_info.yaml`,
`mod.yaml`. Put those (plus the preview image from step 2) in one folder the uploader can
point at. The `mods\Dev\TwitchColony\` test folder works fine as the source:

```
Documents\Klei\OxygenNotIncluded\mods\Dev\TwitchColony\
```

(The `Dev\` folder is for **testing the mod in-game**; it isn't required for publishing — the
uploader can point at any folder. `mod_info.yaml` must sit directly in it.)

## 2. Add a preview image

Put a **`preview.png`** in that folder (square, **512×512** recommended; png/jpg, under
1 MB). Best is a real in-game screenshot — a chat bubble above a duplicant plus the vote HUD.
This becomes the Workshop thumbnail.

## 3. Install & run the uploader tool

1. In Steam, open your **Library** and switch the type filter to **Tools** (the dropdown above
   the library list — tools are hidden from the Games view). Find **Oxygen Not Included
   Uploader** and **Install** it.
   - Can't see it? Trigger the install directly with `steam://install/636750`, or look it up
     via [SteamDB App 636750](https://steamdb.info/app/636750/).
2. **Launch** the uploader.

## 4. Publish

1. Click **Add** (top-left).
2. Fill in:
   - **Content folder** → the folder from step 1 (with the `.dll`, both yaml files, and
     `preview.png`).
   - **Preview image** → `preview.png`.
   - **Title**, **Description**, **Tags** — use the store copy below.
3. Click **Publish!** and wait for it to finish. Accept the Steam Workshop Legal Agreement if
   prompted.
4. The item is created **Hidden**. Open it (Steam → your profile → **Workshop Items**) and set
   **Visibility → Public** when ready. Double-check the thumbnail and description there.

## 5. Updating later

Bump `version` in `mod_info.yaml`, rebuild, refresh the files in the source folder, then in the
uploader **select the existing item** (don't click *Add* again — that makes a second copy) and
re-publish. Same Workshop item, new build.

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
