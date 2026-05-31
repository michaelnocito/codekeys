# CodeKeys — Dev Notes (resume point)

Last updated: 2026-05-30

## Where we are
Working app, system-wide. Builds clean, **127/127 unit tests pass**.

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
- **Module 3 (renderer) DONE — native NAudio** (Mike chose native over Tone.js).
  `Core/Beat/BeatPattern` (pure, tested) → hit timeline; `App/Audio/BeatSequencer`
  (ISampleProvider) bakes a scale voice-bank, sample-clocks the pattern, loops +
  `evolve()` each cycle, live `SetSpec`. Wired as the bed via
  `AudioEngine.SetBedProvider` at −12 dB (bedLevel 0.25). MainWindow: **Beat
  toggle + Mood dropdown** (Focused/Relaxed/Burnout/Silly). Brown-noise bed
  retired. Voices: pad/pulse/marimba/**melody**/ghost.

## Melody redesign — Phase 1 DONE (2026-05-30)
Mike's feedback: the beat "just plays a piano scale after a bit of typing" — it
needs variety, must not annoy, and should **introduce a melody that emerges over
~15–20 min** (NOT tied to his keystrokes — just emerges over the session).
- **Root cause:** the old `Arp` layer was a literal ascending scale
  (`degree = (s/2) % span`), switched on at 40 chars.
- **Fix (Phase 1):** new `Core/Beat/Motif.cs` — pure, deterministic motif engine.
  A `Motif` = one bar of scale-degree notes with rests (it breathes).
  `MotifFactory.Generate(seed, scaleDegrees)` grows a tune via weighted stepwise
  motion + tonic gravity + a resolving ending (not a scale run). Transforms:
  `Transpose` / `Invert` / `WithResolvedEnding` (raw material for Phase 2).
  `BeatLayer.Arp` renamed → **`Melody`**. `BeatPattern` lays the motif per bar as
  **antecedent/consequent** (even bars state it, odd bars answer it on the tonic).
  Motif is seeded from the spec's **stable identity** (preset/scale/root/bpm/
  loopBars) — NOT density/accents — so per-loop `Evolve` drift never scrambles the
  tune; it stays recognizable. 16 new tests (`MotifTests`).
- **NEXT — Phase 2 (session arc):** a pure `SessionArc` mapping elapsed minutes →
  phase (Establish→Statement→Development→Peak→Resolution) → target density/layers/
  variation intensity; make `Evolve` *develop* the motif (apply transforms) per
  phase instead of only jittering density+accents; **fix the `_cycle = 0` reset in
  `BeatSequencer.UpdateGroove`** so the session arc isn't restarted on every 3s
  typing snapshot. Then Phase 3 = ear-test + tuning.
  Also revisit the still-random **Marimba** noodle (make it support the motif).
- **Module 1 (live capture → Signals) DONE.** `Core/Input/KeyClassifier` (vk→KeyKind)
  + `Core/Beat/SignalsCollector` (rolling 12s window → Signals; records only timing
  + category + an upper/lower bit, **never the characters** — `Signals.Text` always
  empty). MainWindow feeds the hook into the collector and a 3s timer snapshots →
  `SignalsToBeat.Of` → `BeatSequencer.UpdateGroove` (applies at next loop boundary,
  no rebake when scale/root unchanged). So the beat now reacts to real typing
  speed/backspaces/punctuation. **All 3 beat modules complete.**

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
- `CodeKeys.Tests` (xUnit): 127 tests over the Core logic.
