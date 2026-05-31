using System.Diagnostics;
using System.Runtime.InteropServices;
using CodeKeys.App.Audio;
using CodeKeys.App.Input;
using CodeKeys.Core.Beat;
using CodeKeys.Core.Input;
using CodeKeys.Core.Presets;

namespace CodeKeys.App.UI;

/// <summary>
/// CodeKeys control panel. Keystroke sound comes from the system-wide hook (type in any
/// app), and an optional generative beat plays underneath at −12 dB, locked to the same
/// scale so it never clashes. This window can sit minimized.
/// </summary>
public sealed class MainWindow : Form
{
    private readonly AudioEngine _engine = new();
    private readonly GlobalKeyboardHook _hook = new();
    private readonly KeystrokeController _keystrokes;
    private readonly BeatSequencer _beat;

    // Live typing capture (module 1) — feeds the beat. Records only timing + key categories,
    // never the characters typed.
    private readonly SignalsCollector _signals = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private System.Windows.Forms.Timer _signalTimer = null!;
    private bool _shiftDown;

    private CheckBox _keysToggle = null!;
    private CheckBox _bedToggle = null!;
    private Label _status = null!;
    private ComboBox _presetPicker = null!;

    // Representative typing signals. NOTE: Text is intentionally left empty — CodeKeys never
    // captures what you type (privacy), so the beat seeds from the mood, not your keystrokes.
    private static readonly Signals DefaultSignals = new()
    {
        Text = "",
        DurationMs = 8000,
        CharCount = 60,
        Backspaces = 2,
        AvgGapMs = 180,
        GapVariance = 120,
        CapsRatio = 0.08,
        PunctCount = 2,
    };

    public MainWindow()
    {
        // Start on the default preset (Midnight — the deep-beat blend).
        var baked = PresetLibrary.Default.Build(AudioEngine.InternalRate);
        _keystrokes = new KeystrokeController(baked.Map, baked.Voices, _engine);

        // Generative beat bed (default mood: Focused).
        var spec = SignalsToBeat.Of(DefaultSignals, BeatPreset.Focused);
        _beat = new BeatSequencer(AudioEngine.InternalRate, spec);

        BuildUi();

        _engine.SetBedProvider(_beat);
        // Fixed internal headroom. The user controls loudness through Windows (WASAPI shared mode
        // means CodeKeys is its own entry in the system volume mixer) — no separate in-app slider.
        _engine.MasterVolume = 0.85f;
        _engine.Start();

        _hook.KeyDown += OnHookKeyDown;
        _hook.KeyUp += OnHookKeyUp;

        // Refresh the beat from live typing every few seconds (applied at the next loop boundary).
        _signalTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _signalTimer.Tick += OnSignalTick;
        _signalTimer.Start();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _hook.Install(); // install once the message loop is running
    }

    private void OnHookKeyDown(int vk)
    {
        if (vk is VirtualKey.ShiftL or VirtualKey.ShiftR or VirtualKey.Shift) _shiftDown = true;

        // Capture typing signals (category + timing only — never the character).
        var kind = KeyClassifier.Classify(vk);
        bool isUpper = kind == KeyKind.Letter && (_shiftDown ^ CapsLockOn());
        _signals.Record(_clock.ElapsedMilliseconds, kind, isUpper);

        if (_keystrokes.OnKeyDown(vk))
            _status.Text = $"♪ {Describe(vk)}";
    }

    private void OnHookKeyUp(int vk)
    {
        if (vk is VirtualKey.ShiftL or VirtualKey.ShiftR or VirtualKey.Shift) _shiftDown = false;
        _keystrokes.OnKeyUp(vk);
    }

    private void OnSignalTick(object? sender, EventArgs e)
    {
        // Feed the conductor the latest typing arousal; it steers the beat gently at the next loop
        // boundary. The mood (scale/root/tempo range) only changes via the Mood picker → SetSpec.
        _beat.Observe(Conductor.Estimate(_signals.Snapshot()));
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private static bool CapsLockOn() => (GetKeyState(VirtualKey.CapsLock) & 1) != 0;

    private static string Describe(int vk) => vk switch
    {
        VirtualKey.Space => "Space",
        VirtualKey.Enter => "Enter",
        VirtualKey.Back => "Backspace",
        >= 'A' and <= 'Z' => ((char)vk).ToString(),
        >= '0' and <= '9' => ((char)vk).ToString(),
        _ => $"key {vk}"
    };

    private void BuildUi()
    {
        Text = "CodeKeys";
        ClientSize = new Size(440, 340);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;

        var heading = new Label
        {
            Text = "Type in any app — you'll hear it. This panel can stay minimized.",
            Dock = DockStyle.Top,
            Padding = new Padding(14, 14, 14, 6),
            Height = 42
        };

        var presetLabel = new Label { Text = "Sound", AutoSize = true, Left = 16, Top = 56 };
        _presetPicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 70,
            Top = 52,
            Width = 250
        };
        foreach (var preset in PresetLibrary.All)
            _presetPicker.Items.Add(preset);
        _presetPicker.SelectedItem = PresetLibrary.Default;
        _presetPicker.SelectedIndexChanged += OnPresetChanged;

        _keysToggle = new CheckBox { Text = "⌨  Keystrokes", Checked = true, AutoSize = true, Left = 16, Top = 92 };
        _keysToggle.CheckedChanged += (_, _) => _keystrokes.Enabled = _keysToggle.Checked;

        _bedToggle = new CheckBox { Text = "🥁  Beat", Checked = false, AutoSize = true, Left = 16, Top = 124 };
        _bedToggle.CheckedChanged += (_, _) =>
        {
            _engine.BedEnabled = _bedToggle.Checked;
            // The cycle clock advances on the audio thread regardless of mute state, so without a
            // reset, toggling Beat on minutes after launch would land mid-fall or post-cycle (=
            // silent). Restart from the build's beginning every time it's enabled.
            if (_bedToggle.Checked) _beat.Reset();
        };

        // Locked to the Focused mood for now; other beat options hidden to keep this focused.
        // Dev aid: compress the build clock 20× so the arc is auditionable in seconds.
        var demoToggle = new CheckBox { Text = "⚡  Demo (fast)", Checked = false, AutoSize = true, Left = 130, Top = 124 };
        demoToggle.CheckedChanged += (_, _) => _beat.TimeScale = demoToggle.Checked ? 20.0 : 1.0;

        // Restart the breathing cycle at silence without toggling Beat off/on.
        var resetButton = new Button { Text = "↺  Reset beat", AutoSize = true, Left = 260, Top = 122 };
        resetButton.Click += (_, _) => _beat.Reset();

        // Per-layer levels (the relative mix). The master is the Windows volume mixer entry —
        // these knobs let Mike dial keystrokes vs the beat against each other without leaving
        // the system master out of his hands.
        var keysVolLabel = new Label { Text = "⌨  Keystrokes level", AutoSize = true, Left = 16, Top = 162 };
        var keysVolSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = (int)Math.Round(_engine.KeysLevel * 100),
            TickFrequency = 25,
            Width = 408,
            Left = 14,
            Top = 182
        };
        keysVolSlider.ValueChanged += (_, _) => _engine.KeysLevel = keysVolSlider.Value / 100f;

        var beatVolLabel = new Label { Text = "🥁  Beat level", AutoSize = true, Left = 16, Top = 230 };
        var beatVolSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = (int)Math.Round(_engine.BedLevel * 100),
            TickFrequency = 25,
            Width = 408,
            Left = 14,
            Top = 250
        };
        beatVolSlider.ValueChanged += (_, _) => _engine.BedLevel = beatVolSlider.Value / 100f;

        var volHint = new Label
        {
            Text = "🔊  Overall volume follows Windows — these sliders adjust the relative mix.",
            AutoSize = false,
            Left = 16,
            Top = 296,
            Width = 408,
            Height = 18,
            ForeColor = SystemColors.GrayText
        };

        _status = new Label
        {
            Text = "ready",
            AutoSize = false,
            Left = 330,
            Top = 54,
            Width = 94,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = SystemColors.Highlight,
            Font = new Font("Cascadia Mono", 10f)
        };

        var stamp = new Label
        {
            Text = BuildInfo.Full,
            Dock = DockStyle.Bottom,
            Height = 20,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 12, 0),
            ForeColor = SystemColors.GrayText
        };

        Controls.Add(presetLabel);
        Controls.Add(_presetPicker);
        Controls.Add(_keysToggle);
        Controls.Add(_bedToggle);
        Controls.Add(demoToggle);
        Controls.Add(resetButton);
        Controls.Add(keysVolLabel);
        Controls.Add(keysVolSlider);
        Controls.Add(beatVolLabel);
        Controls.Add(beatVolSlider);
        Controls.Add(volHint);
        Controls.Add(_status);
        Controls.Add(heading);
        Controls.Add(stamp);
    }

    private void OnPresetChanged(object? sender, EventArgs e)
    {
        if (_presetPicker.SelectedItem is not Preset preset) return;
        var baked = preset.Build(AudioEngine.InternalRate);
        _keystrokes.SetVoices(baked.Map, baked.Voices);
        _status.Text = "ready";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _signalTimer?.Stop();
        _signalTimer?.Dispose();
        _hook.Dispose();
        _engine.Dispose();
        base.OnFormClosed(e);
    }
}
