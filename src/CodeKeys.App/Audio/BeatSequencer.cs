using CodeKeys.Core.Audio;
using CodeKeys.Core.Beat;
using CodeKeys.Core.Music;
using NAudio.Wave;

namespace CodeKeys.App.Audio;

/// <summary>
/// The generative beat renderer (module 3): turns a <see cref="BeatSpec"/> into a looping,
/// evolving bed. It's a sample-clocked sequencer — on each step it spawns pre-baked voice
/// buffers per the <see cref="BeatPattern"/> timeline, mixes the ringing voices, and at the
/// end of each loop calls <c>evolve()</c> so the groove drifts without going stale.
///
/// All pitches come from the spec's scale/root, so the bed is always consonant with the keys.
/// Voices are pre-baked per spec (a full scale bank), so loop turnover never synthesizes on
/// the audio thread.
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
    private BeatSpec? _pending;     // live-update groove, applied at the next loop boundary
    private int _cycle;
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

    /// <summary>Swap to a new beat (e.g. a different mood) live. Re-bakes the voice bank and schedule.</summary>
    public void SetSpec(BeatSpec spec)
    {
        lock (_gate)
        {
            _spec = spec;
            _pending = null;
            _cycle = 0;
            BakeBank(spec);
            BuildSchedule();
            _playhead = 0;
            _nextIdx = 0;
            _active.Clear();
        }
    }

    /// <summary>
    /// Live typing update: refresh the groove from new signals (same mood). If the scale/root
    /// match the current beat, this applies smoothly at the next loop boundary with no rebake
    /// and no restart; if they differ (mood changed) it falls back to a full <see cref="SetSpec"/>.
    /// </summary>
    public void UpdateGroove(BeatSpec spec)
    {
        lock (_gate)
        {
            if (spec.Scale != _spec.Scale || spec.Root != _spec.Root)
            {
                SetSpec(spec);
                return;
            }
            _pending = spec;
        }
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

        Put(BeatLayer.Pulse, root);
        Put(BeatLayer.Ghost, root + 24);
        foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Pad, scale.DegreeToMidi(root, deg));
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
            BeatLayer.Pulse => PercussionFactory.CreateKick(f, _rate, bodyDecaySeconds: 0.22, clickAmount: 0.06),
            BeatLayer.Pad => SynthVoiceFactory.CreateTone(f, _rate, Waveform.WarmPad,
                                new Envelope { Attack = 0.06, Decay = 0.5, Sustain = 0.6, Release = 0.9 },
                                holdSeconds: 1.2, gain: 0.35f),
            BeatLayer.Marimba => InstrumentFactory.CreateMarimba(f, _rate),
            BeatLayer.Melody => SynthVoiceFactory.CreateTone(f, _rate, Waveform.WarmPad, Envelope.Pluck, 0.18, 0.45f),
            BeatLayer.Ghost => PercussionFactory.CreateTap(f, _rate, decaySeconds: 0.045, noiseAmount: 0.25),
            _ => SynthVoiceFactory.CreateTone(f, _rate, Waveform.Sine, Envelope.Pluck)
        };
        return buf.Samples;
    }

    private void BuildSchedule()
    {
        var hits = BeatPattern.Build(_spec);
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
                buffer[offset + i] = sample;

                if (++_playhead >= _loopLen)
                {
                    _playhead = 0;
                    _nextIdx = 0;
                    if (_pending is not null)
                    {
                        _spec = _pending;        // apply the live typing update
                        _pending = null;
                        _cycle = 0;
                    }
                    else
                    {
                        _cycle++;
                        _spec = SignalsToBeat.Evolve(_spec, _cycle); // drift density + accents
                    }
                    BuildSchedule();             // reuses the baked bank (scale/root unchanged)
                }
            }
            return count;
        }
    }
}
