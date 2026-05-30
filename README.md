# CodeKeys

A tiny Windows tray app that plays sound on **every keystroke, system-wide** —
in VS Code, Slack, the Start menu, a login box, anywhere — plus a separate
continuous **ambient focus bed** running underneath. Two independent audio
layers (keystrokes / ambient), each toggleable on its own. Built for low-latency,
near-zero idle footprint. C# / .NET + NAudio, shipped as a self-contained
single-file `.exe`.

Sound aesthetics come in **swappable packs** you pick from the tray.

---

## ⚠️ Privacy — read this

CodeKeys installs a **global low-level keyboard hook**. From a security
standpoint, **that is the same mechanism a keylogger uses.**

What CodeKeys does and does not do:

- ✅ It inspects each key press **locally, in memory, only** to decide which
  sound to play.
- ❌ It **never stores** your keystrokes.
- ❌ It **never transmits** anything. **The app makes no network connections at all.**
- ⚠️ It **will** make sound while you type passwords and into login fields —
  that's expected. Nothing is logged; use the **global mute hotkey** (panic kill)
  whenever you want silence.

The source is here so you can verify all of the above yourself.

---

## Status

Early development. See [ROADMAP.md](ROADMAP.md) for the build order.

## Stack

- **C# / .NET**, published self-contained single-file `.exe` (double-click to run,
  no toolchain needed).
- **Audio:** NAudio. All pack samples decoded into RAM at pack-load; played
  through a polyphonic mixer. Target latency under ~30ms (WASAPI shared mode),
  hard ceiling 50ms.
- **Keyboard hook:** native `WH_KEYBOARD_LL` global hook.
- **Tray:** standard Windows tray app.

## Packs

A pack is a folder in `%APPDATA%\CodeKeys\packs\` with a `manifest.json` plus its
audio assets. Add a pack by cloning a folder, dropping in assets, and editing the
manifest — no recompile. Schema is documented in [docs/packs.md](docs/packs.md).
