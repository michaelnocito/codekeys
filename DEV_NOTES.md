# CodeKeys — Dev Notes (resume point)

Last updated: 2026-05-30

## Where we are
Working app, system-wide. Builds clean, **151/151 unit tests pass**.

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
## Adaptive conductor DONE (2026-05-30) — the headline feature
Mike's direction: generate music that keeps him in flow — type faster → gently
calm; slower → gently energize; changes must be SLOW (he flagged "too fast").
Research-grounded (iso principle + Yerkes-Dodson; HR-entrainment is weak so no
physiological claims). Roadmap "v2 adaptive engine" section has the full spec.
- **`Core/Beat/Conductor.cs`** (pure, deterministic, 24 tests):
  - `Estimate(Signals)→arousal 0..1` = 0.55·speed + 0.25·erraticness +
    0.20·struggle(backspaces); idle reads 0.25.
  - `MusicalTarget(a)` = **counter-active** reflection about `FlowCenter` (0.6):
    over-aroused → aim lower (settle), under → aim higher (activate).
  - `Step(spec, arousal, elapsed, dt, lo, hi)` rate-limits arousal to
    `SlewPerSec`(0.006)/s (≈a minute end-to-end → gentle), maps it to bpm+density
    within the preset range, and runs the **session arc** by elapsed time:
    Establish 0–2m (pad+pulse, sparse) → Statement 2–6m (melody enters) →
    Development 6–12m (marimba joins) → Flow 12m+ (sustain). Preserves
    scale/root/preset/loopBars → renderer never rebakes.
  - Tunables are consts at the top of the file (tune by ear).
- **`BeatSequencer`** rewired: `UpdateGroove`→**`Observe(arousal)`** (just stores
  the latest arousal); at each loop boundary it calls `Conductor.Step` (replaces
  the old random `Evolve`). **Session clock `_sessionSamples` only resets on
  `SetSpec` (mood change)** — fixed the bug where every 3s typing snapshot reset
  the arc. `SetSpec` normalizes the opening to the sparse Establish phase so a new
  mood eases in. Bank still pre-bakes every voice so a layer entering mid-session
  never synthesizes on the audio thread.
- **MainWindow**: the 3s timer now calls `_beat.Observe(Conductor.Estimate(snap))`.
- **Motif seed stabilized**: `motif|preset|scale|root` only (dropped bpm/loopBars)
  so the conductor's tempo drift can't scramble the tune.

### NEXT (after Mike's ear test)
- **Tune by ear**: FlowCenter, LeadGain, SlewPerSec, arc phase lengths, melody
  volume. Does "calm on speed-up / energize on slow-down" feel right & gentle?
- Motif *development* via transforms (invert/transpose) per arc phase — needs a
  `Development` field on BeatSpec; deferred to keep this change bounded.
- Resolution phase on idle (wind down when the user stops), not just by timer.
- Still-random **Marimba** noodle → make it support the motif.
- **Volume follows OS** ✅ DONE — removed the in-app slider; WASAPI shared mode
  means Windows' own volume mixer controls CodeKeys. Window shows a hint instead.

**Scope cuts (2026-05-30, per Mike):** ❌ Apple Watch, ❌ heart-rate / biofeedback,
❌ cross-platform — all dropped. Windows-only, keystroke-driven. Standing order:
follow my own recommendations unless Mike has overridden them.
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
- `CodeKeys.Tests` (xUnit): 151 tests over the Core logic.
