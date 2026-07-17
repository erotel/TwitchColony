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
[b]Twitch Colony[/b] hands your Oxygen Not Included colony over to your Twitch chat.

[b]What it does[/b]
[list]
[*][b]Chat bubbles[/b] — a viewer's message pops up as a speech bubble above the duplicant whose name matches their Twitch nick.
[*][b]Event voting[/b] — chat votes on what happens to your colony next, either by typing in chat (!vote 1/2/3) or in a native Twitch poll.
[*][b]73 events[/b] — stress spikes and group therapy, instant builds/digs/research, floods and element dumps, raining critters, turbo/sleepy dupes, care packages, blackouts, heat waves… up to lava, if you allow it.
[*][b]Critter adoption[/b] — a viewer types !adopt and a critter is named after them, with their nick on a tag under it. Their chat then bubbles above their critter.
[*][b]Sub celebrations[/b] — a NEW SUB banner and the whole colony stops to cheer.
[/list]

[b]You decide how mean chat can be[/b]
Every event is tagged with how much it can hurt, from harmless to deadly. Set the ceiling and chat simply cannot cross it — pick "up to costly" and nobody dies, however hard they vote for it. The danger can also ramp up as the colony ages, so your first cycles are safe while you get set up.

[b]Getting started[/b]
[list]
[*]Enable the mod, open [b]Mods[/b] and click the [b]gear icon[/b] next to Twitch Colony.
[*]Type in your channel name. That's the only required setting.
[*]Load a colony, open the pause menu and click "Start Twitch Votes" — or have it start on its own after a few cycles.
[/list]
Bubbles and chat voting work [b]anonymously — no login, no token, nothing to set up[/b]. A token is only needed if you want the bot to talk in chat or to run native Twitch polls (those also need an Affiliate/Partner account). Full setup and a one-click token link are in the INSTALL guide on GitHub.

[b]For mod authors[/b]
Your mod can add its own events to the vote pool, and it doesn't have to depend on this one: the API library reflects into Twitch Colony at runtime and does nothing when it isn't installed, so your mod still works for players without it. Guide, library and a working example: https://github.com/erotel/TwitchColony/blob/main/MODDING.md

[b]Independent mod[/b] inspired by asquared31415's Twitch Integration. Written from scratch; no code or assets from the original are reused. Thanks to Sgt_Imalas for the review that shaped the modding API. See CREDITS on GitHub.

Source, docs & issues: https://github.com/erotel/TwitchColony
```

**Change notes for the 1.4.4 update.** The Workshop is still on 1.3.0, so this covers everything
since — use this one and ignore the older blocks below.

```
[b]Settings are in the game now[/b] — Mods → the gear icon next to Twitch Colony. Channel, bubbles, voting, adoption and subs, all on one screen. Your old config.json is imported automatically; editing files still works if you prefer.

[b]You control how dangerous chat can get[/b] — every event is tagged from harmless to deadly, and you set the ceiling chat can never cross. It can also ramp up with cycles, so the first cycles stay safe while you get set up. On by default; turn it off for the old free-for-all.

[b]Drag the vote panel wherever you like[/b] — top centre is a bad guess when you have a camera and an overlay to work around. It remembers where you put it. The whole UI also follows the game's UI scale setting now, which it embarrassingly didn't before: at 4K it was a postage stamp.

[b]Your OAuth token moved[/b] to its own file (mods/config/TwitchColony/token.txt) and is deliberately not on the settings screen — a token shown on stream is a password shown on stream. An existing token is moved for you.

[b]Fixes[/b]
[list]
[*]The mod applied its Harmony patches twice — this caused the duplicate chat connection and the doubled pause-menu button.
[*]Emoji drew as hollow boxes. The game's fonts have none, so anything unrenderable is now dropped from bubbles and banners instead.
[*]Starting a second colony without restarting the game left voting dead and the vote button greyed out.
[*]A vote left running when you returned to the main menu used to finish there and fire its event into nothing. The countdown also pauses with the pause menu now (but still not with game speed — chat votes in real time).
[*]"Instant build" produced buildings at zero kelvin.
[*]The vote button greys out the moment you click it.
[*]A viewer who already has a duplicant can no longer adopt a critter as well.
[*]A config.json with a typo in it no longer costs you your settings.
[*]Chat bubbles and critter name tags no longer draw on top of the pause menu.
[/list]

[b]Mods can add their own events now[/b] — there's a proper modding API, and add-ons using it don't force players to install this mod. See MODDING.md on GitHub.

Thanks to Sgt_Imalas, who found a great deal of this while hooking his own event mod up to the API.
```

**Change notes for the 1.4.2 update** (use this one if 1.4.1 never went up — it covers both):

```
[b]Emoji no longer show up as boxes[/b] — the game's fonts have no emoji, so speech bubbles were sprouting little squares. The mod's own bubbles use short words now, and anything the font can't draw is dropped from a bubble or banner instead of drawn as a box. That matters most for chat: viewers type emoji constantly. Normal text, accents and other alphabets are untouched.

[b]The "Start Twitch Votes" button greys out the moment you click it[/b], instead of waiting until you reopen the pause menu.

[b]Mods that add their own events can now put messages on screen[/b] — a banner across the top, or a speech bubble over a duplicant — so their events can warn you what just landed or deliver a punchline. They can also trigger events on demand while testing, and the API library ships for netstandard2.1 as well as net48.

Thanks to Sgt_Imalas, who found all of this while hooking his own event mod up to the API.
```

**Change notes for the 1.4.1 update:**

```
[b]Emoji no longer show up as boxes[/b] — the game's fonts have no emoji, so speech bubbles were sprouting little squares. The mod's own bubbles use short words now, and anything the font can't draw is dropped from a bubble instead of drawn as a box. That last part matters most for chat: viewers type emoji constantly. Normal text, accents and other alphabets are untouched.

[b]The "Start Twitch Votes" button greys out the moment you click it[/b], instead of waiting until you reopen the pause menu.

[b]For mod authors[/b] — the API can now trigger an event on demand for testing, and the library ships for netstandard2.1 as well as net48. See MODDING.md on GitHub.

Thanks to Sgt_Imalas, who found all three while hooking his own events up to the API.
```

**Change notes for the 1.4.0 update** (the uploader asks for these separately from the description
— they're what subscribers see in their update feed):

```
[b]Settings are in the game now[/b] — Mods → the gear icon next to Twitch Colony. Channel, bubbles, voting, adoption and subs, all in one screen. Your old config.json is imported automatically; editing files still works if you prefer.

[b]You control how dangerous chat can get[/b] — every event is tagged from harmless to deadly, and you set the ceiling chat can never cross. It can also ramp up with cycles, so the first cycles stay safe while you get set up. On by default; turn it off for the old free-for-all.

[b]Your OAuth token moved[/b] to its own file (mods/config/TwitchColony/token.txt) and is deliberately not on the settings screen — a token shown on stream is a password shown on stream. An existing token is moved for you.

[b]Other mods can add events[/b] — there's a proper modding API now, and add-ons using it don't force players to install this mod. See MODDING.md on GitHub.

[b]Fixes[/b]
[list]
[*]The mod applied its Harmony patches twice — this caused the duplicate chat connection and the doubled pause-menu button.
[*]Starting a second colony without restarting the game left voting dead and the vote button greyed out.
[*]A vote left running when you went back to the main menu used to finish there and fire its event into nothing.
[*]The bot now disconnects from chat when you leave a colony.
[*]A viewer who already has a duplicant can no longer adopt a critter as well.
[*]A config.json with a typo in it no longer costs you your settings.
[*]Chat bubbles and critter name tags no longer draw on top of the pause menu.
[/list]

Thanks to Sgt_Imalas for the review that shaped the modding API, and for spotting the double patching.
```

**Suggested tags:** `Twitch`, `Interaction`, `Overlay`/`UI`, `Events`, `All DLCs` (mark it
compatible with the base game and expansions since `supportedContent: ALL`).

**Links to set:** point the Workshop item's source/description at
`https://github.com/erotel/TwitchColony`.
