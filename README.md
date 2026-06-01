# Bowl Bass Keys

*(The git repo and executable are named `codekeys` for history continuity; the app is
**Bowl Bass Keys**.)*

A small Windows desktop app that plays a sound on **every keystroke, system-wide** — in VS
Code, Slack, a browser, anywhere — and, underneath, generates a calm **bed of deep bass +
Tibetan singing bowls** that gently responds to *how* you type (your speed, rhythm, and
pauses) to help keep you in flow. Built for low latency and near-zero idle footprint.
C# / .NET 8 + NAudio. All sound is **procedurally synthesized** — there are no audio asset
files.

---

## ⚠️ Privacy — read this

Bowl Bass Keys installs a **global low-level keyboard hook** (`WH_KEYBOARD_LL`). From a
security standpoint, **that is the same mechanism a keylogger uses** — so here is exactly what
it does and does not do. The source is public so you can verify every line.

What it **does**:
- ✅ Inspects each key press **locally, in memory, only** — long enough to (a) pick which
  sound to play and (b) note its *timing* and *category* (letter / number / punctuation /
  backspace / etc.) plus a single upper/lower-case bit, so the music can react to your rhythm.

What it **does not** do:
- ❌ It **never records the characters you type.** The captured text is always empty — only
  timing and key-category counts are kept. (See `SignalsCollector.Snapshot()`; verified by
  `CaptureTests`.)
- ❌ It **never writes keystrokes (or anything else) to disk.** No logs, no files, no registry,
  no `%APPDATA%`. The only file it ever reads is its own `.exe` (to show a build date).
- ❌ It **never transmits anything.** The app makes **no network connections at all** — there
  are no networking libraries in the project.
- ❌ It **does not inject or alter input.** The hook is read-only: every key is passed straight
  through unchanged (`CallNextHookEx`). It cannot type for you, control other apps, or be used
  to drive another program.
- ❌ It **does not persist or auto-start.** No registry `Run` key, no startup folder, no
  service, no scheduled task. Close it and it's gone; reboot and it does not come back unless
  you launch it.

How the data is held:
- Typing telemetry lives in a **rolling ~30-second buffer in RAM only**, continuously evicted.
  Nothing is ever serialized or saved.
- It **will** make sound while you type passwords or into login fields — that's expected, and
  nothing is logged. Turn off the **Keystrokes** toggle (or close the app) whenever you want
  silence.

---

## What it does

- **Keystrokes** — a sound on every key, system-wide. Six selectable voicing packs: **Deep &
  Warm** (default — deep thumps, mid taps, warm top, soft snare), **Soft Mallet**, **Warm Keys**,
  **Felt Piano**, **Water Drops**, and a deliberately silly **Boings**. The four calm packs are
  tuned to the beds' key so they stay consonant with whatever beat is playing.
- **Beat** — a generative bed of deep bass + singing bowls that adapts to your typing via an
  adaptive "conductor" (see `DEV_NOTES.md` / `docs/sound-design.md`). Templates: the seven
  chakra tunings, a 432 Hz Space Clearing mode, and a 21-minute guided Chakra Sweep.
- **Living events** (optional) — soft chimes/splashes that fire on bursts and settles in your
  typing flow.

Each layer is independently toggleable. Overall volume follows Windows (the app is its own
entry in the system volume mixer).

> **Tip:** play it through a speaker — even a small Bluetooth one. The deep bass is meant to be
> *felt*. Headphones work well too; built-in laptop speakers miss the low end.

## Status

Active development. Builds clean; 207 unit tests passing. See [ROADMAP.md](ROADMAP.md) and
[DEV_NOTES.md](DEV_NOTES.md).

## Stack

- **C# / .NET 8**, intended to publish as a self-contained single-file `.exe` (double-click to
  run, no toolchain needed, no admin rights required).
- **Audio:** NAudio, WASAPI **shared** mode (never monopolizes your audio device). Fully
  procedural synthesis, polyphonic mixer.
- **Keyboard hook:** native `WH_KEYBOARD_LL` global hook, read-only.
- **Compliance:** see [docs/COMPLIANCE.md](docs/COMPLIANCE.md) for the security audit and the
  safe-distribution checklist (code signing, VirusTotal/Defender submission).
