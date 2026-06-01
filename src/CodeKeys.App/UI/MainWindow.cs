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
    private ComboBox _chakraPicker = null!;

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

        // Default beat = Root chakra (Mike's current test baseline — paired with Midnight).
        var spec = SignalsToBeat.Of(DefaultSignals, BeatPreset.Root);
        _beat = new BeatSequencer(AudioEngine.InternalRate, spec);

        BuildUi();

        _engine.SetBedProvider(_beat);
        // Fixed internal headroom. The user controls loudness through Windows (WASAPI shared mode
        // means CodeKeys is its own entry in the system volume mixer) — no separate in-app slider.
        _engine.MasterVolume = 0.85f;
        // Both layers ON at startup at their current slider levels (keys 0.55, beat 0.22).
        _engine.BedEnabled = true;
        _beat.Reset();
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
        // Style: clean, minimal, high-contrast — matches michaelnocito.github.io. White background,
        // charcoal text, generous whitespace, sans-serif typography, almost no decoration.
        Text = "Bowl Bass Keys";
        ClientSize = new Size(500, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        BackColor = Color.White;
        ForeColor = Color.FromArgb(28, 28, 30);

        var charcoal = Color.FromArgb(28, 28, 30);
        var gray = Color.FromArgb(120, 120, 130);
        var accent = Color.FromArgb(10, 90, 200);

        // ---- Header: large title + tagline ----
        var title = new Label
        {
            Text = "Bowl Bass Keys",
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Regular),
            ForeColor = charcoal,
            AutoSize = true,
            Left = 28,
            Top = 22
        };

        var tagline = new Label
        {
            Text = "relax as you type/code  ·  space clearing  ·  chakra vibing",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = gray,
            AutoSize = true,
            Left = 30,
            Top = 60
        };

        var hr1 = new Panel
        {
            Left = 28, Top = 92, Width = 444, Height = 1,
            BackColor = Color.FromArgb(230, 230, 234)
        };

        // ---- Toggles ----
        _presetPicker = new ComboBox { Visible = false }; // dormant — kept for API compatibility

        _keysToggle = new CheckBox
        {
            Text = "Keystrokes",
            Font = new Font("Segoe UI", 10f),
            ForeColor = charcoal,
            Checked = true,
            AutoSize = true,
            Left = 28, Top = 110
        };
        _keysToggle.CheckedChanged += (_, _) => _keystrokes.Enabled = _keysToggle.Checked;

        _bedToggle = new CheckBox
        {
            Text = "Beat",
            Font = new Font("Segoe UI", 10f),
            ForeColor = charcoal,
            Checked = true,
            AutoSize = true,
            Left = 140, Top = 110
        };
        _bedToggle.CheckedChanged += (_, _) =>
        {
            _engine.BedEnabled = _bedToggle.Checked;
            if (_bedToggle.Checked) _beat.Reset();
        };

        var demoToggle = new CheckBox
        {
            Text = "Demo (fast)",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = gray,
            Checked = false,
            AutoSize = true,
            Left = 220, Top = 111
        };
        demoToggle.CheckedChanged += (_, _) => _beat.TimeScale = demoToggle.Checked ? 20.0 : 1.0;

        var resetButton = new Button
        {
            Text = "Reset beat",
            Font = new Font("Segoe UI", 9.5f),
            FlatStyle = FlatStyle.Flat,
            ForeColor = accent,
            BackColor = Color.White,
            AutoSize = true,
            Left = 340, Top = 108,
            Padding = new Padding(8, 2, 8, 2)
        };
        resetButton.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 228);
        resetButton.Click += (_, _) => _beat.Reset();

        // ---- Beat template picker ----
        var templateLabel = new Label
        {
            Text = "BEAT TEMPLATE",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = gray,
            AutoSize = true,
            Left = 28, Top = 158
        };

        _chakraPicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = charcoal,
            Left = 28, Top = 180, Width = 444
        };
        _chakraPicker.Items.AddRange(new object[]
        {
            new ChakraOption(BeatPreset.Focused,       "Tibetan Beat"),
            new ChakraOption(BeatPreset.Root,          "Root chakra  ·  396 Hz"),
            new ChakraOption(BeatPreset.Sacral,        "Sacral chakra  ·  417 Hz"),
            new ChakraOption(BeatPreset.SolarPlexus,   "Solar Plexus chakra  ·  528 Hz"),
            new ChakraOption(BeatPreset.Heart,         "Heart chakra  ·  639 Hz"),
            new ChakraOption(BeatPreset.Throat,        "Throat chakra  ·  741 Hz"),
            new ChakraOption(BeatPreset.ThirdEye,      "Third Eye chakra  ·  852 Hz"),
            new ChakraOption(BeatPreset.Crown,         "Crown chakra  ·  963 Hz"),
            new ChakraOption(BeatPreset.SpaceClearing, "Space Clearing  ·  432 Hz"),
            new ChakraOption(BeatPreset.ChakraSweep,   "Chakra Sweep  ·  21 min  ·  Root → Crown"),
        });
        _chakraPicker.SelectedIndex = 1;
        _chakraPicker.SelectedIndexChanged += OnChakraChanged;

        // ---- Mix levels ----
        var mixLabel = new Label
        {
            Text = "MIX",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = gray,
            AutoSize = true,
            Left = 28, Top = 234
        };

        var keysVolLabel = new Label
        {
            Text = "Keystrokes",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = charcoal,
            AutoSize = true,
            Left = 28, Top = 258
        };
        var keysVolSlider = new TrackBar
        {
            Minimum = 0, Maximum = 100,
            Value = (int)Math.Round(_engine.KeysLevel * 100),
            TickFrequency = 25,
            BackColor = Color.White,
            Width = 444, Left = 26, Top = 278
        };
        keysVolSlider.ValueChanged += (_, _) => _engine.KeysLevel = keysVolSlider.Value / 100f;

        var beatVolLabel = new Label
        {
            Text = "Beat",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = charcoal,
            AutoSize = true,
            Left = 28, Top = 330
        };
        var beatVolSlider = new TrackBar
        {
            Minimum = 0, Maximum = 100,
            Value = (int)Math.Round(_engine.BedLevel * 100),
            TickFrequency = 25,
            BackColor = Color.White,
            Width = 444, Left = 26, Top = 350
        };
        beatVolSlider.ValueChanged += (_, _) =>
        {
            _engine.BedLevel = beatVolSlider.Value / 100f;
            int autoKeys = beatVolSlider.Value / 2;
            if (keysVolSlider.Value != autoKeys) keysVolSlider.Value = autoKeys;
        };

        var volHint = new Label
        {
            Text = "Overall volume follows Windows.  Keys auto-track at half the beat level.",
            Font = new Font("Segoe UI", 8.5f),
            AutoSize = false,
            Left = 28, Top = 404, Width = 444, Height = 16,
            ForeColor = gray
        };

        // ---- Footer ----
        var hr2 = new Panel
        {
            Left = 28, Top = 432, Width = 444, Height = 1,
            BackColor = Color.FromArgb(230, 230, 234)
        };

        var stamp = new Label
        {
            Text = BuildInfo.Full,
            Font = new Font("Segoe UI", 8f),
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 28, 6),
            ForeColor = gray
        };

        // ---- Hidden status (kept for keystroke-debug telemetry) ----
        _status = new Label
        {
            Text = "",
            Visible = false,
            AutoSize = false,
            Left = 0, Top = 0, Width = 1, Height = 1
        };

        Controls.Add(title);
        Controls.Add(tagline);
        Controls.Add(hr1);
        Controls.Add(_keysToggle);
        Controls.Add(_bedToggle);
        Controls.Add(demoToggle);
        Controls.Add(resetButton);
        Controls.Add(templateLabel);
        Controls.Add(_chakraPicker);
        Controls.Add(mixLabel);
        Controls.Add(keysVolLabel);
        Controls.Add(keysVolSlider);
        Controls.Add(beatVolLabel);
        Controls.Add(beatVolSlider);
        Controls.Add(volHint);
        Controls.Add(hr2);
        Controls.Add(_status);
        Controls.Add(stamp);
    }

    /// <summary>An item in the chakra picker — pairs a display label with the underlying preset.</summary>
    private sealed record ChakraOption(BeatPreset Preset, string Label)
    {
        public override string ToString() => Label;
    }

    private void OnChakraChanged(object? sender, EventArgs e)
    {
        if (_chakraPicker.SelectedItem is not ChakraOption opt) return;
        // SetSpec restarts the additive build from silence, so the chosen chakra eases in cleanly.
        _beat.SetSpec(SignalsToBeat.Of(DefaultSignals, opt.Preset));
        if (_bedToggle.Checked) _beat.Reset();
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
