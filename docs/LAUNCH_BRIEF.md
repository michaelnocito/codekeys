# Bowl Bass Keys — Launch Brief & Advert Handoff

*A complete handoff for designing a launch advertisement. Everything the product is, every
feature, the science behind it, the honesty guardrails, and the playback recommendation.*

Prepared 2026-06-01. Build: clean, 207/207 unit tests passing.

---

## 0. TL;DR for the advert

**Bowl Bass Keys turns your typing into a calm, living soundscape.** Every keystroke plays a
warm sound, and underneath, a generative bed of **deep bass + Tibetan singing bowls** quietly
responds to *how* you type — your speed, rhythm, and pauses — to gently keep you in flow. It
never sees *what* you type. Choose a Tibetan beat, a chakra tuning, a 432 Hz space-clearing
mode, or a 21-minute guided chakra journey.

**One thing the ad must say:** *play it through a speaker — the bass is the point.* A normal
portable Bluetooth speaker is ideal; headphones are a fine alternative; laptop speakers miss
the low end that makes it work.

---

## 1. What it is

- A **Windows desktop app** that listens for keystrokes **system-wide** (works while you type
  in any app — code editor, browser, docs) and produces two layers of sound:
  1. **Keys** — a sound on every keystroke.
  2. **Beat** — a continuous, generative ambient bed of deep bass + singing bowls that adapts
     to your typing behavior.
- It is a **focus / relaxation / ambiance tool** for people who work at a keyboard. Calm, not
  gamified. Premium, minimal, art-forward.
- **Privacy-first by design:** it records only *timing* and *key category* (letter / number /
  punctuation / backspace / etc.) plus a single upper/lower-case bit — **never the actual
  characters.** The music is seeded from the chosen mood, not from your words.

---

## 2. The complete feature list

### 2.1 Keystroke sound layer ("Keys")
- **System-wide global keyboard hook** — sounds play no matter which app you're typing in.
- **"Midnight" voicing** (the signature sound): a deep-beat blend with a **spatial key map** —
  low keys thump like a deep kick, middle keys pop as taps, the top row is a warm smooth synth,
  and Enter is a soft snare. Pitch zones follow the physical location of the key.
- Procedurally synthesized (no audio sample files), click-free envelopes.

### 2.2 Generative beat bed ("Beat") — 10 selectable templates
All share one musical foundation (see §2.3); only the singing-bowl frequency and tempo differ.

| Template | Bowl frequency | Tempo | Notes |
|---|---|---|---|
| **Tibetan Beat** | scale-tone bowls | 60–72 BPM | the "unspecific" bowl + bass mode (Dorian) |
| **Root chakra** | 396 Hz | 54–66 BPM | slower + a **1.25× bass boost** (grounding) |
| **Sacral chakra** | 417 Hz | 60–72 BPM | |
| **Solar Plexus chakra** | 528 Hz | 60–72 BPM | |
| **Heart chakra** | 639 Hz | 60–72 BPM | |
| **Throat chakra** | 741 Hz | 60–72 BPM | |
| **Third Eye chakra** | 852 Hz | 60–72 BPM | |
| **Crown chakra** | 963 Hz | 60–72 BPM | |
| **Space Clearing** | 432 Hz | 72–84 BPM | faster, sweeping; the "universe vibration" |
| **Chakra Sweep · 21 min** | walks 396 → 963 | 60–72 BPM | guided journey, 3 min per chakra, Root → Crown |

### 2.3 The musical foundation (shared by every template)
- **Deep bass is the star** — a pure, resonant low sine (~73 Hz) with long decay. A warm
  rolling "boom" that acts as a **blanket, not a leash**.
- A grounded **I–I–V–I bass progression** (the perfect fifth computed by interval, so it stays
  correct in any scale).
- **Singing bowls as quiet "appearances"** — each bowl strike is a ~10-second swell (slow
  ascend → brief sustain → long graceful fade), spaced out so it floats in and out of the
  background rather than ringing constantly. The bowl is always well behind the bass.
- Soft drum pulse + occasional dark "splash" accents for life, never bright/clattery.

### 2.4 The adaptive "Conductor" (the headline technology)
The bed isn't a loop — it's *generated and gently steered in real time* to keep you in flow.
- **Reads your "arousal"** from privacy-safe typing telemetry: ~55% typing speed, ~25% rhythm
  erraticness, ~20% struggle (backspaces). It never reads characters.
- **Ride-along behavior** (three zones):
  - **Calm** — holds a steady easy-flow pulse; doesn't drag you down.
  - **Flow** — *rides along* with you: as you speed up, the music lifts a little, but never
    matches you 1:1.
  - **Tense** — only when you're clearly over-driven does it gently counter-act and bring the
    energy back down.
- **Deliberately slow and unintrusive:** rate-limited tempo changes, a responsiveness ramp that
  fades adaptation in over the first ~5 minutes, smoothing over a 30-second window. It steers
  only when it's sure.
- **Additive build** (default): every session starts almost silent and **voices enter one at a
  time over ~10 minutes** — the texture assembles like people quietly joining a drum circle.
- **Breathing cycle:** after the peak, the music gracefully unwinds back toward silence over
  ~8 minutes and then rebuilds — an organic in-and-out, like waves. (The Chakra Sweep is the
  exception: it holds a steady, present bed so each chakra is clearly heard for its 3 minutes.)

### 2.5 Living events (optional toggle — NEW)
- An opt-in accent channel that fires **soft one-shot sounds at meaningful moments** in your
  typing: a gentle **chime** on a burst of flow, a soft **splash** as you settle or correct.
- It is **self-calibrating** — it fires only when a change in your typing is large *relative to
  your own recent variability*, so it adapts to fast and slow typists alike instead of using a
  fixed schedule. (See the science section — this is the PlantWave technique.)
- Off by default; when off, the bed is unchanged.

### 2.6 Controls & interface
- **Phone-app-clean UI** in the michaelnocito.github.io look: a portrait single column of soft
  rounded cards on white, charcoal text, one blue accent, generous whitespace. iOS-style
  toggles and flat sliders.
- Sections: **Sound** (Keystrokes / Beat on-off), **Beat Template** (the 10-template picker),
  **Mix** (Keystrokes + Beat level sliders; keys auto-track at half the beat level), **Flow**
  (Living events + Demo toggles), a **Restart beat** button.
- **Volume follows Windows** (the app is its own entry in the system volume mixer).
- **Demo mode** fast-forwards the long build so you can audition the whole arc in under a minute.

### 2.7 Under the hood (for credibility, not necessarily ad copy)
- C# / .NET 8, NAudio audio engine, WASAPI shared mode (never monopolizes your audio device),
  WinForms. **Fully procedural synthesis — zero audio asset files.** Pure, deterministic,
  unit-tested core logic (**207 passing tests**). Near-zero idle footprint.

---

## 3. The science & design grounding

> **Honesty stance (read this first):** Bowl Bass Keys influences **psychological attention and
> arousal** — the feeling of focus and calm. It makes **no medical, physiological, "healing,"
> heart-rate-entrainment, or brainwave claims.** The evidence for tempo→heart-rate or
> sound→biofield effects is weak, so we don't claim them. The chakra/Solfeggio framing is
> presented as a **sound-healing tradition / aesthetic**, not as medicine. The advert must
> stay on the right side of this line: evocative, not pseudo-medical.

**Flow & arousal regulation.** The conductor is built on two well-established ideas: the **iso
principle** (meet the listener where they are, then gently guide) and the **Yerkes–Dodson law**
(there's an optimal middle level of arousal for performance — too low = disengaged, too high =
frazzled). The resting tempo sits in the research-supported **60–80 BPM "relaxed-focus" band**,
with a ~66 BPM flow anchor it returns to.

**Sonification & parameter mapping** (the *Sonification Handbook*, the field's canonical text).
Turning non-musical data into sound has established, reusable methods we used directly:
- **Parameter mapping** — map a signal's value *and its rate of change* to distinct sound
  parameters.
- **Quantize-to-scale** — snap free-running values onto a musical scale (we use pentatonic /
  modal) so the result is consonant, never a siren.
- **Auditory icons** — fire a short, recognizable sound *when an event occurs* (the basis for
  the Living-events chimes/splashes).

**The PlantWave / MIDI Sprout technique** (granted patent US 10,909,956 B2 + the open-source
firmware). The plant-music devices that inspired the Living-events feature don't fire sounds on
a fixed schedule — they fire when a fluctuation is **large relative to the signal's own recent
variability** (change vs. a rolling standard-deviation threshold), and they drive *extra
effects/voices* from the signal's **rate of change**. We adopted exactly this: our Living-events
detector watches your typing's "velocity" and fires only on changes that genuinely stand out —
self-calibrating to each person and each session.

**Taming a noisy live signal.** Peer-reviewed work shows that, for jittery real-time data,
**temporal averaging** and mapping change to **tempo/density rather than raw pitch** are
measurably calmer and less distracting — which is exactly how the conductor behaves (it moves
density and tempo, not a melody line).

**Additive minimalism.** The "voices join one at a time" build is modeled on **Steve Reich's
*Drumming*** and **West African drum-circle** layering — the feeling of a beat assembling itself
from near-silence.

**Low-distraction by design.** Research on irrelevant sound and auditory salience shows that
**bright, high, sharp, constantly-changing sounds capture attention involuntarily** — the enemy
of focus. So the design deliberately favors **deep, warm, slow, low** material (bass + bowls),
keeps high/bright tones out of the working bed, and makes changes gradual.

**Why deep tones matter.** Low frequencies are **felt as much as heard** — the body responds to
sub-bass and resonance in a way it doesn't to thin treble. This is the entire rationale for the
bass-forward design *and* for the speaker recommendation below.

---

## 4. ⭐ Playback recommendation (MUST appear in the advert / onboarding)

**Use a speaker. The bass is the heart of the experience.**

- The deep bass boom (~73 Hz) and the singing-bowl resonance are meant to be **felt in the
  room, not just heard.** That low end is where the calm comes from.
- ✅ **Best & recommended: a normal portable Bluetooth speaker.** It reproduces enough low end
  and fills the room — exactly the intended experience. No fancy setup required.
- ✅ **Good alternative: headphones.** Solid bass and detail; you lose a little of the "felt in
  the room" ambiance, but it's a perfectly enjoyable way to use it.
- ❌ **Avoid built-in laptop speakers** — they can't reproduce the bass, which is the whole
  point; it'll sound thin and miss the effect.

Phrase it as a friendly tip, not a barrier: *"For the full effect, play it through a speaker —
even a small Bluetooth one. The deep bass is meant to be felt. Headphones work great too."*

---

## 5. Positioning & creative direction (notes for the architect)

- **Audience: broad and inclusive** — people who work or create at a keyboard, of any gender.
  **Design it to appeal to men and women alike**, art-first and calming. **Do not** target
  "young gamer guys," and **do not** pander or use superficial gender cues.
- **Tone:** calm, premium, minimal, a little mystical-but-grounded. Matches Mike's site
  (michaelnocito.github.io): white space, charcoal, one accent, restraint.
- **Core promise options to explore:** "Your typing becomes a calm soundscape." / "Music that
  keeps you in flow." / "Space clearing and chakra vibing while you work." / "Type into the
  bass."
- **Key differentiators to feature:**
  1. It **responds to how you type** (adaptive, generative — not a static playlist).
  2. **Deep bass + singing bowls**, designed to be felt.
  3. **Private by design** — it never sees what you type (strong trust hook).
  4. **Chakra tunings + a 21-minute guided chakra journey.**
  5. **Living events** that react to your flow in the moment.
  6. Calm, unintrusive, low-distraction — built on real focus/sonification science.
- **Honesty guardrails (repeat):** no medical / healing / heart-rate / brainwave claims; chakra
  & Solfeggio = tradition/aesthetic framing only.
- **Privacy as a feature, not fine print:** lead with "never records what you type."

---

## 6. Suggested ad bullet block (drop-in starting point)

> - 🎹 **Every keystroke sings** — system-wide, in any app.
> - 🌊 **A living bed of deep bass + Tibetan singing bowls** that adapts to *how* you type.
> - 🧘 **Chakra tunings, 432 Hz space clearing, and a 21-minute guided chakra journey.**
> - ✨ **Living events** — soft chimes and swells that react to your flow.
> - 🔒 **Private by design** — it never records what you type.
> - 🔊 **Best on a speaker** — the deep bass is meant to be felt (a small Bluetooth speaker is
>   perfect; headphones work great too).

---

*Fact-check anchors for any claims in the ad: flow/arousal = iso principle + Yerkes–Dodson;
sonification methods = the Sonification Handbook; the change-relative-to-variability event
trigger = PlantWave/MIDI Sprout patent US 10,909,956 B2; "felt not just heard" = low-frequency
embodiment rationale. Keep all wording within the honesty stance in §3.*
