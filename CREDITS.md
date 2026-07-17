# Credits

**Twitch Colony** is an independent mod, released under the **MIT licence** (see
[LICENSE](LICENSE)) — use it, fork it, merge the modding API into your own mod, just keep the
copyright notice.

It was **inspired by** the original [Twitch Integration](https://github.com/asquared31415/ONITwitch)
mod by **asquared31415**, which pioneered Twitch-driven events in Oxygen Not Included.

This project does **not** reuse the original mod's source code or assets. Its code,
architecture, class layout, and any assets are our own, written from scratch. The
original is credited here as the inspiration and out of respect for that work.

If you enjoyed the concept, please also check out and support the original author.

## Bundled third-party code

**PLib** by **Peter Han** — <https://github.com/peterhaneve/ONIMods> — powers the in-game
settings screen. PLib is merged into `TwitchColony.dll` (ILRepack), so the mod ships a copy
of it. PLib is licensed under the **MIT License**, reproduced below as required:

```
MIT License

Copyright 2025 Peter Han

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the "Software"), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

## Thanks

- **Sgt_Imalas** — for the review that shaped the modding API. The shared-types-plus-reflection
  design (and the merge-lib that goes with it) is the one he arrived at building the mod API for
  the [Oni-Together](https://github.com/Lyraedan/Oxygen_Not_Included_Together) multiplayer mod, and
  he explained why our first attempt could never have worked from another assembly. He also spotted
  that we were calling `PatchAll` twice.

## Contributors

- **Martin Zatloukal** — author, design, and testing.
- **Claude** (Anthropic's Claude Code, Opus 4.8) — implementation.
