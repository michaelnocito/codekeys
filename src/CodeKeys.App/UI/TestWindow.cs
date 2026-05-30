using CodeKeys.App.Audio;
using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Music;
// `Scale` alias: WinForms Control has a Scale() method that shadows the type name here.
using MusicScale = CodeKeys.Core.Music.Scale;

namespace CodeKeys.App.UI;

/// <summary>
/// Standalone proving-ground for the audio engine (build-order step 3). Type in the
/// box to hear the keystroke layer; toggle the ambient bed; ride the master volume.
/// No global hook yet — this validates both layers and latency in isolation first.
/// </summary>
public sealed class TestWindow : Form
{
    private readonly AudioEngine _engine = new();
    private readonly SpatialKeyMap _map;
    private readonly KeyVoiceSet _voices;

    // Track held keys so OS auto-repeat doesn't machine-gun the same note.
    private readonly HashSet<int> _down = new();

    private TextBox _typeBox = null!;
    private CheckBox _keysToggle = null!;
    private CheckBox _bedToggle = null!;
    private TrackBar _volume = null!;

    public TestWindow()
    {
        // "Melody" voice set: C major pentatonic, warm tone, spanning 2 octaves up from C3.
        _map = new SpatialKeyMap(MusicScale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);
        _voices = KeyVoiceSet.BakeSynth(_map, AudioEngine.InternalRate, Waveform.WarmPad, Envelope.Pluck);

        BuildUi();

        _engine.SetBed(AmbientBedFactory.BrownNoise(AudioEngine.InternalRate));
        _engine.MasterVolume = 0.8f;
        _engine.Start();
    }

    private void BuildUi()
    {
        Text = "CodeKeys — Audio Test";
        ClientSize = new Size(560, 360);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        var heading = new Label
        {
            Text = "Type below to hear the keystroke layer. Toggle the ambient bed underneath.",
            Dock = DockStyle.Top,
            Padding = new Padding(12, 12, 12, 4),
            Height = 40
        };

        _typeBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Cascadia Mono", 11f, FontStyle.Regular),
            Margin = new Padding(12)
        };
        _typeBox.KeyDown += OnTypeKeyDown;
        _typeBox.KeyUp += OnTypeKeyUp;

        var typePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4) };
        typePanel.Controls.Add(_typeBox);

        // --- Controls row ---
        _keysToggle = new CheckBox { Text = "⌨ Keystrokes", Checked = true, AutoSize = true, Left = 12, Top = 10 };
        _keysToggle.CheckedChanged += (_, _) => _engine.KeysEnabled = _keysToggle.Checked;

        _bedToggle = new CheckBox { Text = "🔊 Ambient bed", Checked = false, AutoSize = true, Left = 130, Top = 10 };
        _bedToggle.CheckedChanged += (_, _) => _engine.BedEnabled = _bedToggle.Checked;

        var volLabel = new Label { Text = "Master", AutoSize = true, Left = 270, Top = 12 };
        _volume = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 80,
            TickFrequency = 25,
            Width = 180,
            Left = 320,
            Top = 4
        };
        _volume.ValueChanged += (_, _) => _engine.MasterVolume = _volume.Value / 100f;

        var controls = new Panel { Dock = DockStyle.Bottom, Height = 48 };
        controls.Controls.AddRange(new Control[] { _keysToggle, _bedToggle, volLabel, _volume });

        var stamp = new Label
        {
            Text = BuildInfo.Full,
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 12, 0),
            ForeColor = SystemColors.GrayText
        };

        Controls.Add(typePanel);
        Controls.Add(heading);
        Controls.Add(controls);
        Controls.Add(stamp);
    }

    private void OnTypeKeyDown(object? sender, KeyEventArgs e)
    {
        int vk = e.KeyValue;
        if (!_down.Add(vk)) return; // already held — ignore auto-repeat

        var sound = _map.Resolve(vk);
        var buffer = _voices.Resolve(sound);
        if (buffer != null) _engine.PlayVoice(buffer);
    }

    private void OnTypeKeyUp(object? sender, KeyEventArgs e) => _down.Remove(e.KeyValue);

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _engine.Dispose();
        base.OnFormClosed(e);
    }
}
