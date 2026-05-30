using CodeKeys.App.Audio;
using CodeKeys.App.Input;
using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Presets;

namespace CodeKeys.App.UI;

/// <summary>
/// CodeKeys control panel (build step 4). All keystroke sound now comes from the
/// system-wide hook, so typing in ANY application makes sound — this window can sit
/// minimized. The ambient bed is parked: the toggle stays, but it's not the focus.
/// </summary>
public sealed class MainWindow : Form
{
    private readonly AudioEngine _engine = new();
    private readonly GlobalKeyboardHook _hook = new();
    private readonly KeystrokeController _keystrokes;

    private CheckBox _keysToggle = null!;
    private CheckBox _bedToggle = null!;
    private TrackBar _volume = null!;
    private Label _status = null!;
    private ComboBox _presetPicker = null!;

    public MainWindow()
    {
        // Start on the default preset (Pulse — the low beat).
        var baked = PresetLibrary.Default.Build(AudioEngine.InternalRate);
        _keystrokes = new KeystrokeController(baked.Map, baked.Voices, _engine);

        BuildUi();

        // Bed is parked but functional; default off.
        _engine.SetBed(AmbientBedFactory.BrownNoise(AudioEngine.InternalRate));
        _engine.MasterVolume = 0.8f;
        _engine.Start();

        _hook.KeyDown += OnHookKeyDown;
        _hook.KeyUp += vk => _keystrokes.OnKeyUp(vk);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _hook.Install(); // install once the message loop is running
    }

    private void OnHookKeyDown(int vk)
    {
        if (_keystrokes.OnKeyDown(vk))
            _status.Text = $"♪ {Describe(vk)}";
    }

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
        ClientSize = new Size(440, 246);
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

        _keysToggle = new CheckBox { Text = "⌨  Keystrokes", Checked = true, AutoSize = true, Left = 16, Top = 96 };
        _keysToggle.CheckedChanged += (_, _) => _keystrokes.Enabled = _keysToggle.Checked;

        _bedToggle = new CheckBox { Text = "🔊  Ambient bed (parked)", Checked = false, AutoSize = true, Left = 180, Top = 96 };
        _bedToggle.CheckedChanged += (_, _) => _engine.BedEnabled = _bedToggle.Checked;

        var volLabel = new Label { Text = "Master volume", AutoSize = true, Left = 16, Top = 134 };
        _volume = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 80,
            TickFrequency = 25,
            Width = 400,
            Left = 14,
            Top = 154
        };
        _volume.ValueChanged += (_, _) => _engine.MasterVolume = _volume.Value / 100f;

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
        Controls.Add(volLabel);
        Controls.Add(_volume);
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
        _hook.Dispose();
        _engine.Dispose();
        base.OnFormClosed(e);
    }
}
