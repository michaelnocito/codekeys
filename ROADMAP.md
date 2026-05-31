# CodeKeys — Build Order

Locked stack: **C# / .NET + NAudio**, self-contained single-file `.exe`.
One task at a time. Audio engine first, global hook later, UI last.

1. **Repo + scaffold** ✅ — private GitHub repo, local non-OneDrive clone,
   README (privacy callout), .gitignore, this roadmap.
2. **Audio engine** — two layers:
   - (a) keystroke voices: synth (pitched tones on a scale) + sample playback,
     polyphony (~16-voice cap, let ring), spatial 2-octave in-key note map.
   - (b) ambient bed: seamless looping source, independent gain/mute.
3. **Standalone test window** — type in a textbox → hear keystroke layer; a
   button to start/stop the bed. Proves both layers + latency *before* the hook.
4. **Global keyboard hook** (`WH_KEYBOARD_LL`) — verify latency under ceiling.
   Space/Enter/Backspace distinct; pure modifiers silent.
5. **Pack loader** — `manifest.json` schema + folder scan; ship 4 starter packs:
   - Keys: "Melody" (melodic synth, spatial map) — proves synth keys.
   - Focus: "Brown Study" (brown-noise bed + soft felt-tap keys).
   - Focus: "Rain Room" (rain bed + soft water-drop pentatonic keys).
   - Focus: "Drift" (slow evolving pad bed + soft bells, long reverb).
   - v1 packs are **procedurally synthesized** (zero binary assets); one small
     committed `.wav` sample pack proves the sample-loader path.
6. **Tray shell** — two independent toggles (ambient / keystrokes), master volume
   slider, **global mute panic hotkey**, pack picker (live switch), autostart
   toggle (OFF by default), About dialog (visible build stamp + privacy callout).
7. **Persistence** — JSON config in `%APPDATA%\CodeKeys\`. Remember: selected
   pack, master volume, each layer's toggle, mute state, autostart. Returns as left.
8. **Publish** — self-contained single-file `.exe` (no aggressive IL trimming;
   NAudio reflection can break under it).

## Decisions locked at scaffold time
- **Audio API:** WASAPI **shared** mode (not exclusive — exclusive would
  monopolize the device and silence Slack/music; wrong for an ambient app).
  Latency target as low as shared mode allows, 50ms hard ceiling.
- **v1 packs procedural** — synthesized in code at load, royalty-clean, no asset
  hunting; plus one tiny committed `.wav` pack to exercise the sample path.
- **Tray:** WinForms `NotifyIcon` shell (lightest path for tray-only).
- **Publish:** self-contained (~60-70MB exe, bundles runtime; double-click runs).

## Out of scope (dropped from original concept)
- No keyword/word-completion logic, no profiles, no prefix tracking. Stateless
  key → sound lookup.
- Not Electron.
- Generative/adaptive (Endel-style) beds are **v2**; v1 ambient = looped file only.

---

## v2 — Adaptive music engine (captured 2026-05-30)

Mike's direction: the app should **generate music for you** from your keystrokes
(within the existing privacy guidelines). Goal: gently keep the user in a flow /
"wu wei" state — efficient, low-tension, sustained positive progress.

**Scope decisions (2026-05-30):** ❌ Apple Watch, ❌ heart-rate / biofeedback of
any kind ("not important to our cause"), and ❌ cross-platform are all CUT.
CodeKeys is **Windows-only**, driven **purely by keystrokes**.

### Research grounding (so we don't ship pseudoscience)
- **Iso principle** (music therapy): don't jump straight to the target mood —
  *match* the user's current arousal, then *gradually* lead it toward the target.
  An agitated person resists sudden calm music but will follow a slow ramp. → our
  tempo/intensity changes must be **slow and gradual** (validates Mike's "changes
  too fast" note). Sources in docs/sound-design.md.
- **Yerkes-Dodson / flow**: performance peaks at an *optimal* arousal (inverted-U);
  flow ≈ the peak. Too fast/intense → stress; too slow → boredom/disengagement.
  So the target is a **flow band**, not "always calmer." Tempo is the main lever
  (faster ≈ more arousal, >~94 BPM raises it).
- **HONESTY CAVEAT — heart-rate entrainment is weak**: rigorous studies find HR
  does *not* reliably sync to musical tempo. So we must NOT claim "the beat slows
  your heart." Legit mechanism = psychological arousal regulation + attention +
  iso-principle leading. "Brain-wavelength"/binaural-beat framing = avoid.

### The adaptive "conductor" (buildable now, no hardware)
A layer above the beat that merges the earlier Phase-2 session-arc with arousal
regulation. Inputs already exist in `SignalsCollector` (rate, gap variance,
backspaces, punctuation — privacy-safe, no characters).
- **Read arousal proxy, not raw speed.** Rising arousal/agitation = speeding up
  *and/or* rising variance/backspaces (bursty, erratic). Disengagement = slowing,
  long pauses, low variance. (Pure fast+steady = flow → leave it alone.)
- **Thermostat toward a flow band** via the iso principle: nudge BPM/density in
  **small steps over many loop cycles** (e.g. ≤1-2 BPM per loop), never jumps.
  - Arousal climbing → gently ease tempo/density down to settle them.
  - Engagement dropping → gently lift tempo/density to re-activate them.
- **Compose with the melody arc**: the motif still develops over ~15-20 min
  (emerge → develop → resolve); the thermostat modulates intensity within it.
- **Fix carried over**: `BeatSequencer.UpdateGroove` resets `_cycle=0` every 3s —
  must preserve session time or the arc/ramp keeps restarting.

### Smaller items
- **Volume follows the OS** ✅ DONE (2026-05-30). WASAPI shared mode already makes
  CodeKeys its own entry in the Windows volume mixer (scaled by system master +
  per-app fader), so the in-app slider was redundant — removed it; the window now
  just shows a "Volume follows Windows" hint. Fixed internal headroom = 0.85.
  Optional future: a global mute/panic hotkey (tray step 6).
