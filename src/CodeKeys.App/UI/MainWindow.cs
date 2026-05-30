using CodeKeys.App.Audio;
using CodeKeys.App.Input;
using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Music;
// `Scale` alias: WinForms Control has a Scale() method that shadows the type name here.
using MusicScale = CodeKeys.Core.Music.Scale;

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

    public MainWindow()
    {
        // "Melody" voice set: C major pentatonic, warm tone, 2 octaves up from C3.
        var map = new SpatialKeyMap(MusicScale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);
        var voices = KeyVoiceSet.BakeSynth(map, AudioEngine.InternalRate, Waveform.WarmPad, Envelope.Pluck);
        _keystrokes = new KeystrokeController(map, voices, _engine);

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
        ClientSize = new Size(440, 200);
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

        _keysToggle = new CheckBox { Text = "⌨  Keystrokes", Checked = true, AutoSize = true, Left = 16, Top = 56 };
        _keysToggle.CheckedChanged += (_, _) => _keystrokes.Enabled = _keysToggle.Checked;

        _bedToggle = new CheckBox { Text = "🔊  Ambient bed (parked)", Checked = false, AutoSize = true, Left = 16, Top = 84 };
        _bedToggle.CheckedChanged += (_, _) => _engine.BedEnabled = _bedToggle.Checked;

        var volLabel = new Label { Text = "Master volume", AutoSize = true, Left = 16, Top = 118 };
        _volume = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 80,
            TickFrequency = 25,
            Width = 400,
            Left = 14,
            Top = 138
        };
        _volume.ValueChanged += (_, _) => _engine.MasterVolume = _volume.Value / 100f;

        _status = new Label
        {
            Text = "ready",
            AutoSize = false,
            Left = 200,
            Top = 56,
            Width = 224,
            Height = 24,
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

        Controls.Add(_keysToggle);
        Controls.Add(_bedToggle);
        Controls.Add(volLabel);
        Controls.Add(_volume);
        Controls.Add(_status);
        Controls.Add(heading);
        Controls.Add(stamp);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _hook.Dispose();
        _engine.Dispose();
        base.OnFormClosed(e);
    }
}
