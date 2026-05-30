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
