# CodeKeys — Dev Notes (resume point)

Last updated: 2026-05-30

## Where we are
Working app, system-wide. Builds clean, **59/59 unit tests pass**.

- **Build/test (PowerShell):** refresh PATH from Machine+User first, then
  `dotnet build CodeKeys.sln -c Debug` / `dotnet test CodeKeys.sln -c Debug`.
  **Kill any running CodeKeys.exe before building** (it locks Core.dll).
- **Run:** `src/CodeKeys.App/bin/Debug/net8.0-windows/win-x64/CodeKeys.exe`
- Git commit messages: use `git commit -F <file>` (inner quotes break PowerShell
  here-strings). Inline author:
  `git -c user.email=276664011+michaelnocito@users.noreply.github.com -c user.name="Michael Nocito"`

## Done (build steps 1–4 + sound direction)
1. Repo scaffold, README (privacy callout), ROADMAP.
2. Audio engine: WASAPI shared (WaveOut fallback), polyphonic mixer (16-voice
   cap), master volume/mute. Ambient bed layer — **PARKED** (Mike: "blows").
3. Spatial key map, procedural synth, click-free envelopes.
4. **Global WH_KEYBOARD_LL hook** — sounds in any app. MainWindow control panel.
5. **Presets** (research-grounded): live dropdown switching, lazy baking.
   **9 presets** (dropdown). **Midnight** (default) = deep-beat blend: per-key
   pitch zones via KeyVoiceSet.BakeNotes — deep kick thumps low, tap pops mid,
   smooth warm synth on top keys, snare on Enter. Others: Pulse, Thock, Keyboard,
   After Dark (dark pluck / "Starboy"), Electric (guitar), Grand Piano, Rhodes,
   Marimba. (Neon Nights removed per Mike; CreateSuperSaw kept in InstrumentFactory.)
   Synthesis in Core/Audio: Synth/Percussion/String/Instrument factories.
   Preset table + sources: `docs/sound-design.md`.

## Generative beat system (NEW — module 2 of 3 done)
- **Brain ported to C#** (`Core/Beat`): `Signals` → `SignalsToBeat.Of` → `BeatSpec`
  (+ `Evolve`). Pure, deterministic (FNV-1a + mulberry32 bit-for-bit from the TS
  original), 17 tests. `BeatSpec.Scale`/`Root` = single tonal source of truth;
  bridge `SignalsToBeat.ToScale`/`RootMidi` → Music types. Dorian scale added.
- **Module 1 (capture → Signals)** and **Module 3 (renderer / "BeatEngine")** still
  to do. **OPEN DECISION:** renderer stack. The TS directive says Tone.js (web),
  BUT a browser CANNOT capture system-wide keystrokes — that's CodeKeys' whole
  point — so the renderer must be **native NAudio** to keep the system-wide app.
  (Tone.js only fits if pivoting to an in-page web typing-toy.) Awaiting Mike.

## Open / next (in rough priority)
- **Tune the low-beat presets by ear** (Mike used "works for now" — revisit when
  he has tuning notes: punch, pitch, length, attack tick). Names are placeholders.
- **Persistence (JSON in %APPDATA%\CodeKeys)** so settings + selected preset
  survive restart, and **"save custom preset"** becomes possible. (Roadmap step 7.)
- **Pack/manifest folder system** — presets as editable folders. (Roadmap step 5.)
- **Tray shell** — toggles, master volume, **global mute hotkey (panic kill)**,
  pack picker, autostart toggle (off by default), About (build stamp + privacy).
  (Step 6.) Right now you quit by closing the window; no mute hotkey yet.
- **Publish** self-contained single-file .exe. (Step 8.)

## Architecture quick map
- `CodeKeys.Core` (pure, net8.0): Music, Input (SpatialKeyMap, KeystrokeController),
  Audio (Synth/Percussion factories, KeyVoiceSet, IVoicePlayer), Presets.
- `CodeKeys.App` (net8.0-windows, NAudio, WinForms): AudioEngine, GlobalKeyboardHook,
  MainWindow.
- `CodeKeys.Tests` (xUnit): 59 tests over the Core logic.
