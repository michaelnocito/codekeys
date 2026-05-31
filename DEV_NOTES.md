# CodeKeys — Dev Notes (resume point)

Last updated: 2026-05-31

## Where we are
Working app, system-wide. Builds clean, **157/157 unit tests pass**.
Latest commit: `d568ae2` (soft chime layer). The generative beat is the active
work area — see the "Adaptive conductor", tuning, and chime sections below.

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
  - `MusicalTarget(a)` = **counter-active** reflection about `FlowCenter` (0.5)
    with a **Deadband** (0.18): inside the band it doesn't steer at all (hold the
    pulse); past it, over-aroused → aim lower (settle), under → aim higher.
  - `Step(spec, arousal, elapsed, dt, lo, hi)` rate-limits arousal to
    `SlewPerSec`(0.004)/s, **scales the move by a responsiveness ramp**
    (`elapsed/ResponsivenessFullAt`, 300s) so adaptation fades IN from the base
    beat, maps it to bpm+density within the preset range, and runs the
    **session arc** by elapsed time:
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
- **Demo toggle** (`BeatSequencer.TimeScale`, UI "⚡ Demo build-up"): compresses the
  arc clock 20× (≈12-min build-up → ~36s) so it's auditionable; leaves the arousal
  ramp at real time so calm/energize still feels natural. Dev aid — likely hide
  before ship.
- **Motif seed stabilized**: `motif|preset|scale|root` only (dropped bpm/loopBars)
  so the conductor's tempo drift can't scramble the tune.

### Tuning pass — "more background, less reactive" (2026-05-30)
Mike: it drove to the forefront / competed with the work; sensitivity too high;
wanted a slow transition from base beat → responding; larger sample size; keep
the base tone. Applied:
- **Quieter / more space**: bed −12dB→−16dB (`_bedLevel` 0.25→0.16); melody voice
  softened (gentle 30ms attack + 0.7s tail, gain 0.45→0.30, WarmPad tone kept);
  motif now 3–5 notes (was 4–7) at lower gains (0.26–0.32); density ceiling cut
  (arcMult ≤0.9, density formula `0.28+0.42·m`, cap 0.85); kick click 0.06→0.04.
- **Calmer tempo**: Focused 72–84 → **60–72 BPM**.
- **Less reactive / only when sure**: `FlowCenter` 0.6→0.5, `LeadGain` 0.45→0.25,
  `SlewPerSec` 0.006→0.004, new **`Deadband` 0.18** (hold unless clearly drifted),
  new **responsiveness ramp** (`ResponsivenessFullAt` 300s) so it starts as just
  the base beat and slowly begins responding.
- **Larger sample**: `SignalsCollector` window 12s→**30s** + EMA smoothing (0.25)
  on the arousal estimate in `Observe`.

### Levels + flow anchor (2026-05-31, research-grounded)
Mike: keystrokes too loud; wants a research-backed resting/flow baseline that
adjustments always return toward. Findings + changes (sources in docs/sound-design.md):
- **Keystrokes lowered**: new `AudioEngine._keysLevel` 0.55 (was effectively 1.0),
  via a `VolumeSampleProvider` on the key mixer (+ `KeysLevel` property). Keys now
  sit ~11 dB over the bed — clear foreground feedback without the fatigue of
  too-loud incidental sound (raises cognitive load per the research).
- **Flow anchor**: 60–80 BPM is the research sweet spot for relaxed-focus. Focused
  is already 60–72 with `FlowCenter`→~66 BPM, and the conductor already homes back
  toward it → **left tempo unchanged** (changing it would be worse). Documented the
  anchor in `Conductor.FlowCenter` + sound-design.md. No brain-wave-entrainment claims.
- **Considered but deferred**: cognitive-load ducking (lower whole mix when working
  hard). Grounded but subtle — gated on an ear test.

### Back-beat variance (2026-05-31)
Mike wanted the backing beat to stop looping dead. `BeatPattern.Build` now takes a
`cycle` (loop index, threaded from `BeatSequencer._loopCount`, reset on SetSpec):
- rng seed includes `cycle` → off-beat kicks + marimba notes vary loop to loop;
- the quarter-note kick (downbeat) stays constant = the anchor pulse is never lost
  (`Quarter_Note_Pulse_Is_Steady_Across_Cycles` test);
- every other loop adds a soft pickup fill (kick on the last "and" + a ghost tick).
Busyness scales with `Density` (so arousal still modulates it). 3 new tests.

### Reactivity / sensitivity (2026-05-31)
Mike wanted it less gradual (+25%) and a user slider. Added a **`sensitivity`**
multiplier to `Conductor.Step` (scales `maxDelta`, i.e. how fast it moves toward
target each loop; 1 = baseline). `BeatSequencer.Sensitivity` property, default
**1.25** (the +25%). MainWindow **Reactivity slider** (0–100 → 0.5×–2.0×, default
mid = 1.25×) sets it live. Responsiveness fade-in + deadband still apply on top.
1 new test. (Note: "⚡ Demo build-up" = compresses the arc clock 20× for auditioning;
does NOT change reaction speed — that's what the Reactivity slider is for.)

### Chime / musicality layer (2026-05-31)
Mike wanted more musical variance — soft chimes/effects that increase or decrease
as needed. Added a new `BeatLayer.Chime`: sparse soft sine **bells** (custom long-
decay envelope, gain 0.4) on high chord tones (root+24, degrees 0/2/4 → always
consonant). Pattern: 8th-grid, prob `Density*0.10`, varies per loop (cycle seed),
hit gain 0.25. Conductor adds Chime from the **Statement** phase (with the melody);
because probability tracks Density, it naturally grows in fuller moments and thins
when calm. Baked in `BeatSequencer.BakeBank`. Added to FullSpec test + arc tests.

### Buildup mode (2026-05-31)
Mike wanted a separate setting: a slow ~10-min crescendo from almost-silent/ultra-
sparse to the full coherent beat — subtle, song-like, "barely noticeable at first."
- **`Conductor.BuildupEnvelope(elapsed)`** = smoothstep 0→1 over `BuildupSeconds`
  (600s). **`Conductor.BuildupSpec`** (pure, time-driven, ignores arousal): density
  0.04→0.7, tempo lifts gently, layers enter progressively (Pad>0.05, Melody>0.30,
  Chime>0.50, Marimba>0.65 of the envelope).
- **`BeatPattern.Build` gained an `intensity` (note-fill) param** (default 1.0 =
  no-op, byte-identical normal mode — guarded so it consumes no rng at 1.0). <1
  thins the kick (only bar-start kick early) + melody (notes fill in) → sparse→full.
- **`BeatSequencer.Buildup`** property (toggle; resets the clock). In buildup it
  uses `BuildupSpec`, rides an **output crescendo** (`_buildupGain` 0.05→1.0) and
  passes `_noteFill = envelope` to Build. Works with the Demo toggle (20×) to
  audition the whole 10-min arc in ~30s. UI: "🎚 Buildup (slow 10-min build)".
  Note: buildup bypasses the adaptive thermostat (Reactivity slider doesn't apply).

### NEXT (after Mike's ear test)
- **Re-tune by ear** the consts in `Conductor.cs`; `_keysLevel`/`_bedLevel` in
  `AudioEngine.cs`. Back-beat variance knobs: off-beat prob `Density*0.30`, fill
  cadence `cycle % 2`; chime density `Density*0.10` in `BeatPattern`. Buildup knobs:
  `BuildupSeconds`, the envelope curve, layer thresholds, `_buildupGain` floor.
  Could add more soft effects (rare shimmer/pad swell) — same pattern as Chime.
  Does it now feel like background that holds the pulse and only guides when sure?
- Possible deeper **voice enrichment** from the library (soft Rhodes/bells/richer
  pad) — held back because Mike likes the base tone; pick by ear next.
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
- `CodeKeys.Tests` (xUnit): 157 tests over the Core logic.
