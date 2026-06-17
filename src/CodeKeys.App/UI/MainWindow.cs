using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using CodeKeys.App.Audio;
using CodeKeys.App.Input;
using CodeKeys.App.UI.Controls;
using CodeKeys.Core.Beat;
using CodeKeys.Core.Input;
using CodeKeys.Core.Presets;

namespace CodeKeys.App.UI;

/// <summary>
/// Bowl Bass Keys control panel. Keystroke sound comes from the system-wide hook (type in any app),
/// and an optional generative bed of singing bowls + deep bass plays underneath, locked to the same
/// scale so it never clashes. Styled like a phone settings screen — a single column of soft cards on
/// white, matching michaelnocito.github.io (charcoal text, one blue accent, generous whitespace).
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

    private ToggleSwitch _keysToggle = null!;
    private ToggleSwitch _bedToggle = null!;
    private ToggleSwitch _livingToggle = null!;
    private ToggleSwitch _demoToggle = null!;
    private FlatSlider _keysVol = null!;
    private FlatSlider _beatVol = null!;
    private ComboBox _voicePicker = null!;
    private ComboBox _chakraPicker = null!;
    private Label _status = null!;
    private Button _updateBtn = null!;

    // ---- palette (matches the site) ----
    private static readonly Color Charcoal = Color.FromArgb(28, 28, 30);
    private static readonly Color Gray = Color.FromArgb(120, 120, 130);
    private static readonly Color Accent = Color.FromArgb(10, 90, 200);
    private static readonly Color Divider = Color.FromArgb(238, 238, 242);

    // ---- layout grid ----
    private const int CardX = 24;
    private const int CardW = 356;

    // Representative typing signals. NOTE: Text is intentionally left empty — Bowl Bass Keys never
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
        var baked = CodeKeys.Core.Presets.PresetLibrary.Default.Build(AudioEngine.InternalRate);
        _keystrokes = new KeystrokeController(baked.Map, baked.Voices, _engine);

        // Default beat = Dreamflow (the new flowing 90s-new-age bed — first in the picker).
        var spec = SignalsToBeat.Of(DefaultSignals, BeatPreset.Dreamflow);
        _beat = new BeatSequencer(AudioEngine.InternalRate, spec);

        BuildUi();

        _engine.SetBedProvider(_beat);
        _engine.MasterVolume = 0.85f;     // fixed headroom; loudness is the Windows mixer's job
        _engine.BedEnabled = true;        // both layers on at startup
        _beat.Reset();
        _engine.Start();

        _hook.KeyDown += OnHookKeyDown;
        _hook.KeyUp += OnHookKeyUp;

        _signalTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _signalTimer.Tick += OnSignalTick;
        _signalTimer.Start();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _hook.Install();
    }

    private void OnHookKeyDown(int vk)
    {
        if (vk is VirtualKey.ShiftL or VirtualKey.ShiftR or VirtualKey.Shift) _shiftDown = true;

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
        // boundary (and drives living events when that channel is on).
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

    private async void OnCheckForUpdates(object? sender, EventArgs e)
    {
        _updateBtn.Enabled = false;
        _updateBtn.Text = "Checking…";
        try
        {
            var info = await UpdaterService.CheckAsync();
            if (info is null)
            {
                _updateBtn.Text = "Up to date ✓";
                await Task.Delay(2500);
                _updateBtn.Text = "Check for Updates";
                _updateBtn.Enabled = true;
                return;
            }

            var notes = string.IsNullOrWhiteSpace(info.Notes)
                ? ""
                : $"\n\nWhat's new:\n{info.Notes.Trim()}";
            var result = MessageBox.Show(
                $"Version {info.Version} is available.{notes}\n\nDownload and restart now?",
                "Update Available",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result != DialogResult.Yes)
            {
                _updateBtn.Text = "Check for Updates";
                _updateBtn.Enabled = true;
                return;
            }

            var progress = new Progress<int>(pct =>
            {
                _updateBtn.Text = $"Downloading… {pct}%";
            });
            await UpdaterService.DownloadAndApplyAsync(info, progress);
        }
        catch (Exception ex)
        {
            _updateBtn.Text = "Check for Updates";
            _updateBtn.Enabled = true;
            MessageBox.Show($"Update check failed: {ex.Message}", "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void BuildUi()
    {
        Text = "Bowl Bass Keys";
        ClientSize = new Size(404, 924);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        BackColor = Color.White;
        ForeColor = Charcoal;

        // ---- header ----
        Controls.Add(new Label
        {
            Text = "Bowl Bass Keys",
            Font = new Font("Segoe UI Semibold", 19f, FontStyle.Regular),
            ForeColor = Charcoal,
            AutoSize = true,
            Left = CardX, Top = 26,
        });
        Controls.Add(new Label
        {
            Text = "relax as you type  ·  space clearing  ·  chakra vibing",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Gray,
            AutoSize = true,
            Left = CardX + 2, Top = 64,
        });

        // ---- SOUND ----
        Controls.Add(SectionLabel("SOUND", 100));
        var sound = Card(120, 104);
        _keysToggle = Row(sound, top: 0, "Keystrokes", "sound on every key, system-wide", out _);
        sound.Controls.Add(RowDivider(52));
        _bedToggle = Row(sound, top: 52, "Beat", "the generative bowls + bass bed", out _);
        _keysToggle.Checked = true;
        _bedToggle.Checked = true;
        _keysToggle.CheckedChanged += (_, _) => _keystrokes.Enabled = _keysToggle.Checked;
        _bedToggle.CheckedChanged += (_, _) =>
        {
            _engine.BedEnabled = _bedToggle.Checked;
            if (_bedToggle.Checked) _beat.Reset();
        };

        // ---- KEYSTROKE SOUND (the keystroke voicing packs; the beat is the same across all) ----
        Controls.Add(SectionLabel("KEYSTROKE SOUND", 236));
        var voiceCard = Card(256, 62);
        _voicePicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Charcoal,
            Left = 16, Top = 16, Width = CardW - 32,
        };
        _voicePicker.Items.AddRange(PresetLibrary.All.ToArray());
        _voicePicker.SelectedIndex = 0; // Deep & Warm (default)
        _voicePicker.SelectedIndexChanged += OnVoiceChanged;
        voiceCard.Controls.Add(_voicePicker);

        // ---- TEMPLATE ----
        Controls.Add(SectionLabel("BEAT TEMPLATE", 332));
        var template = Card(352, 62);
        _chakraPicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10.5f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Charcoal,
            Left = 16, Top = 16, Width = CardW - 32,
        };
        _chakraPicker.Items.AddRange(new object[]
        {
            new ChakraOption(BeatPreset.Dreamflow,     "Dreamflow  ·  90s new age flow"),
            new ChakraOption(BeatPreset.CodeGroove,    "Code Groove  ·  lo-fi coding beat"),
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
        _chakraPicker.SelectedIndex = 0; // Dreamflow (default — the new flowing bed)
        _chakraPicker.SelectedIndexChanged += OnChakraChanged;
        template.Controls.Add(_chakraPicker);

        // ---- MIX ----
        Controls.Add(SectionLabel("MIX", 428));
        var mix = Card(448, 160);
        mix.Controls.Add(MixLabel("Keystrokes", 16));
        _keysVol = new FlatSlider { Left = 16, Top = 44, Width = CardW - 32, Value = (int)Math.Round(_engine.KeysLevel * 100) };
        mix.Controls.Add(_keysVol);
        mix.Controls.Add(RowDivider(86));
        mix.Controls.Add(MixLabel("Beat", 100));
        _beatVol = new FlatSlider { Left = 16, Top = 128, Width = CardW - 32, Value = (int)Math.Round(_engine.BedLevel * 100) };
        mix.Controls.Add(_beatVol);
        _keysVol.ValueChanged += (_, _) => _engine.KeysLevel = _keysVol.Value / 100f;
        _beatVol.ValueChanged += (_, _) =>
        {
            _engine.BedLevel = _beatVol.Value / 100f;
            int autoKeys = _beatVol.Value / 2;       // keys auto-track at half the beat level
            if (_keysVol.Value != autoKeys) _keysVol.Value = autoKeys;
        };

        // ---- FLOW (extras) ----
        Controls.Add(SectionLabel("FLOW", 620));
        var flow = Card(640, 116);
        _livingToggle = Row(flow, top: 0, "Living events", "soft accents that react to your typing flow", out _);
        flow.Controls.Add(RowDivider(64));
        _demoToggle = Row(flow, top: 64, "Demo", "fast-forward the build to hear it quickly", out _);
        _livingToggle.CheckedChanged += (_, _) => _beat.LivingEventsEnabled = _livingToggle.Checked;
        _demoToggle.CheckedChanged += (_, _) => _beat.TimeScale = _demoToggle.Checked ? 20.0 : 1.0;

        // ---- restart ----
        var restart = new Button
        {
            Text = "Restart beat",
            Font = new Font("Segoe UI", 10f),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Accent,
            BackColor = Color.White,
            Left = CardX, Top = 772, Width = CardW, Height = 42,
        };
        restart.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 232);
        restart.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 248, 253);
        restart.Click += (_, _) => _beat.Reset();
        Controls.Add(restart);

        // ---- update button ----
        _updateBtn = new Button
        {
            Text = "Check for Updates",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Accent,
            BackColor = Color.White,
            Left = CardX, Top = 826, Width = CardW, Height = 42,
        };
        _updateBtn.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 232);
        _updateBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 248, 253);
        _updateBtn.Click += OnCheckForUpdates;
        Controls.Add(_updateBtn);

        // ---- footer ----
        Controls.Add(new Label
        {
            Text = "Overall volume follows Windows.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Gray,
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Left = CardX, Top = 880, Width = CardW, Height = 16,
        });
        Controls.Add(new Label
        {
            Text = BuildInfo.Full,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(170, 170, 178),
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Left = CardX, Top = 898, Width = CardW, Height = 14,
        });

        // hidden status (keystroke-debug telemetry)
        _status = new Label { Visible = false, Left = 0, Top = 0, Width = 1, Height = 1 };
        Controls.Add(_status);
    }

    // ---- small UI builders ----

    private static Label SectionLabel(string text, int top) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 8f, FontStyle.Bold),
        ForeColor = Gray,
        AutoSize = true,
        Left = CardX + 6, Top = top,
    };

    private CardPanel Card(int top, int height)
    {
        var c = new CardPanel { Left = CardX, Top = top, Width = CardW, Height = height };
        Controls.Add(c);
        return c;
    }

    /// <summary>A settings row inside a card: title (+ optional subtitle) on the left, a toggle on the right.</summary>
    private ToggleSwitch Row(CardPanel card, int top, string title, string subtitle, out Label titleLabel)
    {
        titleLabel = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = Charcoal,
            AutoSize = true,
            Left = 18, Top = top + (subtitle.Length > 0 ? 13 : 18),
        };
        card.Controls.Add(titleLabel);
        if (subtitle.Length > 0)
            card.Controls.Add(new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Gray,
                AutoSize = true,
                Left = 18, Top = top + 34,
            });
        var toggle = new ToggleSwitch { OnColor = Accent, Left = CardW - 16 - 48, Top = top + 14 };
        card.Controls.Add(toggle);
        return toggle;
    }

    private static Label MixLabel(string text, int top) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 10f),
        ForeColor = Charcoal,
        AutoSize = true,
        Left = 18, Top = top,
    };

    private static Panel RowDivider(int top) => new()
    {
        Left = 18, Top = top, Width = CardW - 36, Height = 1,
        BackColor = Divider,
    };

    /// <summary>An item in the template picker — pairs a display label with the underlying preset.</summary>
    private sealed record ChakraOption(BeatPreset Preset, string Label)
    {
        public override string ToString() => Label;
    }

    private void OnVoiceChanged(object? sender, EventArgs e)
    {
        if (_voicePicker.SelectedItem is not Preset preset) return;
        // Swap the keystroke voicing live — bakes the new pack and hands it to the controller.
        // The beat (bowls + bass) is untouched.
        var baked = preset.Build(AudioEngine.InternalRate);
        _keystrokes.SetVoices(baked.Map, baked.Voices);
    }

    private void OnChakraChanged(object? sender, EventArgs e)
    {
        if (_chakraPicker.SelectedItem is not ChakraOption opt) return;
        // SetSpec restarts the build from silence, so the chosen template eases in cleanly.
        _beat.SetSpec(SignalsToBeat.Of(DefaultSignals, opt.Preset));
        if (_bedToggle.Checked) _beat.Reset();
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
