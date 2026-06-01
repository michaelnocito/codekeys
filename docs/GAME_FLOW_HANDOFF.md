# Handoff: Adaptive "flow" music + beat-responsive play for a controller game

*From the Bowl Bass Keys project → to one of Mike's Godot game projects (e.g. Inconnu Heretic /
Camelot Hills / Dance Combat Lab). Goal: reuse the adaptive-music engine and structure we built
so that gameplay — even on a controller — feels responsive **with** the beat, and the music
**pulls the player into flow fast.***

Prepared 2026-06-01.

---

## 0. The ask, in one line

Take the adaptive generative-music engine from Bowl Bass Keys and bring it into the game so the
**music reacts to play and play reacts to the music** — but re-tuned from *calm/new-age/
relaxing* to **energetic: get the player into the video-game flow sweet spot quickly and keep
them there.** This is a tempo/intensity/instrument re-skin of the same machinery, **not** a new
system.

> **Tone note from Mike:** the keyboard app is intentionally relaxing (singing bowls, slow
> 10-minute breathing builds). For the game, drop the new-age calm. We want fast onset, drive,
> and momentum — the "in the zone" arcade/action feeling. Keep the *adaptive structure*, change
> the *character and the speed*.

---

## 1. What Bowl Bass Keys is (context for the game's architect)

A Windows app that turns **typing behavior** into adaptive generative music. The important part
for you is the **pure, deterministic, engine-agnostic core** — it has no audio or OS
dependencies and is fully unit-tested (207 tests). It's a clean pipeline:

```
raw input events ──▶ Signals (privacy-safe telemetry, a rolling window)
                        │
                        ▼
                 Conductor.Estimate(Signals) ──▶ "arousal" 0..1
                        │
                        ▼
        Conductor.Step(spec, arousal, elapsed, …) ──▶ evolved BeatSpec
                        │            (tempo, density, which voices are active)
                        ▼
        BeatPattern.Build(spec, cycle) ──▶ a one-loop timeline of hits
                        │
                        ▼
            renderer turns hits into sound (sample-clocked)
```

Plus a side channel — **LivingEventDetector** — that fires one-shot accents when the input's
*rate of change* spikes relative to its own recent variability (self-calibrating; adapted from
the PlantWave/MIDI-Sprout technique).

**Files to port (all pure C#, no NAudio/WinForms dependency — `CodeKeys.Core/Beat/`):**
- `Signals` / `SignalsToBeat` — telemetry → a musical `BeatSpec` (scale, root, tempo range, layers).
- `Conductor` — the brain: `Estimate` (input→arousal), `MusicalTarget` (ride-along/counter logic),
  `Step` (rate-limited evolution of tempo + density + active voices), the build/arc envelopes.
- `BeatPattern` — turns a `BeatSpec` into a deterministic per-loop list of hits (the part you'd
  feed to a Godot audio renderer).
- `Motif` / `MotifFactory` — a small in-key melodic cell engine (dormant in the calm app; useful
  for a game's melodic hooks).
- `LivingEvents` — the event/stinger detector.

The renderer (`CodeKeys.App/Audio/BeatSequencer.cs`) is NAudio-specific and would be
**re-implemented in Godot** (see §5) — but it's a thin sample-clock over `BeatPattern`'s output,
so it's easy to mirror.

Godot 4.x supports C#, so the core can be **dropped in nearly as-is** (strip the `CodeKeys.Core`
namespace, keep the classes). Precedent: the Dance Combat Lab rhythm overlay was already
cross-pollinated into Inconnu Heretic, so this team has done music↔gameplay coupling before.

---

## 2. The key re-mapping: typing telemetry → gameplay telemetry

`Signals` is just "what is the user doing, how fast, how erratically, how much struggle." Swap
the *sources* and the whole engine works unchanged. Suggested gameplay arousal inputs:

| Bowl Bass Keys signal | Game equivalent (pick what fits the genre) |
|---|---|
| typing speed | **action rate** — inputs/sec, attacks/sec, movement speed, APM |
| rhythm variance (erraticness) | **input j/combat irregularity** — dodge spam, panic mashing |
| backspaces (struggle) | **mistakes** — missed parries, taking hits, deaths, whiffed combos |
| (n/a) | **combat intensity** — enemies on screen, boss phase, damage taken/dealt |
| (n/a) | **progression** — objective proximity, chase active, timer pressure |

Compute a single **intensity 0..1** the way `Conductor.Estimate` blends its terms (weighted sum,
clamped), e.g. `0.5·actionRate + 0.2·combatPressure + 0.2·threat + 0.1·irregularity`. That feeds
`Conductor.Step` exactly as arousal does today.

**Controller specifically:** sample stick magnitude, trigger pressure, button cadence, and the
combat state machine — a controller emits a rich, continuous intensity signal (often *better*
than keystrokes). Add **haptic pulses on the beat** (rumble on downbeats / on-beat hits) so the
player physically feels the groove through the pad — this is a big part of "play feels responsive
with the beat."

---

## 3. Re-tuning calm → game-flow (the actual changes)

Same engine, different constants and instruments. Concretely:

**Speed & onset**
- **Fast onset, not a slow build.** The calm app eases in over ~10 minutes (`BuildupSeconds`
  600). For the game, collapse this to **seconds** (a 5–20 s ramp), or skip the build and start
  near full energy on encounter start. (We already proved a steady non-breathing envelope works —
  the Chakra Sweep uses `SweepEnvelope`: a quick ease-in to a held plateau. Use that pattern, but
  ~10× faster.)
- **No breathing rise/fall** during active play — hold energy up; let it drop only between
  encounters / on safe zones (use the cycle envelope only for downtime).

**Tempo & arousal targets**
- **Raise the tempo band** well above the calm 60–72 BPM: action games live around **120–160
  BPM** (combat), with calmer exploration maybe 90–110. Set per-state ranges.
- **Raise `FlowCenter`** and make the conductor **ride along harder** (bigger `RideGain`) so the
  music climbs *with* escalating action instead of holding back. Keep a `TenseThreshold` so it
  doesn't redline into noise, but the whole curve sits higher and hotter than the calm app.
- **Shorten `ResponsivenessFullAt`** (calm app fades adaptation in over 300 s) to a few seconds,
  and raise the slew rate so tempo/intensity track combat quickly. Keep *some* smoothing so it
  grooves rather than stutters.

**Instrumentation / character**
- **Drop the singing bowls and the slow bass-drone identity** (that's the calm app's brand).
  For the game use **driving drums, a punchy bassline, arps/leads, risers** — momentum
  instruments. Keep the *structural* tricks: quantize-to-scale (stays musical), additive layering
  (voices enter as intensity rises), motif for a hook.
- **Vertical layering** is your friend: pre-author stems (drums / bass / lead / tension) and have
  `Conductor.Step`'s "active layers" decide which stems are audible at the current intensity —
  exactly what `Step` already returns, just mapped to stems instead of synth voices.

**Beat-synced gameplay (the "play responsive with the beat" part)**
- Expose the engine's **beat clock** (it already sample-clocks loops at a known BPM) to the
  gameplay layer. Then:
  - **Quantize player feedback** (hit sparks, screen pulse, UI bumps, camera kick) to the beat
    grid so the world visibly pulses with the music.
  - Optionally **reward on-beat actions** (timing windows around each beat → bonus damage/score/
    style) — the rhythm-action sweet spot; this is the Dance Combat Lab DNA. Make it a *bonus*,
    not a *requirement*, for a non-rhythm game so it doesn't punish.
  - **Stingers in key:** route gameplay events (kill, combo milestone, low-health, boss intro)
    through the `LivingEventDetector`/auditory-icon pattern so they fire short musical hits
    **quantized to the next beat and in the current scale** — they feel composed, not bolted on.

---

## 4. The science (why this gets players into flow fast)

- **Flow theory (Csikszentmihalyi):** flow lives in the channel where **challenge ≈ skill**.
  Adaptive music is a cheap, continuous signal of that balance — rising music = rising challenge,
  which both *reflects* and *nudges* the player's arousal toward the zone.
- **Yerkes–Dodson:** there's an optimal arousal for performance — but for **fast action tasks the
  optimum is HIGHER** than for calm/precision tasks. That's the whole reason to retune upward: the
  calm app aims for a low-arousal focus plateau; the game should aim for an energized one.
- **Iso principle:** meet the player where they are, then lead. The ride-along conductor already
  does this — start near current intensity, then pull toward the target zone.
- **Parameter-mapping sonification + quantize-to-scale** (the *Sonification Handbook* methods we
  built on): map continuous gameplay values to musical parameters but snap to a scale so it stays
  musical at any intensity. Reuse directly.
- **Dynamic/adaptive game-music craft:** vertical *layering* (add/remove stems by intensity) and
  horizontal *re-sequencing* (swap sections at musical boundaries), à la LucasArts iMUSE and
  modern middleware (Wwise/FMOD). `Conductor.Step` is effectively a layering controller; pair it
  with beat-aligned transitions for re-sequencing.
- **Beat entrainment of action:** syncing visual/haptic feedback and reward windows to a steady
  pulse leverages sensorimotor synchronization — players naturally lock to the beat, which raises
  engagement and the feeling of mastery. (Stay honest: this is engagement/feel, **not** any
  medical/heart-rate claim — same honesty line as the parent project.)

---

## 5. Suggested implementation path in Godot

1. **Port the core.** Copy `CodeKeys.Core/Beat/{Signals, SignalsToBeat, Conductor, BeatPattern,
   Motif, LivingEvents}.cs` into the Godot C# project; rename namespace. It compiles standalone
   (no NAudio/WinForms). Bring the unit tests too — they're engine-agnostic and lock in behavior.
2. **Feed it gameplay signals.** A `MusicDirector` node samples the controller + combat state each
   frame, builds a `Signals`-equivalent over a short rolling window, and calls
   `Conductor.Estimate` → `Conductor.Step` on each loop boundary (or each bar).
3. **Render with stems, not synthesis.** Instead of the NAudio sample-synth renderer, drive
   pre-authored **stem `AudioStreamPlayer`s** (drums/bass/lead/tension). Map `BeatSpec.Layers` →
   which stems are at full volume; crossfade on bar boundaries. Use one master tempo clock so all
   stems + the gameplay beat-grid share the same time base. (If you want fully generative drums
   like the app, mirror `BeatSequencer`'s sample-clock with an `AudioStreamGenerator` — more work;
   stems are faster to ship and sound more "produced.")
4. **Publish the beat clock** to gameplay (signal on each beat/bar) for pulses, reward windows,
   and haptics. Add controller rumble on downbeats / on-beat hits.
5. **Re-tune the constants** per §3 (tempo bands, FlowCenter/RideGain, responsiveness, build
   length) and A/B by feel. Keep them as exposed consts like the app does, so they're ear-tunable.

---

## 6. Open questions for the game's architect

1. **Which project / genre?** (Inconnu Heretic action-RPG vs Camelot Hills vs Dance Combat Lab —
   the tuning and whether on-beat *rewards* are core vs flavor depends heavily on this.)
2. **Generative or stems?** Pre-authored stems (fast, polished, less adaptive) vs fully
   generative like the app (more adaptive, more work, riskier to make sound "produced"). Hybrid is
   likely best: generative drums + authored melodic stems.
3. **How beat-coupled should *play* be?** Cosmetic (world pulses to music) → soft (on-beat bonus)
   → hard (rhythm-gated actions). Pick a point on that spectrum up front.
4. **Middleware?** Native Godot audio vs FMOD/Wwise integration (both have strong adaptive-music
   tooling that pairs well with the `Conductor`-as-controller approach).
5. **Encounter model** for energy: where does intensity rise/fall (combat start/end, boss phases,
   chases, safe rooms)? That defines the envelope/state machine around `Conductor.Step`.

---

*Source engine, for reference: `C:\Users\Mike\Projects\codekeys`, `CodeKeys.Core/Beat/`. The
calm-app tuning rationale and citations are in `docs/sound-design.md`; the PlantWave-derived
event technique is documented in `docs/LAUNCH_BRIEF.md` §3 and `DEV_NOTES.md`.*
