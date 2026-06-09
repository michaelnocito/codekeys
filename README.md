# Bowl Bass Keys

*(The git repo and executable are named `codekeys` for continuity; the app is **Bowl Bass Keys**.)*

A small Windows desktop app that plays a sound on **every keystroke, system-wide** — in VS Code, Slack, a browser, anywhere — and generates a calm **generative bed of deep bass + Tibetan singing bowls** underneath that quietly responds to how you type. Built as a personal tool for staying in flow while working. Others are welcome to use it.

**[Download → CodeKeys.exe](https://github.com/michaelnocito/codekeys/releases/latest/download/CodeKeys.exe)**
*(Windows 10/11, 64-bit. Double-click to run — no installer, no admin rights.)*

---

> **Personal project — active development.** I built this for myself. It works, it's tested, and it gets used daily, but it will go through changes. Don't depend on it for anything critical. Feedback and issues welcome.

---

## Why does Windows show a "Run Anyway" warning?

When you download and run the `.exe`, Windows SmartScreen will show a blue warning that says *"Windows protected your PC"* and won't run it immediately.

**This is expected and harmless.** Here's why it happens:

Windows SmartScreen flags any executable that:
- was downloaded from the internet, and
- comes from a publisher who hasn't paid for a **code-signing certificate** (~$300–500/year)

Code-signing certificates are how Microsoft's system distinguishes "known publishers" from everyone else. This is a personal hobby project with no commercial budget, so the exe is unsigned. The warning is not a virus alert — it just means Microsoft hasn't seen enough downloads from this specific file hash to auto-trust it yet.

**To run it:** click **"More info"** (in the SmartScreen dialog), then **"Run anyway"**. That's it.

If you want to verify the file independently, the full source is in this repo and the build is reproducible.

---

## What it does

Bowl Bass Keys runs two independent layers:

**1. Keystroke sounds** — a distinct click, tap, or tone on every keypress, system-wide. Six selectable voice packs, all procedurally synthesized (no audio files). The four calm packs are tuned to match the current beat so nothing clashes.

**2. Generative beat bed** — a continuously evolving piece of deep bass and Tibetan singing bowls that slowly builds up over ~10 minutes, reacts to your typing rhythm, and loops seamlessly. You don't need to touch it — it just plays underneath whatever you're doing.

Both layers are independently toggleable. Overall volume follows Windows (Bowl Bass Keys appears as its own entry in the system volume mixer).

> **Tip:** play it through a speaker, even a small Bluetooth one. The deep bass is meant to be felt. Headphones work well too; built-in laptop speakers lose the low end.

---

## Keystroke voice packs

| Pack | Character |
|---|---|
| **Deep & Warm** (default) | Deep thump on the root keys, mid pop on consonants, warm-pad top layer, soft occasional snare |
| **Soft Mallet** | Rounded mallet tones, gentle attack |
| **Warm Keys** | Mellow piano-adjacent timbre |
| **Felt Piano** | Damped, intimate — like a piano with the sustain pedal off |
| **Water Drops** | Light drips and plinks |
| **Boings** | Deliberately silly — use with caution |

---

## Beat templates

Each template sets a root note, scale, tuning, and tonal character. The keystroke sounds lock to the same key automatically.

| Template | Tuning / character |
|---|---|
| **Root** | 396 Hz · grounding, steady low end |
| **Sacral** | 417 Hz · slightly warmer, mid-forward |
| **Solar Plexus** | 528 Hz · brighter, more presence |
| **Heart** | 639 Hz · open, centered |
| **Throat** | 741 Hz · clear, expressive |
| **Third Eye** | 852 Hz · spacious, introspective |
| **Crown** | 963 Hz · airy, overtone-rich |
| **Space Clearing** | 432 Hz · broad sweep, full-range bowl |
| **Chakra Sweep** | 21-minute guided journey — the bowl slowly climbs through all seven tunings |
| **Focus** | 40 Hz isochronic tone (340 Hz carrier, AM-modulated) + soft brown noise floor · the isochronic tone is the primary layer (must be audible to work); brown noise sits underneath as a low acoustic mask · steady 60–68 BPM Dorian groove · works on speakers and headphones |

---

## How to use it

1. **Download** `CodeKeys.exe` from the [Releases page](https://github.com/michaelnocito/codekeys/releases/latest) and run it (see the SmartScreen note above)
2. The app opens to a single settings panel — everything is visible at once, no menus to dig through
3. Toggle **Keystrokes** on if you want per-key sounds; toggle **Beat** on for the ambient bed
4. Pick a **voice pack** and a **beat template** from the dropdowns
5. Use the **Mix** sliders to balance keystroke sound vs. beat volume
6. **Restart Beat** resets the build from silence (useful if you want a fresh start mid-session)
7. **Living events** adds occasional soft chimes and splashes that react to bursts and pauses in your typing
8. **Demo** fast-forwards the beat build so you can hear what it sounds like fully layered without waiting 10 minutes
9. **Check for Updates** pings GitHub and downloads a new version if one exists — the app will close, install, and reopen automatically

Bowl Bass Keys does not auto-start, does not persist anything to disk, and does not appear in the taskbar (only in the window title bar and the sound mixer). Close it and it's gone until you open it again.

---

## Privacy

Bowl Bass Keys installs a **global low-level keyboard hook** (`WH_KEYBOARD_LL`) — the same OS mechanism a keylogger uses — so here is exactly what it does and does not do. The full source is in this repo.

| | |
|---|---|
| ✅ | Inspects each keypress locally and briefly to choose a sound and note its *timing + category* (letter / number / punctuation / backspace / etc.) |
| ❌ | **Never records the characters you type** — only timing and key-category counts are kept (`SignalsCollector.Snapshot()`, verified by `CaptureTests`) |
| ❌ | **Never writes anything to disk** — no logs, no files, no registry, no `%APPDATA%` |
| ❌ | **Never transmits anything** — no network connections except the optional update check (GitHub Releases API, initiated only when you click "Check for Updates") |
| ❌ | **Does not inject or alter input** — the hook is read-only; every key passes through unchanged (`CallNextHookEx`) |
| ❌ | **Does not auto-start** — no `Run` key, no startup folder, no service or scheduled task |

Typing telemetry lives in a rolling ~30-second RAM buffer and is continuously evicted — nothing is ever serialized. The app will make sound while you type passwords (that's expected; nothing is logged). Toggle **Keystrokes** off or close the app if you want silence.

---

## Stack

- **C# / .NET 8**, published as a self-contained single-file `.exe`
- **NAudio 2.2.1** — WASAPI shared mode (never monopolizes your audio device)
- **Fully procedural synthesis** — all sound is generated in code; no audio files anywhere in the project
- **Global keyboard hook** — native `WH_KEYBOARD_LL`, read-only
- 207 unit tests

---

## Status

Personal project, active development. The core experience is stable and daily-driven. Templates, voice packs, and the adaptive beat system will keep evolving. Breaking changes may land without notice.

Built by [Michael Nocito](https://michaelnocito.github.io).
