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
    private ComboBox _moodPicker = null!;

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
        ClientSize = new Size(440, 318);
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
        _bedToggle.CheckedChanged += (_, _) => _engine.BedEnabled = _bedToggle.Checked;

        var moodLabel = new Label { Text = "Mood", AutoSize = true, Left = 120, Top = 126 };
        _moodPicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 168,
            Top = 122,
            Width = 150
        };
        foreach (var mood in Enum.GetValues<BeatPreset>())
            _moodPicker.Items.Add(mood);
        _moodPicker.SelectedItem = BeatPreset.Focused;
        _moodPicker.SelectedIndexChanged += OnMoodChanged;

        // Dev/demo aid: compress the ~12-min beat build-up into seconds so it's auditionable.
        var demoToggle = new CheckBox { Text = "⚡  Demo build-up (fast arc)", Checked = false, AutoSize = true, Left = 16, Top = 150 };
        demoToggle.CheckedChanged += (_, _) => _beat.TimeScale = demoToggle.Checked ? 20.0 : 1.0;

        // Reactivity: how fast the beat follows your typing (maps to Conductor sensitivity).
        var reactLabel = new Label { Text = "Reactivity — how fast the beat follows your typing", AutoSize = true, Left = 16, Top = 182 };
        var reactSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 50,            // 50 → 1.25× (the +25% default); left = calmer, right = snappier
            TickFrequency = 25,
            Width = 408,
            Left = 14,
            Top = 202
        };
        reactSlider.ValueChanged += (_, _) => _beat.Sensitivity = 0.5 + reactSlider.Value / 100.0 * 1.5;

        var volHint = new Label
        {
            Text = "🔊  Volume follows Windows — adjust it from the taskbar volume / mixer.",
            AutoSize = false,
            Left = 16,
            Top = 256,
            Width = 408,
            Height = 34,
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
        Controls.Add(moodLabel);
        Controls.Add(_moodPicker);
        Controls.Add(demoToggle);
        Controls.Add(reactLabel);
        Controls.Add(reactSlider);
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

    private void OnMoodChanged(object? sender, EventArgs e)
    {
        if (_moodPicker.SelectedItem is not BeatPreset mood) return;
        _beat.SetSpec(SignalsToBeat.Of(DefaultSignals, mood));
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
