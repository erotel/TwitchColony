# Installing & configuring Twitch Colony

A step-by-step guide to getting the mod running and tuning it. If you only want the
short version: **subscribe on the Workshop → enable the mod → open the mod's settings
(gear icon in the Mods list) → set your `Channel` → load a colony → click "Start Twitch
Votes" in the pause menu.**

- [1. Install the mod](#1-install-the-mod)
- [2. Settings — in-game screen or config.json](#2-settings--in-game-screen-or-configjson)
- [3. Quick-start recipes](#3-quick-start-recipes)
- [4. Full configuration reference](#4-full-configuration-reference)
- [5. Getting an OAuth token](#5-getting-an-oauth-token)
- [6. Starting a vote & how voting works](#6-starting-a-vote--how-voting-works)
- [7. Troubleshooting](#7-troubleshooting)

---

## 1. Install the mod

### A) Steam Workshop (recommended)

1. Open the mod's Workshop page and click **Subscribe**.
2. Launch Oxygen Not Included. Steam downloads the mod automatically.
3. In the main menu open **Mods**, find **Twitch Colony**, toggle it **on**, and let
   the game restart when prompted.

That's it — no files to copy. Workshop keeps the mod up to date automatically.

### B) Manual / local install

Use this for a `.zip` release (GitHub Releases) or your own build.

1. Grab the release archive (`TwitchColony-vX.Y.Z.zip`) or the contents of
   `dist/TwitchColony/` if you built it yourself. You need three files:
   `TwitchColony.dll`, `mod_info.yaml`, `mod.yaml`.
2. Copy them into a **new folder** here:

   ```
   Documents\Klei\OxygenNotIncluded\mods\Local\TwitchColony\
   ```

   `mod_info.yaml` must sit **directly** in that `TwitchColony\` folder (not in a
   nested subfolder).
3. Start the game, open **Mods**, enable **Twitch Colony**, restart when asked.

> **Windows path tip:** `Documents` is your real Documents folder. The full path is
> usually `C:\Users\<you>\Documents\Klei\OxygenNotIncluded\`.

Minimum game version: **U59+** (`minimumSupportedBuild: 740622`).

---

## 2. Settings — in-game screen or config.json

### The easy way: the settings screen

Open **Mods** in the main menu, find **Twitch Colony**, and click the **gear icon** next to
it. Everything you normally tune is there, grouped into *Twitch connection*, *Chat bubbles*,
*Critter adoption*, *Voting* and *Twitch subs*. Click **OK** and it's saved.

Bare minimum to do anything useful: set **Channel** to your Twitch channel login (all
lowercase, the name from your `twitch.tv/<name>` URL).

Most settings take effect the moment you hit OK. **Channel, bot nick and the token** are the
exception — the chat connection is opened once when a colony loads, so those three only apply
after you reload the colony.

> **The OAuth token is deliberately not on the settings screen.** You're a streamer: if you
> opened that screen on air, your token would be sitting on the stream in plain text for
> anyone to copy — and a chat token is a password. It goes in `token.txt` instead (§5).

### The files

The mod writes two files when the game starts with the mod enabled:

```
Documents\Klei\OxygenNotIncluded\mods\config\TwitchColony\config.json   <- all settings
Documents\Klei\OxygenNotIncluded\mods\config\TwitchColony\token.txt     <- just the OAuth token
```

The **MANUAL CONFIG** button on the settings screen opens that folder for you. The exact paths
are also printed in `Player.log` on every load, so you never have to guess.

**Why the token has its own file:** the settings screen loads `config.json` when it opens and
writes the whole file back when you press **OK**. Anything you typed into `config.json` while
that screen was open gets overwritten by its older copy. A token pasted there could quietly
vanish — so it lives in a file the settings screen never touches. If you still have a token in
`config.json` from an earlier version, the mod moves it into `token.txt` for you on the next
start and says so in the log.

**Edit the file with the game closed**, then start the game again — it's read at startup.

> **Upgrading from 1.3.0 or older?** Nothing to do. Your old
> `Documents\Klei\OxygenNotIncluded\config_twitchcolony\config.json` is imported automatically
> the first time 1.4.0 starts, and the old file is kept as `config.json.migrated` in case you
> want it back. Edit the new path from then on.

New settings added by a mod update are written into your file automatically, with their
defaults, without touching what you'd already set.

If the file ever gets a syntax error (a missing comma is the classic), the mod logs a warning,
runs on defaults for that session, and **leaves your file alone** so nothing is lost — fix the
JSON, or delete the file and restart to regenerate a clean one.

---

## 3. Quick-start recipes

Copy one of these into `config.json`, replace `yourchannel`, save, and load a colony.
Each shows only the fields that matter for that setup; everything else keeps its default.
(You can set all of this on the in-game settings screen too — the recipes are just faster to
paste, and the token has to go in the file anyway.)

### Recipe 1 — Chat bubbles only, no login (anonymous)

Messages that start with `!say` pop up as a speech bubble over the duplicant whose name
matches the chatter's Twitch nick. No token needed — the mod reads chat anonymously.

```json
{
  "Channel": "yourchannel",
  "EnableBubbles": true,
  "BubblePrefix": "!say",
  "EnableEvents": false
}
```

### Recipe 2 — Chat voting, no login (anonymous)

Viewers vote by typing `!vote 1`, `!vote 2`, … The mod counts messages; the event with
the most votes fires. Works fully anonymously (read-only).

```json
{
  "Channel": "yourchannel",
  "EnableEvents": true,
  "UseTwitchPolls": false,
  "VoteCommandPrefix": "!vote",
  "VotingSeconds": 60,
  "OptionsPerVote": 3,
  "AnnounceInChat": false
}
```

### Recipe 3 — Chat voting **and** the bot announces options + winner in chat

Same as recipe 2, but the mod also posts the vote options and the winner into chat. This
**requires a login**: `Nick` here, plus a token with the `chat:edit` scope in `token.txt`
(see §5). The `Nick` can be your own account or a dedicated bot account.

```json
{
  "Channel": "yourchannel",
  "Nick": "yourbotname",
  "EnableEvents": true,
  "UseTwitchPolls": false,
  "AnnounceInChat": true
}
```

### Recipe 4 — Native Twitch polls (Affiliate/Partner)

Instead of counting chat messages, the mod opens a **real Twitch poll** and reads the
result. Requires an Affiliate/Partner channel and a token (in `token.txt`, see §5) with
`channel:manage:polls` + `channel:read:polls`.

```json
{
  "Channel": "yourchannel",
  "Nick": "yourchannel",
  "EnableEvents": true,
  "UseTwitchPolls": true,
  "VotingSeconds": 60,
  "OptionsPerVote": 3
}
```

`OptionsPerVote` must be **2–5** for native polls (Twitch's limit). Leave
`ClientIdOverride` / `BroadcasterIdOverride` empty — they're only for the CLI mock (see
the developer notes in `CLAUDE.md`).

> **Affiliate/Partner only.** Even with a valid poll token, Twitch only lets
> **Affiliate/Partner** channels create polls; on a regular account the poll call fails with
> `403` and nothing opens. If you're not there yet, use Recipe 2 (chat voting) instead. See
> [§5](#5-getting-an-oauth-token) for the full explanation.

---

## 4. Full configuration reference

Defaults are what the mod writes on first run. Every field below is also on the in-game
settings screen (§2), **except** the Helix overrides, which are file-only. The OAuth token
isn't in `config.json` at all — it has its own file (§2).

### Twitch connection

| Field | Default | What it does |
|---|---|---|
| `Channel` | `""` | Twitch channel login to join (lowercase, from your URL). **Required for anything to happen.** |
| `Nick` | `""` | Login used to *send* messages. Empty = anonymous read-only (bubbles + chat voting still work; the bot just can't talk or run native polls). |
| *(the token)* | — | Not a `config.json` field: paste it into `token.txt` in the same folder, **without** the `oauth:` prefix. Empty = anonymous. See §2 and §5. |

### Chat bubbles

| Field | Default | What it does |
|---|---|---|
| `EnableBubbles` | `true` | Master switch for speech bubbles. |
| `BubblePrefix` | `"!say"` | Only messages starting with this become bubbles. `""` = every message becomes a bubble. |
| `MaxBubbleLength` | `100` | Max characters shown in a bubble. |
| `BubbleSeconds` | `4` | How long a bubble stays on screen. |
| `BubbleCooldownSeconds` | `5` | Per-user cooldown, to stop one chatter spamming bubbles. |
| `BubbleFontSize` | `10` | Font size. |
| `BubbleMaxWidth` | `100` | Wrap width (UI units) before text wraps to a new line. |
| `BubbleFont` | `""` | Name of a game TMP font. `""` = default. If you set a name that doesn't exist, the mod logs the list of available font names to `Player.log`. |

A bubble appears only when a live duplicant's name matches the chatter's nick (case-insensitive),
**or** an adopted critter carries that nick (see below). No match → no bubble.

### Critter adoption

Viewers can "adopt" a critter — it gets renamed to their Twitch nick, and from then on their chat
messages also pop up as bubbles above that critter, just like duplicants.

| Field | Default | What it does |
|---|---|---|
| `EnableCritterAdopt` | `true` | Master switch for the adopt command. |
| `AdoptCommand` | `"!adopt"` | A viewer types this in chat to adopt a random free (un-adopted) critter. One critter per viewer; if none are free, nothing happens. With `AnnounceInChat` + a login, the bot confirms in chat. |
| `ShowAdoptedNameTag` | `true` | Show a small persistent name label under each adopted critter (the owner's nick), like a nameplate. Set `false` to keep only the chat bubbles. |

### Voting

| Field | Default | What it does |
|---|---|---|
| `EnableEvents` | `true` | Master switch for the event/voting system. |
| `StartAfterCycles` | `0` | Cycles to wait after the colony loads before the **first** vote auto-starts, giving the streamer time to prepare. `0` = don't auto-start; begin manually with the pause-menu button. The button still works to start earlier. Counted from load, so it works on both new and loaded colonies. |
| `UseTwitchPolls` | `false` | `false` = count chat votes; `true` = native Twitch poll (needs the poll scopes + Affiliate/Partner). |
| `VotingSeconds` | `60` | Length of the voting window. |
| `VoteDelay` | `540` | Seconds between one vote ending and the next starting (auto-restart). Default ≈ 9 minutes. The countdown **pauses while the game is paused.** |
| `OptionsPerVote` | `3` | How many random events are offered per vote. **2–5** for native polls. |
| `VoteCommandPrefix` | `"!vote"` | Chat-vote command; viewers type e.g. `!vote 2`. |
| `AnnounceInChat` | `true` | Post the options and the winner into chat. Needs `Nick` + a token with `chat:edit`; otherwise it silently does nothing. |
| `SurpriseBoxZoom` | `true` | The Surprise-Box event pans/zooms the camera to the box. Set `false` if you dislike the camera moving. |

### Twitch subs

When a viewer subscribes, resubscribes, or gifts a sub, the HUD shows a **"NEW SUB" banner** and the
**duplicants cheer** (clap / thumbs-up / sing) — a pure celebration, no gameplay change. These arrive
over the normal chat stream — **no Helix API, scopes, or extra login needed** (the channel just has to
have subs, i.e. Affiliate/Partner). Works even on the anonymous connection.

| Field | Default | What it does |
|---|---|---|
| `EnableSubRewards` | `true` | Banner + duplicant cheer on each sub / resub / gifted sub. |
| `SubRewardCooldownSeconds` | `12` | Minimum gap between celebrations, so a sub-train / mass gift can't restart every dupe's emote dozens of times. |

### Twitch Helix (native polls — advanced)

| Field | Default | What it does |
|---|---|---|
| `HelixBaseUrl` | `https://api.twitch.tv/helix` | Twitch API base. Only change it to point at the Twitch CLI mock. |
| `ClientIdOverride` | `""` | Leave empty in normal use (read from token validation). Only for the CLI mock. |
| `BroadcasterIdOverride` | `""` | Same — leave empty; only for the CLI mock. |

---

## 5. Getting an OAuth token

You only need a token if you want the bot to **talk in chat** (`AnnounceInChat`) or to run
**native polls** (`UseTwitchPolls`). Pure bubbles + chat voting need **no token at all**.

Whatever route you use, paste the result into `token.txt` **without** any leading `oauth:`
part, and set `Nick` to the account the token belongs to.

**Scopes you need:**

- Announcing in chat → `chat:read` + `chat:edit`.
- Native Twitch polls → `channel:read:polls` + `channel:manage:polls` (and the account must
  be Affiliate or Partner — Twitch only allows polls on those channels).

### Option A — one-click authorize link

Just like the original mod: click a link, approve on Twitch, and a small page shows your
token to copy. Pick the one matching what you want to do:

[![Register token — chat voting](https://img.shields.io/badge/Register%20token-Chat%20voting-9146FF?style=for-the-badge&logo=twitch&logoColor=white)](https://id.twitch.tv/oauth2/authorize?client_id=npyqq5754vpi2lqs83jhirg193n7pz&redirect_uri=https://erotel.github.io/TwitchColony/login.html&response_type=token&scope=chat:read+chat:edit)

&nbsp;→ scopes `chat:read` + `chat:edit`. Use this for chat bubbles that talk back and for
`AnnounceInChat`.

[![Register token — Twitch polls](https://img.shields.io/badge/Register%20token-Twitch%20polls-9146FF?style=for-the-badge&logo=twitch&logoColor=white)](https://id.twitch.tv/oauth2/authorize?client_id=npyqq5754vpi2lqs83jhirg193n7pz&redirect_uri=https://erotel.github.io/TwitchColony/login.html&response_type=token&scope=chat:read+chat:edit+channel:read:polls+channel:manage:polls)

&nbsp;→ adds `channel:read:polls` + `channel:manage:polls` on top. Use this to run **native
Twitch polls** (`UseTwitchPolls`, Affiliate/Partner only). Covers chat too.

> **Getting the poll token ≠ being allowed to run polls.** Twitch grants the poll *scopes*
> to **any** account, so this link succeeds even for a regular streamer — but the actual
> "create poll" API call only works on an **Affiliate or Partner** channel. What decides it
> is your **account status, not the token**: an Affiliate/Partner token runs polls fine; a
> non-Affiliate token authorizes but Twitch rejects the poll with `403 Forbidden` ("channel
> is not a partner or affiliate"), which the mod logs to `Player.log`. Until you're
> Affiliate, use chat voting (`UseTwitchPolls: false`) — it works for everyone.

After you click **Authorize**, Twitch sends you to the mod's login page, which shows the
token — copy it into `token.txt` (no `oauth:` prefix), and set `Nick` to the account you
authorized with. The link is permanent; reuse it whenever you need a fresh token.

<details>
<summary><strong>Running your own fork?</strong> Build your own link (5 min setup).</summary>

The buttons above carry this repo's Twitch `client_id` and point at its GitHub Pages login
page. If you fork the project, register your own free app so the redirect matches your Pages
URL:

1. **Host the login page.** Enable GitHub Pages on your fork (Settings → Pages → *Deploy
   from a branch* → `main` / `/docs`). It serves [`docs/login.html`](docs/login.html) at
   `https://<your-github-name>.github.io/<repo>/login.html`.
2. **Register a Twitch app** at the
   [Developer Console](https://dev.twitch.tv/console/apps) → *Register Your Application*.
   Set **OAuth Redirect URLs** to that exact Pages URL (the app name may **not** contain the
   word "Twitch"). Copy the **Client ID**.
3. **Assemble the link** — replace `YOUR_CLIENT_ID` and the redirect host:

   ```
   https://id.twitch.tv/oauth2/authorize?client_id=YOUR_CLIENT_ID&redirect_uri=https://YOUR_NAME.github.io/YOUR_REPO/login.html&response_type=token&scope=chat:read+chat:edit+channel:read:polls+channel:manage:polls
   ```

   The `redirect_uri` must match the registered Redirect URL **character for character**, or
   Twitch rejects it. (The `client_id` is public — safe to commit; only the *Client Secret*,
   which this flow never uses, stays private.)

</details>

### Option B — no setup, use a token generator

If you don't want to register an app, use a reputable third-party generator that hosts its
own app and lets you pick scopes (for example **twitchtokengenerator.com**). Select the
scopes listed above, authorize, and copy the token. Simplest to start with; the downside is
you're trusting a third party with the authorization step.

> **Keep the token secret** — anyone with it can post as your bot. If it leaks, revoke it in
> your Twitch **Connections** settings and generate a new one. Tokens also **expire**: if
> chat messages or polls suddenly stop working, regenerate and update `config.json`.

---

## 6. Starting a vote & how voting works

1. Load a colony with the mod enabled.
2. Open the **pause menu** (Esc). Click **"Start Twitch Votes"**.
3. The first vote opens for `VotingSeconds`. Viewers vote in chat (`!vote N`) or in the
   native Twitch poll. An on-screen panel shows the options, the running tally, and a
   countdown.
4. When time's up the winning event fires and a green **WINNER** banner shows briefly.
5. After that, votes **auto-restart** every `VoteDelay` seconds — you only click the button
   once per session. The delay countdown freezes whenever you pause the game.

If two options tie (or a native poll returns no votes, e.g. the mock), the first option
wins.

---

## 7. Troubleshooting

Everything the mod does is logged with the tag **`[TwitchColony]`** in:

```
Documents\Klei\OxygenNotIncluded\Player.log
```

Open that file (or search it for `[TwitchColony]`) first — it usually says exactly what's
wrong.

| Symptom | Likely cause / fix |
|---|---|
| No `[TwitchColony]` lines at all | Mod not enabled, or you didn't load into a colony yet. Re-check the Mods menu. |
| "Config loaded" but nothing happens | `Channel` is empty. Set it to your channel login (lowercase). |
| Config warning / "using defaults" | `config.json` has a syntax error (often a missing comma). Your file is left untouched — fix the JSON, or delete it and restart to regenerate. The log line names the exact path. |
| Settings gear icon missing | The mod didn't finish loading — check `Player.log` for `[TwitchColony]` errors. |
| Changed Channel/Nick/token, nothing happened | Those three apply when the chat connection opens. Reload the colony. |
| Bubbles never show | `EnableBubbles` off; message didn't start with `BubblePrefix`; or no live dupe matches the chatter's nick. |
| Bot doesn't announce in chat | `AnnounceInChat` needs `Nick` + a token with `chat:edit`. Anonymous connections can read but not send. |
| Native poll never opens | Not Affiliate/Partner, missing `channel:manage:polls`/`channel:read:polls`, or expired token. Check the log for the Helix error. |
| Native poll offers wrong number of options | `OptionsPerVote` must be 2–5 for polls. |
| Vote never auto-restarts | The game is paused — the `VoteDelay` countdown freezes while paused. Unpause. |

Still stuck? Grab the relevant `[TwitchColony]` lines from `Player.log` and open an issue.
