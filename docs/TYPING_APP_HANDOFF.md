# Handoff: add Bowl Bass Keys' keystroke sound to the typing-drill app (basic integration)

*From the **Bowl Bass Keys** project → to Mike's "learn to type in a relaxed way, as a data
analyst" key-drill app. Goal: a small, supportive keystroke-sound layer that complements the
app's principle — **calm, low-stress, encouraging typing** — using only the ONE default sound
pack, adapted for a typing drill.*

> **Source for reference:** `C:\Users\Mike\Projects\codekeys`. The keystroke synthesis lives in
> `src/CodeKeys.Core/Audio/` and the default pack recipe is in
> `src/CodeKeys.Core/Presets/PresetLibrary.cs` → `DeepWarm()`. If you (the agent) can read that
> folder, use it; if not, everything you need is reproduced below — don't guess.

---

## 0. The ask, in one line

Bring in **just the per-keystroke sound** from Bowl Bass Keys — **not** the generative beat,
bowls, bass bed, or adaptive conductor. **Normalize to a single default pack** (no picker), and
**modify that one pack** so it fits a typing drill: gentle, relaxing, and clearly distinguishing
a correct keypress from a mistake without ever feeling punishing.

## 1. Scope — take this, leave that

**Take:** the keystroke sound layer only — one soft sound per key, played on each keypress.

**Do NOT take (out of scope for "basic"):** the generative beat bed, the singing bowls + bass,
the adaptive "conductor", the living-events channel, the template picker, the multiple voicing
packs. Those belong to the relaxation app. (An ambient bed could be a *later* optional toggle,
but keep it out of v1.)

## 2. "Normalize" — one default pack, always

Lock to the single default pack, **"Deep & Warm"** — no pack picker, no options. It's a low,
warm, percussive set chosen specifically because low/steady/percussive sound **habituates and
fades into the background** (so it supports the typing task instead of competing with it — see
§6). One consistent voice = predictable, calm, learnable.

## 3. The "Deep & Warm" recipe (so you can reimplement or port it)

Every sound is **procedurally synthesized** (no audio files), short, with a soft attack and a
faded tail so nothing clicks. Pitches come from a 2-octave **A minor-pentatonic** layout starting
at **A2 (~110 Hz)** — keys are mapped low→high across the keyboard so the hand's position gives a
gentle pitch contour, but the range is narrow and low so it never turns into a "melody."

Per-key voices (ordered low→high, index *i* of *n* total pitched keys):
- **Top ~25% of keys** → a soft **warm-pad tone**: oscillator = sine + gentle partials
  (`0.70·f + 0.18·2f + 0.08·3f + 0.04·4f`), ADSR `attack 10 ms, decay 180 ms, sustain 0.40,
  release 260 ms`, hold ~160 ms, gain ~0.5.
- **The rest, alternating:**
  - even keys → a **deep kick/thump**: a sine whose pitch starts ~3× high and settles to the
    key pitch over ~35 ms, under an exponential amplitude decay (~220 ms body), 2 ms attack,
    plus a tiny 2.2 kHz "tick" for the first <6 ms. Normalize ~0.85.
  - odd keys → a **soft tap/"thock"**: a fast-damped sine (~70 ms decay) with a short broadband
    noise transient (<4 ms), 1.5 ms attack. Normalize ~0.8.

Special keys:
- **Space** → the deepest kick (pitch ~82 Hz, ~260 ms body, soft tick).
- **Enter** → a soft **snare/clap**: high-passed white noise under a sharp ~160 ms decay, a couple
  of fast pre-echo "clap" taps, plus a quiet 180 Hz tone. Normalize ~0.85.
- **Backspace** → a quick **tap** at ~110 Hz, ~60 ms decay.

All buffers get a ~4–5 ms linear fade-out at the end so back-to-back keys never click. Keep the
whole layer **quiet** — it's supportive, not the star.

## 4. Modifications FOR the key-drill app (the important part)

A typing tutor needs feedback the relaxation app doesn't. Add these, in priority order:

1. **Correct vs. incorrect — the one essential change.**
   - **Correct keypress** → the normal Deep & Warm key sound (as above).
   - **Wrong key** → a single, *consistent, gentle* "muted" sound — **never a harsh buzzer.**
     Suggested: a low, dull, short tap (e.g. a ~140 Hz sine, ~90 ms decay, extra noise, **quieter
     than the correct keys** — ~60–70% of their level) with a tiny downward pitch slip so it reads
     as "soft no" rather than "ERROR." The app is about *relaxed* typing — a punishing error sound
     spikes stress and teaches anxiety, which is the opposite of the goal. Make the miss feel like
     a soft cushion, not a slap.
   - This means the audio call needs one extra bit of info from the drill: `play(key, isCorrect)`.

2. **Gentle milestone reward (encouraging, sparing).** On finishing a line / word / drill
   *cleanly*, play one soft, pleasant resolution — e.g. a quiet two- or three-note warm-pad chord
   in the same A-minor-pentatonic key (consonant with the keystroke sounds). Use it **rarely** so
   it stays a treat, not noise. This reinforces progress and a calm, positive association with the
   keyboard. (Skip it if the drill had errors, or play a softer "tidy up and retry" cue instead —
   still gentle.)

3. **Relaxed pacing / low cognitive load.** Keep variation narrow and levels consistent (no
   rising tension, no tempo, no melody). Steadiness is what lets it recede so the learner can
   focus. Don't reintroduce bright/high/sharp sounds — those grab attention involuntarily.

4. **Optional data-analyst flavor (subtle).** Analysts type a lot of **numbers and operators**.
   If you want a light theme touch, give the **number row + symbols** the smooth *warm-pad* timbre
   (instead of the kick/tap), so number drills feel a touch distinct and "smooth." Keep it subtle
   and still calm — optional.

5. **Levels & control.** One master volume + a mute. Default fairly low (keys ~0.5 of full); the
   visual drill is the foreground, sound is support. Correct keys at full pack level, wrong key
   ~65%, milestone reward soft.

## 5. Implementation paths

**If the typing app is web (JS/TS — most likely for an analyst web app):** reimplement the recipe
in the **Web Audio API**. It's small: a few `OscillatorNode` + `GainNode` voices with the
envelopes/params in §3–4, or pre-render each key to an `AudioBuffer` once at load and just trigger
buffers on keypress (lowest latency, matches how the C# app pre-bakes). You do **not** need to
port the C# — just the parameters above. Pseudocode for one keypress:
`isCorrect ? playBuffer(bufferForKey(key)) : playBuffer(missBuffer, gain*0.65)`.

**If the typing app is C#/.NET:** port these files from `CodeKeys.Core/Audio/` — `SampleBuffer`,
`Oscillator`, `Waveform`, `Envelope`, `SynthVoiceFactory`, `PercussionFactory` — plus
`Music/*` (note utils), `Input/SpatialKeyMap`, `Audio/KeyVoiceSet`, `Input/KeystrokeController`,
and the `DeepWarm()` builder from `Presets/PresetLibrary.cs`. They're pure/engine-agnostic. Add a
`miss` buffer + a `playKey(vk, isCorrect)` path on top of `KeystrokeController`. Use whatever audio
output the app already has (NAudio, or the platform mixer).

**Either way, the new bit to build is the feedback model:** the drill tells the sound layer
`(key, isCorrect)` and, at line/drill end, `(milestone, clean?)`. That's the whole integration.

## 6. Why this fits the app's principles (the science, briefly)

- **Low-cognitive-load sound supports learning.** Research on the "irrelevant sound effect" shows
  bright, changing-state, attention-grabbing audio hurts focus; low, steady, percussive,
  habituating audio doesn't. Deep & Warm is built on exactly that — so it can accompany a learning
  task without stealing attention.
- **Gentle error feedback lowers anxiety.** For *relaxed* skill-building, a soft, non-punishing
  miss sound avoids the stress response a harsh buzzer triggers; lower anxiety = better motor
  learning and retention. The point is to build a calm, positive association with typing.
- **Sparing positive reinforcement** at milestones rewards progress and sustains a pleasant,
  flow-friendly mood without turning into clutter.
- **Honesty:** this is about *feel and focus* — no medical/biofeedback claims. Same line as the
  parent project.

## 7. Open questions for the typing app's agent

1. **Framework?** Web (Web Audio) vs C#/desktop vs other — decides reimplement-vs-port.
2. **Error model:** does the drill already know correct-vs-incorrect per keystroke (it should, for
   a tutor)? That single boolean is all the sound layer needs.
3. **Milestone granularity:** per character, per word, per line, per drill — where should the
   gentle reward fire?
4. **Do you want the optional number/operator "analyst" timbre** in v1, or keep it dead simple?
5. **Any ambient option later?** (Out of scope now; note it if you'll want the calm bed as a
   toggle down the road — that's a bigger lift and would reuse the parent project's beat engine.)
