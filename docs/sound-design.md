# CodeKeys — Sound Design Notes

Why the presets sound the way they do. This is the research grounding for the
low-cognitive-interference direction, so future presets stay principled.

## The core problem: "fun but distracting"

The original melodic keyboard maps every key to a different pitch across two
octaves. That is **maximally "changing-state"** — and changing-state sound is
exactly what competes with focused work.

- **Changing-state effect / Irrelevant Sound Effect.** The brain *automatically*
  tracks acoustic *variation* in background sound, and that tracking interferes
  with the serial-order processing you use for verbal/sequential tasks (writing,
  coding). Steady, repetitive, low-variation sound gets *habituated* and tuned
  out; varying sound keeps capturing attention.
  ([changing-state in serial recall](https://pmc.ncbi.nlm.nih.gov/articles/PMC9569014/))
- **Tempo/rhythm consistency matters more than tonal variety.** A study varying a
  melody's tempo hurt recall; varying its *mode* did not. Keeping a steady pulse
  matters more than which notes play.
  ([irrelevant music, tempo vs mode](https://pmc.ncbi.nlm.nih.gov/articles/PMC7381464/))
- **Lyrics/semantic content are the worst** (language-on-language interference);
  melody less so — but a per-key pitch melody is still changing-state.
  ([music with lyrics interferes](https://journalofcognition.org/articles/10.5334/joc.273))

## What makes a low-distraction, still-satisfying keystroke sound

- **Short.** UI feedback should be well under ~300 ms; ours are < ~200 ms.
- **Low frequency.** Lows read as calm, "felt" confirmation and capture less
  attention than bright high transients. (Note: laptop/phone speakers roll off
  deep bass, so we keep fundamentals audible, ~110 Hz, not sub-bass.)
  ([UI sound best practices](https://sfxengine.com/blog/best-practices-for-game-ui-sounds))
- **Consistent timbre + minimal pitch variation** → near steady-state →
  habituation → it disappears into the background.
- **The "beat" satisfaction is the transient + low-end body, repeated
  identically** — that's the Beat Saber "juice": immediate, consistent percussive
  feedback, not melody.
  ([Beat Saber game feel](https://blog.playstation.com/2019/06/27/how-the-beat-saber-devs-make-their-game-feel-so-fun/))
- **The rhythm comes from your own typing cadence**, which is self-generated and
  predictable, so it doesn't fight your attention the way an external varying
  melody does.

## The starter presets

| Preset | Voice | Pitch range | Intent |
|---|---|---|---|
| **Keyboard (melodic)** | warm synth tone | 2 octaves (wide) | The fun, higher-interference template. Kept to clone from. |
| **Pulse (low beat)** | soft low kick (pitch-drop sine + soft tick) | 1 low octave (A2, narrow) | The satisfying low beat; calm enough to fade out. **Default.** |
| **Thock (deep tap)** | damped low tone + short knock | 1 low octave (A2, narrow) | Most neutral, lowest distraction. |

Pitches and decay lengths are deliberately conservative and easy to tune by ear —
see `PresetLibrary.cs`. Adding a preset = add one entry there (and, later, a pack
folder once the manifest loader lands).
