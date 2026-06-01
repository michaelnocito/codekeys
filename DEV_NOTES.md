# Bowl Bass Keys — Dev Notes (resume point)

(Repo is still named `codekeys` for git history continuity; the app rebranded
to **Bowl Bass Keys**.)

Last updated: 2026-06-01

## Where we are
Working app, system-wide. Builds clean, **199/199 unit tests pass**.
Latest commit: Chakra Sweep template (21-min guided journey, bowl walks Root→Crown).

## CURRENT FOCUS (2026-06-01) — refresh this every session
The app is **Bowl Bass Keys**. UI redesigned to match
[michaelnocito.github.io](https://michaelnocito.github.io) — white BG, charcoal
text, generous whitespace, Segoe UI Semibold title, small uppercase gray section
labels, two thin hr panels. Form is 500×480.

**Keystrokes**: locked to Midnight (no picker). PresetLibrary.All exposes only
Midnight; other voicings dormant in code.

**Beat templates (10, exposed in picker)** — all share one musical pattern, only
the bowl Hz + tempo differ per template:
- Tibetan Beat (Focused; Dorian D3; 60-72 BPM)
- Root chakra · 396 Hz (MajorPentatonic D3; 54-66 BPM; **1.25× bass boost**)
- Sacral · 417, Solar Plexus · 528, Heart · 639, Throat · 741,
  Third Eye · 852, Crown · 963 (MajorPentatonic D3; 60-72 BPM each)
- **Space Clearing · 432 Hz** (MajorPentatonic D3; 72-84 BPM faster)
- **Chakra Sweep · 21 min** (NEW; see section below) — the bowl walks UP the seven
  chakras, 3 min each (Root→Crown), over a steady (non-breathing) bed.

**Musical bass = I-I-V-I** over 4 bars: long-sustain bass on every bar start,
perfect 5th on bar 2 (V), soft half-bar fill on bar 2 leading into the V.
Perfect 5th is computed by interval (rootMidi + 7) so it works in any scale
(Dorian's perfect 5th = degree 4, MajorPentatonic's perfect 5th = degree 3 —
the interval-based approach sidesteps the scale-degree gotcha that bit us once).

**Bowls = quiet background appearances** (Mike: "bowls are background, bass is
the star"):
- Voice: 10 s buffer; ADSR with sustain plateau (1.5 s ascend, 3.5 s sustain,
  ~5 s slow descend). Warble nearly off (detuneHz 0.08, shimmer 0.10, primary
  0.90). decayMultiplier 0.04. FadeOutTail 300 ms.
- Pattern: spacing = max(1, round(3 − 2.5 × density)) loops between strikes —
  calm = every 3 loops, medium = 2, dense = every loop.
- Hit gain 0.05 (primary), normalize 0.85 — bowl total summed signal sits well
  below the bass even with overlap.

**Conductor = RIDE-ALONG** (replaced the older counter-active design). Three zones:
- Calm (arousal ≤ FlowCenter 0.5): hold at FlowCenter, don't slow them.
- Flow (0.5 .. TenseThreshold 0.75): RIDE ALONG at RideGain 0.40 of deviation —
  beat rises with user but stays calmer.
- Tense (> 0.75): counter-active at 2 × LeadGain 0.40 of overage — only here
  does the beat actively bring the user back down.
All gated by the build envelope + responsiveness ramp, so during the first few
minutes adaptation barely applies.

**Audio levels**: `_keysLevel` 0.11, `_bedLevel` 0.22. Moving the beat slider
auto-sets keys to half. Manual keys-slider moves stay until next beat-slider move.

**Startup defaults** (Mike's test baseline): Beat ON, Root chakra selected, Demo
OFF, keys slider 11, beat slider 22.

## Chakra Sweep · 21 min (2026-06-01) — NEW guided journey
A single picker template that walks the singing bowl UP the seven chakras, 3 min
each (Root→Sacral→SolarPlexus→Heart→Throat→ThirdEye→Crown), 21 min total, then
holds on Crown. Same bass+bowl bed as a single chakra — only the bowl Hz changes
over time. Implementation:
- `BeatPreset.ChakraSweep` (new enum value). Preset range = 60-72 BPM, MajorPentatonic
  D3 — same calm bed as the chakra templates.
- `SignalsToBeat`: `ChakraSweepStages` (the 7 chakras in order), `ChakraSweepStageSeconds`
  (180), `ChakraSweepTotalSeconds` (1260), `ChakraSweepStageAt(elapsed)` → the chakra
  whose bowl is active at that time (clamps to Crown after 21 min). `ChakraBowlFreq(ChakraSweep)`
  returns 396 (the opening Root bowl) so the sweep stays on the shared "musical bass +
  bowl from t=0" code path; the LIVE bowl is selected per-stage by the sequencer.
- **Steady, NOT breathing**: the sweep uses `Conductor.SweepEnvelope` (ease-in over
  `SweepRiseSeconds` 60 s to a held `SweepPlateau` 0.72) instead of the 18-min
  rise/fall `CycleEnvelope`. Reason: the breathing build would leave the early
  chakras near-silent and fade the late ones out right as you reach Crown. `Conductor.Step`
  gained a `buildOverride` param (default -1 = breathing, unchanged for all other templates).
- **Rendering**: `BeatSequencer` pre-bakes all 7 chakra bowls up front; `UpdateSweepBowl(elapsed)`
  sets `_sweepBowlMidi` per stage at each loop boundary; `BeatPattern.Build` gained a
  `bowlMidiOverride` param (default -1 = unchanged) used to strike the current stage's bowl.
  `BeatSequencer.BuildAt(preset, elapsed)` picks Sweep vs breathing envelope.
- **Demo toggle** compresses the sweep too (elapsed × TimeScale), so the whole 21-min
  walk auditions in ~63 s with Demo on.
- 22 new tests (`ChakraSweepTests`). 199/199 pass.
- ⚠️ **Awaiting Mike's ear test.** Open tweaks if he flags: per-stage Root bass boost
  (currently NOT applied during the Root stage — bass is uniform across the sweep), the
  `SweepPlateau` level, `SweepRiseSeconds`, whether a soft crossfade/announce is wanted at
  each 3-min boundary, and whether the steady-vs-breathing call was right.

## ⚠️ Next session — open items
- After Mike ear-tests Root + propagated templates, may need to tweak any of:
  bowl spacing const (`3.0 − 2.5 × density`), Root bass boost (1.25×), per-chakra
  BPM (currently all 60-72 except Root 54-66 and Space Clearing 72-84), V-chord
  cadence (currently only on bar 2 of 4-bar loops).
- The 432 Hz bowl in Space Clearing is harmonically very close to (but slightly
  flat of) A4 = 6th harmonic of D2 bass — may sound subtly out of tune. If Mike
  flags it, retune Space Clearing bass root to ~72 Hz for exact harmonic, or use
  a different scale.
- App rename in process: window title + heading + `BuildInfo.Full` all say
  "Bowl Bass Keys"; repo and .exe filename still `CodeKeys` (intentional, for
  git history continuity — don't rename without explicit ask).

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

### Bass + drums "blanket" pivot (2026-05-31) — IMPORTANT
Mike: the high tones (xylophone/marimba, chime, and the high melody) kept pulling
his focus. Wants a warm low **blanket, not a leash** — "warmer when needed, lighter
when cooling off." Key insight: that warmth behavior is ALREADY the conductor
(density rises when under-stimulated, falls when over → counter-active). So this was
an **instrument change**, not a behavior change:
- New **`BeatLayer.Bass`**: warm low WarmPad body at the root register (root + an
  occasional fifth), density-scaled fullness ("warmer" = more bass). Baked in
  `BeatSequencer`; pattern block in `BeatPattern`.
- **Conductor now activates only Bass** (from the Statement phase) + the base
  Pad/Pulse(kick)/Ghost(soft tap). **Melody, Marimba, Chime are filtered OUT** of
  both the arc (`Step`) and `BuildupSpec` — no high tones. Their code/voices remain
  (dormant), so they're easy to bring back if Mike wants.
- So the active bed = **Pad (warmth) + Pulse/Ghost (drums) + Bass (body)**.

### Silence-when-Beat-on fix (2026-05-31)
Mike: turned Beat on and heard nothing. Root cause: the audio thread keeps calling
BeatSequencer.Read() while Beat is muted, so `_sessionSamples` advances unattended.
If you wait minutes after launch before enabling Beat, the cycle is already
mid-fall or past CycleSeconds → bed is silent on toggle. Compounded by the new
softer Sub voice + the very low `_buildGain` floor (0.06) + low `_bedLevel`
(0.16) all stacking → effectively inaudible. Fixes:
- **MainWindow now calls `_beat.Reset()` whenever Beat is toggled ON** — cycle
  restarts from silence reliably.
- **`_buildGain` floor 0.06 → 0.25** so the bed isn't ducked to inaudible during
  the early build.
- **Bar-start kick floor 0.05 → 0.15** so even intensity=0 has a perceptible
  heartbeat (~1 per loop).
- **First-kick-ever guarantee** (`cycle == 0 && s == 0`) so Beat-on plays a
  confirmation hit immediately, regardless of intensity.
- **`_bedLevel` 0.16 → 0.22** (softer pulse needed a touch more presence).
167 tests pass.

### Breathing cycle + Reset button + softer pulse (2026-05-31)
Mike: (1) after the peak, fall back to silence at a rate **25% faster than the
rise**, then repeat the pattern; (2) needs a UI **Reset** to restart the beat
without toggling Beat off/on; (3) the driving kick was "acting like a melody" —
needs less harsh, more sustain, more atmospheric. Changes:
- **`Conductor.CycleEnvelope(t)`** wraps `BuildupEnvelope` as the rise (p², 10 min)
  + a mirrored ease-out fall ((1-p)², 8 min) — `FallSeconds = BuildupSeconds /
  FallSpeedFactor (1.25)`. Total cycle = `CycleSeconds` (18 min). Loops forever
  (modulo CycleSeconds). `Step` + `BeatSequencer` now use `CycleEnvelope` so
  voices/density/arousal-gating breathe in and out together.
- **`BeatSequencer.Reset()`** + a **"↺ Reset beat"** Button in MainWindow that
  restarts the cycle from silence (no rebake, no toggle dance).
- **Pulse voice** swapped from `CreateKick` (pitch-drop thump + click) → 
  `CreateSub(decay 0.55s, gain 0.55)` — a pure low sine with a soft attack and
  long sustain. Reads as a warm "hummmm" rather than a driving kick — less
  melodic-feeling, more atmospheric.

### Additive build is now the DEFAULT (2026-05-31) — fixed dom-dom-dom regression
Mike: it was hitting "dom dom dom dom" from t=0 instead of being "almost not
noticeable" like the original Buildup spec. Root cause: when the Buildup toggle
was removed from the UI in the declutter pass, the build behaviour wasn't wired
in as the DEFAULT — Beat-on was going straight to full kick + half-bar bass.
Researched: Steve Reich/Drumming + West African drum-circle = "additive
minimalism" (voices enter one at a time, each repeats — the "people in public
adding to a beat" metaphor exactly). Rewrote so:
- **`BuildupEnvelope` is now ease-in (p²)** instead of smoothstep — at 1 min in
  = 0.0028 (≈0.3% — truly imperceptible); at 5 min in = 0.25; reaches 1.0 at
  `BuildupSeconds`=600s (10 min).
- **`Conductor.Step` ALWAYS runs the build**: density scales with envelope,
  voices enter ONE AT A TIME at envelope thresholds (Pulse always = the lone
  tapper; Ghost > 0.20 ≈ 4.5 min; Bass > 0.40 ≈ 6.3 min; Splash > 0.70 ≈ 8.4 min).
  Arousal response is also gated by the envelope — thermostat barely moves during
  the build, fades in as it assembles.
- **`BeatPattern` bar-start kicks are now probabilistic too** at low intensity
  (was unconditional — the actual source of "dom dom dom"). At intensity = 0.01,
  bar-start prob ≈ 0.06 → about one kick per minute. At intensity = 1.0 short
  circuit keeps byte-identical default behaviour.
- **`ArousalMin` floor is now scaled by the build** so it doesn't snap the tempo
  up during the silent start.
- **`_buildGain` output crescendo (0.06 → 1.0)** applied always (was opt-in).
- **`BuildupSpec`, `Buildup` property removed** — subsumed into Step.
- Focused preset base trimmed to just `Pulse`.
- All this means Demo (TimeScale=20) still works to audition the 10-min build in
  ~30s.

### Deep bass + splashes + declutter (2026-05-31) — refines the above
Mike: drop the bass an octave + long resonance ("booooommm"); the pad sounded like
a low guitar-strum chord → **remove it**; keep only low bass + drum hits driving;
add *rare* subtle "splash" appearances for variety but NOT instruments riding the
beat; he fixates on the **highest treble** (researched: high/bright timbre + sharp
transients capture attention involuntarily — `docs/sound-design.md`). Changes:
- **Bass**: now `root-12` (~73 Hz, above the ~45 Hz floor), **pure sine, long decay
  (Decay 1.8s)** = deep resonant boom; on the half-bar (steady driver). It's part of
  the Focused preset base now (always on with the kick).
- **Pad removed** from the active bed (conductor filters it; code dormant).
- **New `BeatLayer.Splash`**: a rare (~22%/loop), soft, **dark mid-low** one-off
  (WarmPad, slow attack, no sharp transient, never high) — an appearance, not a
  layer. Conductor adds it from the Statement phase / buildup e>0.5.
- **UI decluttered**: removed the Mood picker (locked to Focused), Reactivity slider,
  and Buildup toggle from the screen (properties/code kept — easy to restore). Kept
  Sound picker, Keystrokes/Beat toggles, the Demo toggle, volume hint.
- Active bed now: **deep Bass + Pulse/Ghost drums**, with occasional Splashes. No
  Pad, no high tones. Warmth still scales via drum density (conductor).

### NEXT (after Mike's ear test)
- **Re-tune by ear** the consts in `Conductor.cs`; `_keysLevel`/`_bedLevel` in
  `AudioEngine.cs`. Bass knobs: register (`DegreeToMidi(root, …)` — go root-12 for
  deeper if his speakers handle it), fill prob `Density*0.5`, bake gain 0.50. Buildup knobs:
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
