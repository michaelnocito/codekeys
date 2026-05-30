# CodeKeys — Dev Notes (resume point)

Last updated: 2026-05-30

## Where we are
Working app, system-wide. Builds clean, **59/59 unit tests pass**.

- **Build/test (PowerShell):** refresh PATH from Machine+User first, then
  `dotnet build CodeKeys.sln -c Debug` / `dotnet test CodeKeys.sln -c Debug`.
  **Kill any running CodeKeys.exe before building** (it locks Core.dll).
- **Run:** `src/CodeKeys.App/bin/Debug/net8.0-windows/win-x64/CodeKeys.exe`
- Git commit messages: use `git commit -F <file>` (inner quotes break PowerShell
  here-strings). Inline author:
  `git -c user.email=276664011+michaelnocito@users.noreply.github.com -c user.name="Michael Nocito"`

## Done (build steps 1–4 + sound direction)
1. Repo scaffold, README (privacy callout), ROADMAP.
2. Audio engine: WASAPI shared (WaveOut fallback), polyphonic mixer (16-voice
   cap), master volume/mute. Ambient bed layer — **PARKED** (Mike: "blows").
3. Spatial key map, procedural synth, click-free envelopes.
4. **Global WH_KEYBOARD_LL hook** — sounds in any app. MainWindow control panel.
5. **Presets** (research-grounded low-cognitive-load): PercussionFactory
   (kick/tap), PresetLibrary, live dropdown switching. 3 presets: Keyboard
   (melodic template), **Pulse** (low beat, default), Thock (deep tap).
   Rationale + sources: `docs/sound-design.md`.

## Open / next (in rough priority)
- **Tune the low-beat presets by ear** (Mike used "works for now" — revisit when
  he has tuning notes: punch, pitch, length, attack tick). Names are placeholders.
- **Persistence (JSON in %APPDATA%\CodeKeys)** so settings + selected preset
  survive restart, and **"save custom preset"** becomes possible. (Roadmap step 7.)
- **Pack/manifest folder system** — presets as editable folders. (Roadmap step 5.)
- **Tray shell** — toggles, master volume, **global mute hotkey (panic kill)**,
  pack picker, autostart toggle (off by default), About (build stamp + privacy).
  (Step 6.) Right now you quit by closing the window; no mute hotkey yet.
- **Publish** self-contained single-file .exe. (Step 8.)

## Architecture quick map
- `CodeKeys.Core` (pure, net8.0): Music, Input (SpatialKeyMap, KeystrokeController),
  Audio (Synth/Percussion factories, KeyVoiceSet, IVoicePlayer), Presets.
- `CodeKeys.App` (net8.0-windows, NAudio, WinForms): AudioEngine, GlobalKeyboardHook,
  MainWindow.
- `CodeKeys.Tests` (xUnit): 59 tests over the Core logic.
