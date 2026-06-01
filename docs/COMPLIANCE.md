# Bowl Bass Keys — Security & Compliance

*Why a keystroke-sound app is **not** a keylogger, verified against the source, plus the
checklist for distributing the Windows build without being flagged as malicious.*

Last audited: 2026-06-01 (build clean, 207/207 tests passing).

---

## The concern

Bowl Bass Keys installs a **global low-level keyboard hook** (`WH_KEYBOARD_LL`) so it can play
a sound on every keystroke in any app. That is the *same OS mechanism a keylogger uses*, so the
app must be — and provably is — transparent about what it does with what it sees. The risk we
are guarding against is twofold: (1) the app itself being or becoming a privacy problem, and
(2) the app being **construed** as malicious by users, antivirus heuristics, or SmartScreen, or
being usable as a vector to control/observe other programs.

## Audit result: CLEAN

A full read-only audit of `src/` and `tests/` found **no keylogging, spyware, malware, or
data-exfiltration indicators, and no concerns.** Every privacy claim is enforced by the
architecture, with evidence:

| Claim | Verified by |
|---|---|
| Hook is **read-only** — keys pass through unchanged, never swallowed or altered | `GlobalKeyboardHook.cs` callback reads only the vk, raises an event, and always calls `CallNextHookEx` |
| **Characters are never captured** — only timing + category + an upper/lower bit | `MainWindow.OnHookKeyDown` keeps only `KeyClassifier.Classify(vk)` (a `KeyKind` enum); `SignalsCollector` stores `(timestamp, KeyKind, isUpper)` tuples |
| Captured **text is always empty** | `SignalsCollector.Snapshot()` hard-codes `Text = ""`; asserted by `CaptureTests` |
| **No disk writes** of any kind | The only file I/O in the app is `File.GetLastWriteTime()` on its own `.exe` (build-stamp display). No `File.Write*`, `FileStream`, `StreamWriter`, registry, or `%APPDATA%` |
| **No network / telemetry** | No `HttpClient`/`Socket`/`WebClient`/URLs anywhere; the only NuGet dependency is `NAudio` (audio) |
| **No input injection** | No `SendInput`, `keybd_event`, `mouse_event`, clipboard, or screen-capture APIs |
| **No persistence / autostart** | No registry `Run` key, startup folder, service, or scheduled task. The only P/Invokes beyond the hook are `ShowWindow`/`SetForegroundWindow` (single-instance window focus) |
| **Cannot be used to manage/control other programs** | The hook neither modifies the input stream nor forwards data anywhere; it only triggers in-process audio |

**Conclusion:** the keyboard hook is read-only on input and write-only to audio hardware, with
no persistent state of keystroke content. The privacy guarantee ("records only timing + key
category, never characters, never transmits, never persists") holds.

## Distribution checklist (do these before handing out the Windows build)

To keep a legitimate keyboard-hook app from tripping SmartScreen / AV heuristics:

1. **Code-sign the `.exe`** (Authenticode). This is the single most important step — it
   establishes publisher identity, prevents the SmartScreen "unknown publisher" warning over
   time, and keeps Defender from a "potentially unwanted app" false positive.
2. **No admin elevation.** `WH_KEYBOARD_LL` runs in user mode — keep the manifest at
   `asInvoker`; never request elevation. (An app that needlessly asks for admin looks worse.)
3. **Pre-submit for scanning** before mass distribution:
   - **VirusTotal** (free, ~70 engines) — catch and dispute any false positives early.
   - **Microsoft Defender** submission portal — submit the signed exe with the README +
     this document as evidence of intent.
   - Optionally submit directly to major vendors (Avast, McAfee, Kaspersky).
4. **Publish a SHA-256 checksum** next to the download so users can verify integrity.
5. **Distribute from a branded page** (e.g. michaelnocito.github.io) with: the signed exe, the
   checksum, a link to the source, and a short plain-English privacy FAQ ("never records what
   you type · no network · no telemetry · no autostart").
6. **Keep the disclosure prominent** — the README leads with the privacy section; keep it that
   way, and keep it accurate to the shipped build (no stale claims about files/registry it
   doesn't touch).
7. **Reputation builds over ~2–4 weeks** after first signing; expect some first-download
   friction until then, and point users to VirusTotal results in the meantime.

## Honesty note

Separate from security: the product makes **no medical, "healing," heart-rate-entrainment, or
brainwave claims** (the evidence is weak). Chakra / Solfeggio framing is a sound-healing
*tradition / aesthetic*. Keep marketing and onboarding within that line. See
`docs/sound-design.md` for the grounded rationale.
