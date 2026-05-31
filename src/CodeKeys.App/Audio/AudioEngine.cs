using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace CodeKeys.App.Audio;

/// <summary>
/// The heart of CodeKeys: two independent audio layers mixed to the default device
/// through WASAPI shared mode (low latency, but never monopolizes the device, so
/// Slack/music keep playing).
///
///   • Keystroke layer — transient one-shot voices, polyphonic, capped at ~16 ringing
///     voices so fast typing layers instead of cutting (which prevents mud).
///   • Ambient bed layer — one endless seamless loop with its own gain.
///
/// A master volume / mute stage sits over both (the panic kill). Audible buffers are
/// pre-baked by Core; this class only mixes and streams them.
/// </summary>
public sealed class AudioEngine : IDisposable, IVoicePlayer
{
    public const int InternalRate = 44100;

    private readonly object _gate = new();

    private readonly MixingSampleProvider _keyMixer;   // transient key voices
    private readonly VolumeSampleProvider _keysVol;    // keystroke layer level (foreground feedback)
    private readonly MixingSampleProvider _master;     // keys + bed
    private readonly VolumeSampleProvider _masterVol;  // master volume / mute

    private IWavePlayer? _output;

    // Bed layer (ambient loop or generative beat)
    private VolumeSampleProvider? _bedVol;

    // Polyphony bookkeeping
    private readonly List<BufferSampleProvider> _voices = new();
    private long _voiceCounter;
    private int _voiceCap = 16;

    // State
    private float _masterVolume = 1.0f;
    // Keystroke layer = the foreground feedback the user triggers. Research: too-loud incidental
    // sound raises cognitive load/fatigue, so keep it moderate — clearly above the bed, not blaring.
    // 0.41 is the prior 0.55 lowered by 25% per Mike's testing baseline.
    private float _keysLevel = 0.41f;
    private float _bedLevel = 0.22f; // background bed sits ~8 dB under the keystroke layer (still well back)
    private bool _keysEnabled = true;
    private bool _bedEnabled = false;
    private bool _muted = false;

    public AudioEngine()
    {
        var mono = WaveFormat.CreateIeeeFloatWaveFormat(InternalRate, 1);

        _keyMixer = new MixingSampleProvider(mono) { ReadFully = true };
        _keyMixer.MixerInputEnded += OnVoiceEnded;

        _master = new MixingSampleProvider(mono) { ReadFully = true };
        _keysVol = new VolumeSampleProvider(_keyMixer) { Volume = _keysLevel };
        _master.AddMixerInput(_keysVol);

        _masterVol = new VolumeSampleProvider(_master) { Volume = _masterVolume };
    }

    /// <summary>Open the audio device and start streaming. Call once at startup.</summary>
    public void Start(int requestedLatencyMs = 30)
    {
        if (_output != null) return;

        ISampleProvider chain = new MonoToStereoSampleProvider(_masterVol);

        try
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            int deviceRate = device.AudioClient.MixFormat.SampleRate;

            if (deviceRate != InternalRate)
                chain = new WdlResamplingSampleProvider(chain, deviceRate);

            var wasapi = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, requestedLatencyMs);
            wasapi.Init(chain);
            wasapi.Play();
            _output = wasapi;
        }
        catch
        {
            // Fallback: plain WaveOut if WASAPI shared can't be initialized on this machine.
            var waveOut = new WaveOutEvent { DesiredLatency = Math.Max(60, requestedLatencyMs), NumberOfBuffers = 2 };
            waveOut.Init(new MonoToStereoSampleProvider(_masterVol));
            waveOut.Play();
            _output = waveOut;
        }
    }

    // ---- Keystroke layer ----

    /// <summary><see cref="IVoicePlayer"/> entry point — play a baked voice at unity gain.</summary>
    public void Play(SampleBuffer buffer) => PlayVoice(buffer);

    /// <summary>Play a single pre-baked key voice, respecting the polyphony cap and the layer toggle.</summary>
    public void PlayVoice(SampleBuffer buffer, float gain = 1f)
    {
        if (buffer.Length == 0) return;

        lock (_gate)
        {
            if (!_keysEnabled) return;

            // Cap polyphony: drop the oldest still-ringing voice before adding a new one.
            while (_voices.Count >= _voiceCap)
            {
                var oldest = _voices[0];
                _voices.RemoveAt(0);
                _keyMixer.RemoveMixerInput(oldest);
            }

            var voice = new BufferSampleProvider(buffer, ++_voiceCounter, gain);
            _voices.Add(voice);
            _keyMixer.AddMixerInput(voice);
        }
    }

    private void OnVoiceEnded(object? sender, SampleProviderEventArgs e)
    {
        lock (_gate)
        {
            if (e.SampleProvider is BufferSampleProvider v)
                _voices.Remove(v);
        }
    }

    // ---- Ambient bed layer ----

    /// <summary>Install (or replace) the bed layer from any provider (e.g. the beat sequencer).</summary>
    public void SetBedProvider(ISampleProvider provider)
    {
        lock (_gate)
        {
            if (_bedVol != null)
                _master.RemoveMixerInput(_bedVol);

            _bedVol = new VolumeSampleProvider(provider) { Volume = _bedEnabled && !_muted ? _bedLevel : 0f };
            _master.AddMixerInput(_bedVol);
        }
    }

    /// <summary>Install (or replace) the bed from a seamless loop buffer.</summary>
    public void SetBed(SampleBuffer bed) => SetBedProvider(new LoopSampleProvider(bed));

    // ---- Toggles & levels ----

    public bool KeysEnabled
    {
        get { lock (_gate) return _keysEnabled; }
        set { lock (_gate) _keysEnabled = value; }
    }

    public bool BedEnabled
    {
        get { lock (_gate) return _bedEnabled; }
        set { lock (_gate) { _bedEnabled = value; ApplyBedVolume(); } }
    }

    /// <summary>Ambient bed level relative to master (0..1). Default 0.6 — focus research favors quiet beds.</summary>
    public float BedLevel
    {
        get { lock (_gate) return _bedLevel; }
        set { lock (_gate) { _bedLevel = Math.Clamp(value, 0f, 1f); ApplyBedVolume(); } }
    }

    /// <summary>Keystroke layer level (0..1) — the foreground feedback, kept above the bed.</summary>
    public float KeysLevel
    {
        get { lock (_gate) return _keysLevel; }
        set { lock (_gate) { _keysLevel = Math.Clamp(value, 0f, 1f); _keysVol.Volume = _keysLevel; } }
    }

    /// <summary>Master output volume (0..1).</summary>
    public float MasterVolume
    {
        get { lock (_gate) return _masterVolume; }
        set { lock (_gate) { _masterVolume = Math.Clamp(value, 0f, 1f); ApplyMasterVolume(); } }
    }

    /// <summary>Global mute — the panic kill. Silences everything without losing state.</summary>
    public bool Muted
    {
        get { lock (_gate) return _muted; }
        set { lock (_gate) { _muted = value; ApplyMasterVolume(); ApplyBedVolume(); } }
    }

    private void ApplyMasterVolume() => _masterVol.Volume = _muted ? 0f : _masterVolume;

    private void ApplyBedVolume()
    {
        if (_bedVol != null)
            _bedVol.Volume = _bedEnabled && !_muted ? _bedLevel : 0f;
    }

    public void Dispose()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
    }
}
