using CodeKeys.Core.Audio;
using CodeKeys.Core.Beat;
using CodeKeys.Core.Music;
using NAudio.Wave;

namespace CodeKeys.App.Audio;

/// <summary>
/// The generative beat renderer (module 3): turns a <see cref="BeatSpec"/> into a looping,
/// evolving bed. It's a sample-clocked sequencer — on each step it spawns pre-baked voice
/// buffers per the <see cref="BeatPattern"/> timeline and mixes the ringing voices. At the end
/// of each loop it hands the spec to the <see cref="Conductor"/>, which gently steers tempo /
/// density toward a flow band (from the latest typing arousal) and advances the session arc.
///
/// All pitches come from the spec's scale/root, so the bed is always consonant with the keys.
/// Voices are pre-baked per spec (a full scale bank), so loop turnover never synthesizes on the
/// audio thread. The conductor only moves bpm/density/layers (never scale/root), so loop turnover
/// reuses the baked bank — no audio-thread synthesis, no clicks.
/// </summary>
public sealed class BeatSequencer : ISampleProvider
{
    private readonly struct Scheduled
    {
        public readonly long Offset;
        public readonly float[] Data;
        public readonly float Gain;
        public Scheduled(long offset, float[] data, float gain) { Offset = offset; Data = data; Gain = gain; }
    }

    private struct ActiveVoice
    {
        public float[] Data;
        public int Pos;
        public float Gain;
    }

    public WaveFormat WaveFormat { get; }

    private readonly int _rate;
    private readonly object _gate = new();
    private readonly Dictionary<(BeatLayer, int), float[]> _bank = new();
    private readonly List<ActiveVoice> _active = new();

    private BeatSpec _spec = null!; // set in ctor via SetSpec
    private double _userArousal = 0.5;  // latest typing arousal (0..1); updated live via Observe
    private long _sessionSamples;       // samples since the session (mood) started → arc clock
    private int _loopCount;             // loop index → seeds the per-loop back-beat variation
    private double _timeScale = 1.0;    // compresses the build clock for quick auditioning (1 = real time)
    private double _sensitivity = 1.25; // user reactivity multiplier (1 = baseline; default +25%)
    private float _buildGain = 0.25f;   // additive build's output gain — 0.25 (quiet, not silent) → 1.0
    private double _noteFill = 0.0;     // note-fill factor passed to BeatPattern — 0 (sparse) → 1 (full)
    private Scheduled[] _schedule = Array.Empty<Scheduled>();
    private long _loopLen = 1;
    private long _playhead;
    private int _nextIdx;

    public BeatSequencer(int sampleRate, BeatSpec spec)
    {
        _rate = sampleRate;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        SetSpec(spec);
    }

    /// <summary>
    /// Swap to a new beat live. Re-bakes the voice bank and restarts the additive build from
    /// silence so the new spec eases in (one tapper, then voices joining) instead of slamming in
    /// at full texture.
    /// </summary>
    public void SetSpec(BeatSpec spec)
    {
        lock (_gate)
        {
            var (lo, hi) = SignalsToBeat.BpmRange(spec.Preset);
            // dt = 0 + elapsedSeconds = 0 → tempo unchanged; just applies the build's opening
            // (Pulse only, near-silent) so the texture starts from the bottom of the curve.
            _spec = Conductor.Step(spec, _userArousal, elapsedSeconds: 0, dtSeconds: 0, lo, hi, _sensitivity);
            double e0 = Conductor.CycleEnvelope(0);
            _buildGain = (float)(0.25 + 0.75 * e0);
            _noteFill = e0;
            _sessionSamples = 0;
            _loopCount = 0;
            BakeBank(_spec);
            BuildSchedule();
            _playhead = 0;
            _nextIdx = 0;
            _active.Clear();
        }
    }

    /// <summary>
    /// Restart the breathing cycle from the very beginning (silent → rise → peak → fall → repeat),
    /// without rebaking the voice bank. The bound user "Reset" button calls this so a fresh build
    /// can be heard without toggling Beat off/on.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _sessionSamples = 0;
            _loopCount = 0;
            var (lo, hi) = SignalsToBeat.BpmRange(_spec.Preset);
            _spec = Conductor.Step(_spec, _userArousal, elapsedSeconds: 0, dtSeconds: 0, lo, hi, _sensitivity);
            double e0 = Conductor.CycleEnvelope(0);
            _buildGain = (float)(0.25 + 0.75 * e0);
            _noteFill = e0;
            BuildSchedule();
            _playhead = 0;
            _nextIdx = 0;
            _active.Clear();
        }
    }

    /// <summary>
    /// Live typing update: record the latest arousal estimate (0..1). The conductor reads it at the
    /// next loop boundary and nudges the groove gently — no rebake, no restart. Mood changes go
    /// through <see cref="SetSpec"/> instead (they change scale/root and need a rebake).
    /// </summary>
    public void Observe(double arousal)
    {
        double a = Math.Min(1.0, Math.Max(0.0, arousal));
        // Smooth toward the new reading (on top of the 30s signals window) so the conductor follows
        // a settled trend, never a single snapshot — deliberately not hyper-responsive.
        lock (_gate) _userArousal += (a - _userArousal) * 0.25;
    }

    /// <summary>
    /// Compress the build clock for quick auditioning (1 = real time; e.g. 20 = 20× faster).
    /// Only the build clock is scaled — NOT the moment-to-moment arousal ramp — so you can hear
    /// the whole ~10-min assembly in seconds.
    /// </summary>
    public double TimeScale
    {
        get { lock (_gate) return _timeScale; }
        set { lock (_gate) _timeScale = Math.Max(1.0, value); }
    }

    /// <summary>
    /// How fast the beat reacts to typing (1 = baseline). Higher = snappier / less gradual, lower =
    /// calmer. The arousal response is also gated by the build envelope, so early on this knob
    /// barely matters; it takes effect once the build is well under way.
    /// </summary>
    public double Sensitivity
    {
        get { lock (_gate) return _sensitivity; }
        set { lock (_gate) _sensitivity = Math.Max(0.0, value); }
    }

    private void BakeBank(BeatSpec spec)
    {
        _bank.Clear();
        var scale = SignalsToBeat.ToScale(spec.Scale);
        int root = NoteUtil.ParseNoteName(spec.Root);
        int span = scale.DegreeSpan(2);

        void Put(BeatLayer layer, int midi)
        {
            var key = (layer, midi);
            if (!_bank.ContainsKey(key)) _bank[key] = Bake(layer, midi);
        }

        // Bake every voice the arc might enable (regardless of which layers are active right now),
        // so a layer turning on mid-session never synthesizes on the audio thread.
        Put(BeatLayer.Pulse, root);
        Put(BeatLayer.Ghost, root + 24);
        foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Pad, scale.DegreeToMidi(root, deg));      // chord (unused)
        foreach (int deg in new[] { 0, 4 }) Put(BeatLayer.Bass, scale.DegreeToMidi(root - 12, deg));   // deep low boom
        foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Splash, scale.DegreeToMidi(root, deg));   // rare dark splash
        foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Chime, scale.DegreeToMidi(root + 24, deg)); // high sparkle (unused)
        for (int d = 0; d < span; d++)
        {
            int midi = scale.DegreeToMidi(root + 12, d);
            Put(BeatLayer.Marimba, midi);
            Put(BeatLayer.Melody, midi);
        }
    }

    private float[] Bake(BeatLayer layer, int midi)
    {
        double f = NoteUtil.MidiToFrequency(midi);
        SampleBuffer buf = layer switch
        {
            // Atmospheric pulse — a pure deep sine with a soft attack and long sustain (no pitch
            // drop, no click). Reads as a warm "hummmm" rather than a driving kick — gentler in the
            // mix, less of a melodic-feeling transient pulling at attention.
            BeatLayer.Pulse => PercussionFactory.CreateSub(f, _rate, decaySeconds: 0.55, gain: 0.55f),
            BeatLayer.Pad => SynthVoiceFactory.CreateTone(f, _rate, Waveform.WarmPad,
                                new Envelope { Attack = 0.06, Decay = 0.5, Sustain = 0.6, Release = 0.9 },
                                holdSeconds: 1.2, gain: 0.35f),
            BeatLayer.Marimba => InstrumentFactory.CreateMarimba(f, _rate),
            // Deep low boom — pure sine, long resonant decay (the "boooommm"). Sits ~73 Hz, above the
            // ~45 Hz floor so it stays audible/clean and not physically intrusive.
            BeatLayer.Bass => SynthVoiceFactory.CreateTone(f, _rate, Waveform.Sine,
                                new Envelope { Attack = 0.005, Decay = 1.8, Sustain = 0.0, Release = 0.3 },
                                holdSeconds: 0.0, gain: 0.60f),
            // Splash — a rare, dark, soft mid-low appearance. Slow attack (no sharp transient) and
            // no high content, so it adds variety without capturing attention (per the research).
            BeatLayer.Splash => SynthVoiceFactory.CreateTone(f, _rate, Waveform.WarmPad,
                                new Envelope { Attack = 0.05, Decay = 0.5, Sustain = 0.3, Release = 0.6 },
                                holdSeconds: 0.25, gain: 0.32f),
            // Soft, ambient melody — gentle fade-in + long tail so it floats behind the work instead
            // of plucking to the front (same WarmPad tone Mike likes, just sat well back).
            BeatLayer.Melody => SynthVoiceFactory.CreateTone(f, _rate, Waveform.WarmPad,
                                new Envelope { Attack = 0.03, Decay = 0.25, Sustain = 0.4, Release = 0.7 },
                                holdSeconds: 0.22, gain: 0.30f),
            // Soft bell: pure sine with a long, clean decay — a delicate high sparkle, sits well back.
            BeatLayer.Chime => SynthVoiceFactory.CreateTone(f, _rate, Waveform.Sine,
                                new Envelope { Attack = 0.002, Decay = 1.4, Sustain = 0.0, Release = 0.1 },
                                holdSeconds: 0.0, gain: 0.40f),
            BeatLayer.Ghost => PercussionFactory.CreateTap(f, _rate, decaySeconds: 0.045, noiseAmount: 0.25),
            _ => SynthVoiceFactory.CreateTone(f, _rate, Waveform.Sine, Envelope.Pluck)
        };
        return buf.Samples;
    }

    private void BuildSchedule()
    {
        var hits = BeatPattern.Build(_spec, _loopCount, _noteFill);
        int steps = _spec.LoopBars * 16;
        double samplesPerStep = 60.0 / _spec.Bpm / 4.0 * _rate;
        _loopLen = Math.Max(1, (long)(steps * samplesPerStep));

        var list = new List<Scheduled>(hits.Count);
        foreach (var h in hits)
        {
            if (!_bank.TryGetValue((h.Layer, h.Midi), out var data)) continue;
            long offset = (long)((h.Step + h.SwingFraction * 0.5) * samplesPerStep);
            if (offset >= _loopLen) offset = _loopLen - 1;
            list.Add(new Scheduled(offset, data, (float)h.Gain));
        }
        list.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        _schedule = list.ToArray();
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            for (int i = 0; i < count; i++)
            {
                // Spawn any hits whose time has arrived.
                while (_nextIdx < _schedule.Length && _playhead >= _schedule[_nextIdx].Offset)
                {
                    var s = _schedule[_nextIdx++];
                    _active.Add(new ActiveVoice { Data = s.Data, Pos = 0, Gain = s.Gain });
                }

                // Mix ringing voices (iterate backwards so finished ones can be removed).
                float sample = 0f;
                for (int v = _active.Count - 1; v >= 0; v--)
                {
                    var av = _active[v];
                    sample += av.Data[av.Pos] * av.Gain;
                    av.Pos++;
                    if (av.Pos >= av.Data.Length) _active.RemoveAt(v);
                    else _active[v] = av;
                }
                buffer[offset + i] = sample * _buildGain; // additive build's output gain (0.06 → 1.0)

                _sessionSamples++;

                if (++_playhead >= _loopLen)
                {
                    _playhead = 0;
                    _nextIdx = 0;
                    // Advance the build clock; the conductor runs it (voice entry + density + the
                    // gated arousal response). Build envelope drives output gain + note-fill so the
                    // first minutes are almost imperceptible by both volume and note count.
                    double elapsed = _sessionSamples / (double)_rate * _timeScale;
                    double dt = _loopLen / (double)_rate;
                    var (lo, hi) = SignalsToBeat.BpmRange(_spec.Preset);
                    _spec = Conductor.Step(_spec, _userArousal, elapsed, dt, lo, hi, _sensitivity);
                    double e = Conductor.CycleEnvelope(elapsed);
                    _buildGain = (float)(0.25 + 0.75 * e);
                    _noteFill = e;
                    _loopCount++;
                    BuildSchedule(); // reuses the baked bank (scale/root unchanged)
                }
            }
            return count;
        }
    }
}
