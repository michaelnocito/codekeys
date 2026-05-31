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
    private double _timeScale = 1.0;    // compresses the arc clock for quick auditioning (1 = real time)
    private double _sensitivity = 1.25; // user reactivity multiplier (1 = baseline; default +25%)
    private bool _buildup;              // "Buildup" mode: a slow ~10-min near-silent → full crescendo
    private float _buildupGain = 1.0f;  // output crescendo gain in buildup (1 = normal)
    private double _noteFill = 1.0;     // note-fill factor passed to BeatPattern (1 = full; <1 = sparse)
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
    /// Swap to a new beat (e.g. a different mood) live. Re-bakes the voice bank, restarts the
    /// session arc, and normalizes the spec to the arc's opening (sparse "establish" phase) so a
    /// new mood eases in rather than slamming in at full texture.
    /// </summary>
    public void SetSpec(BeatSpec spec)
    {
        lock (_gate)
        {
            var (lo, hi) = SignalsToBeat.BpmRange(spec.Preset);
            if (_buildup)
            {
                _spec = Conductor.BuildupSpec(spec, elapsedSeconds: 0, lo, hi);
                double e0 = Conductor.BuildupEnvelope(0);
                _buildupGain = (float)(0.05 + 0.95 * e0);
                _noteFill = e0;
            }
            else
            {
                // dt = 0 → tempo unchanged; just applies the arc's opening (establish) layers/density.
                _spec = Conductor.Step(spec, _userArousal, elapsedSeconds: 0, dtSeconds: 0, lo, hi, _sensitivity);
                _buildupGain = 1.0f;
                _noteFill = 1.0;
            }
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
    /// Compress the session arc for quick auditioning (1 = real time, e.g. 20 = 20× faster build-up).
    /// Only the arc's build-up clock is scaled — NOT the gentle arousal ramp — so you can hear the
    /// phases (establish → melody → marimba → full) in seconds while the calm/energize response
    /// still moves at its natural, unobtrusive rate.
    /// </summary>
    public double TimeScale
    {
        get { lock (_gate) return _timeScale; }
        set { lock (_gate) _timeScale = Math.Max(1.0, value); }
    }

    /// <summary>
    /// How fast the beat reacts to typing (1 = baseline). Higher = snappier / less gradual, lower =
    /// calmer. Exposed to the user as a slider; the default is 1.25 (+25%).
    /// </summary>
    public double Sensitivity
    {
        get { lock (_gate) return _sensitivity; }
        set { lock (_gate) _sensitivity = Math.Max(0.0, value); }
    }

    /// <summary>
    /// "Buildup" mode: bypass the adaptive thermostat and run a slow ~10-minute crescendo instead —
    /// near-silent and sparse at first, gradually assembling the full beat (volume, density, layers,
    /// and the melody all build). Toggling it restarts the build from the top.
    /// </summary>
    public bool Buildup
    {
        get { lock (_gate) return _buildup; }
        set { lock (_gate) { _buildup = value; _sessionSamples = 0; _loopCount = 0; } }
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
        foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Pad, scale.DegreeToMidi(root, deg));
        foreach (int deg in new[] { 0, 4 }) Put(BeatLayer.Bass, scale.DegreeToMidi(root, deg));     // warm low body
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
            BeatLayer.Pulse => PercussionFactory.CreateKick(f, _rate, bodyDecaySeconds: 0.22, clickAmount: 0.04),
            BeatLayer.Pad => SynthVoiceFactory.CreateTone(f, _rate, Waveform.WarmPad,
                                new Envelope { Attack = 0.06, Decay = 0.5, Sustain = 0.6, Release = 0.9 },
                                holdSeconds: 1.2, gain: 0.35f),
            BeatLayer.Marimba => InstrumentFactory.CreateMarimba(f, _rate),
            // Warm low bass — sustained pad-ish body, the heart of the "blanket". No high content.
            BeatLayer.Bass => SynthVoiceFactory.CreateTone(f, _rate, Waveform.WarmPad,
                                new Envelope { Attack = 0.02, Decay = 0.35, Sustain = 0.5, Release = 0.5 },
                                holdSeconds: 0.35, gain: 0.50f),
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
                buffer[offset + i] = sample * _buildupGain; // _buildupGain is 1.0 outside buildup mode

                _sessionSamples++;

                if (++_playhead >= _loopLen)
                {
                    _playhead = 0;
                    _nextIdx = 0;
                    // Let the conductor gently steer the next loop toward the flow band + along the arc.
                    // Session time persists across typing updates (it only resets on a mood change),
                    // so the ramp and the arc are never restarted mid-session.
                    double elapsed = _sessionSamples / (double)_rate * _timeScale; // arc clock (demo-scalable)
                    double dt = _loopLen / (double)_rate;                          // real time → ramp stays gentle
                    var (lo, hi) = SignalsToBeat.BpmRange(_spec.Preset);
                    if (_buildup)
                    {
                        // Time-driven crescendo: assemble the beat over ~10 min (volume + notes + layers).
                        _spec = Conductor.BuildupSpec(_spec, elapsed, lo, hi);
                        double e = Conductor.BuildupEnvelope(elapsed);
                        _buildupGain = (float)(0.05 + 0.95 * e);
                        _noteFill = e;
                    }
                    else
                    {
                        _spec = Conductor.Step(_spec, _userArousal, elapsed, dt, lo, hi, _sensitivity);
                        _buildupGain = 1.0f;
                        _noteFill = 1.0;
                    }
                    _loopCount++;
                    BuildSchedule(); // reuses the baked bank (scale/root unchanged)
                }
            }
            return count;
        }
    }
}
