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
    private double _noteFill = 0.0;     // note-fill factor passed to BeatPattern — 0 (sparse) → 1 (full)
    private int _sweepBowlMidi = -1;    // Chakra Sweep: the current stage's bowl MIDI (-1 = not sweeping)

    // Living events (optional): a derivative-driven, self-calibrating accent channel. The detector
    // watches the arousal stream; when it fires we inject a soft one-shot chime (rising) or splash
    // (falling) directly into the mix at the moment of the event — an "auditory icon", separate from
    // the looping bed. Off by default; the bed is unchanged when disabled.
    private readonly LivingEventDetector _events = new();
    private bool _livingEvents;
    private int _eventChimeMidi = -1;   // baked Chime pitch used for a rising (flow-burst) accent
    private int _eventSplashMidi = -1;  // baked Splash pitch used for a falling (settling) accent
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
            double build0 = BuildAt(spec.Preset, 0);
            _spec = Conductor.Step(spec, _userArousal, elapsedSeconds: 0, dtSeconds: 0, lo, hi, _sensitivity, build0);
            _noteFill = build0;
            _sessionSamples = 0;
            _loopCount = 0;
            UpdateSweepBowl(0);
            BakeBank(_spec);
            BuildSchedule();
            _playhead = 0;
            _nextIdx = 0;
            _active.Clear();
            _events.Reset();
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
            double build0 = BuildAt(_spec.Preset, 0);
            _spec = Conductor.Step(_spec, _userArousal, elapsedSeconds: 0, dtSeconds: 0, lo, hi, _sensitivity, build0);
            _noteFill = build0;
            UpdateSweepBowl(0);
            BuildSchedule();
            _playhead = 0;
            _nextIdx = 0;
            _active.Clear();
            _events.Reset();
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
        lock (_gate)
        {
            // Smooth toward the new reading (on top of the 30s signals window) so the conductor follows
            // a settled trend, never a single snapshot — deliberately not hyper-responsive.
            _userArousal += (a - _userArousal) * 0.25;

            // Living events run off the RAW reading (its change/velocity), not the smoothed trend, so a
            // genuine burst or settle is detected before the smoothing irons it out. Fire a soft accent
            // the instant the detector trips — a one-shot, distinct from the looping bed.
            if (_livingEvents)
            {
                var kind = _events.Push(a);
                if (kind == LivingEventKind.Rising) Inject(BeatLayer.Chime, _eventChimeMidi, 0.16f);
                else if (kind == LivingEventKind.Falling) Inject(BeatLayer.Splash, _eventSplashMidi, 0.22f);
            }
        }
    }

    /// <summary>
    /// Toggle the living-events accent channel (off by default). Inspired by how PlantWave fires
    /// extra sounds from the rate-of-change of its signal. Resetting the detector on toggle so it
    /// re-warms cleanly. Does not touch the bed — when off, playback is byte-identical.
    /// </summary>
    public bool LivingEventsEnabled
    {
        get { lock (_gate) return _livingEvents; }
        set { lock (_gate) { _livingEvents = value; _events.Reset(); } }
    }

    /// <summary>Spawn a single pre-baked voice into the live mix immediately. Caller holds the gate.</summary>
    private void Inject(BeatLayer layer, int midi, float gain)
    {
        if (midi >= 0 && _bank.TryGetValue((layer, midi), out var data))
            _active.Add(new ActiveVoice { Data = data, Pos = 0, Gain = gain });
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

        void Put(BeatLayer layer, int midi, double? freqOverride = null, double? decayOverride = null)
        {
            var key = (layer, midi);
            if (!_bank.ContainsKey(key)) _bank[key] = Bake(layer, midi, freqOverride, decayOverride);
        }

        // Bake every voice the arc might enable (regardless of which layers are active right now),
        // so a layer turning on mid-session never synthesizes on the audio thread.
        Put(BeatLayer.Pulse, root);
        Put(BeatLayer.Ghost, root + 24);
        foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Pad, scale.DegreeToMidi(root, deg));      // chord (dormant moods)
        // Dreamflow: pre-bake every pad pitch its wandering progression needs (a low sustained root an
        // octave down + a 3-note stacked-scale chord, per progression degree), so a bar change just
        // reschedules baked buffers — never synthesizes the lush pad on the audio thread.
        if (SignalsToBeat.IsPadFlow(spec.Preset))
            foreach (int baseDeg in SignalsToBeat.DreamflowProgression)
            {
                Put(BeatLayer.Pad, scale.DegreeToMidi(root - 12, baseDeg));
                foreach (int d in new[] { 0, 2, 4 }) Put(BeatLayer.Pad, scale.DegreeToMidi(root, baseDeg + d));
            }
        // Code Groove: bake the fixed drum-kit timbres under their sentinel bank keys (the bassline
        // reuses the Bass pitches baked below).
        if (SignalsToBeat.IsGroove(spec.Preset))
        {
            Put(BeatLayer.Kick,  BeatPattern.GrooveKickMidi);
            Put(BeatLayer.Snare, BeatPattern.GrooveSnareMidi);
            // (No hi-hat — removed; it read as a metronome mallet.)
        }
        // Zion: the techno kit + tribal toms (the kick/snare/hat reuse the sentinel keys; bass + synth
        // reuse the Bass/Melody pitches baked above, voiced as saws by the Bake() Zion branch).
        if (SignalsToBeat.IsZion(spec.Preset))
        {
            Put(BeatLayer.Kick,  BeatPattern.GrooveKickMidi);
            Put(BeatLayer.Snare, BeatPattern.GrooveSnareMidi);
            Put(BeatLayer.Hat,   BeatPattern.GrooveHatMidi);
            foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Tom, scale.DegreeToMidi(root, deg));
        }
        // Bake TWO Bass variants per pitch — a mid-length default (~2s) and a long lingering one
        // (~3.6s). Pitches: scale degree 0 (root), the PERFECT FIFTH (by interval, root+7 — works
        // for any scale), and scale degree 4 (dormant-pattern fallback). All three so the pattern
        // can pick safely whether it asks via scale degree or interval.
        int rootBassMidi  = scale.DegreeToMidi(root - 12, 0);
        int fifthBassMidi = (root - 12) + 7;
        int deg4BassMidi  = scale.DegreeToMidi(root - 12, 4);
        foreach (int bMidi in new[] { rootBassMidi, fifthBassMidi, deg4BassMidi })
        {
            double bFreq = NoteUtil.MidiToFrequency(bMidi);
            Put(BeatLayer.Bass, bMidi);                                                        // default ~2.0s
            Put(BeatLayer.Bass, bMidi + BeatPattern.LongBassOffset, bFreq, decayOverride: 3.6); // long ~3.6s
        }
        foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Splash, scale.DegreeToMidi(root, deg));   // rare dark splash
        // Chakra presets tune the bowl to a specific Solfeggio Hz; others use scale degrees.
        var chakraFreq = SignalsToBeat.ChakraBowlFreq(spec.Preset);
        if (spec.Preset == BeatPreset.ChakraSweep || spec.Preset == BeatPreset.CelestialSweep)
            // Both walking-sweep presets climb all seven chakras — pre-bake every bowl so a stage
            // change just reschedules a cached buffer (no audio-thread synthesis).
            foreach (var c in SignalsToBeat.ChakraSweepStages)
                Put(BeatLayer.Bowl, SignalsToBeat.ChakraBowlMidi(c), SignalsToBeat.ChakraBowlFreq(c));
        else if (chakraFreq.HasValue)
            Put(BeatLayer.Bowl, SignalsToBeat.ChakraBowlMidi(spec.Preset), chakraFreq.Value);
        else
            foreach (int deg in new[] { 0, 4 }) Put(BeatLayer.Bowl, scale.DegreeToMidi(root, deg));    // Tibetan bowl strikes
        foreach (int deg in new[] { 0, 2, 4 }) Put(BeatLayer.Chime, scale.DegreeToMidi(root + 24, deg)); // high sparkle (unused / living events)
        // Living-events accent pitches (consonant chord tones): chime an octave up for a rising
        // flow-burst, a dark splash at the root for a settle. Both already baked above.
        _eventChimeMidi = scale.DegreeToMidi(root + 24, 0);
        _eventSplashMidi = scale.DegreeToMidi(root, 0);

        for (int d = 0; d < span; d++)
        {
            int midi = scale.DegreeToMidi(root + 12, d);
            Put(BeatLayer.Marimba, midi);
            Put(BeatLayer.Melody, midi);
        }
    }

    private float[] Bake(BeatLayer layer, int midi, double? freqOverride = null, double? decayOverride = null)
    {
        double f = freqOverride ?? NoteUtil.MidiToFrequency(midi);
        SampleBuffer buf = layer switch
        {
            // Atmospheric pulse — a pure deep sine with a soft attack and long sustain (no pitch
            // drop, no click). Reads as a warm "hummmm" rather than a driving kick. Quieter than the
            // Bass hum, so it sits as a gentle occasional accent over the continuous low foundation.
            BeatLayer.Pulse => PercussionFactory.CreateSub(f, _rate, decaySeconds: 0.55, gain: 0.40f),
            // Lush, detuned, long-blooming pad — overlaps bar-to-bar into a continuous wash (Dreamflow's
            // flowing new-age bed). Per-note hit gains in BeatPattern keep the stacked chord in check.
            BeatLayer.Pad => SynthVoiceFactory.CreatePad(f, _rate, holdSeconds: 3.0, gain: 0.5f),
            BeatLayer.Marimba => InstrumentFactory.CreateMarimba(f, _rate),
            // Zion: a driving techno saw bass — aggressive, punchy, short — the "thudding bass".
            BeatLayer.Bass when SignalsToBeat.IsZion(_spec.Preset) =>
                                SynthVoiceFactory.CreateTone(f, _rate, Waveform.Saw,
                                new Envelope { Attack = 0.004, Decay = 0.16, Sustain = 0.25, Release = 0.08 },
                                holdSeconds: 0.04, gain: 0.45f),
            // Deep low boom — pure sine, long resonant decay. Decay is overridable so we can bake a
            // short / mid / long variant per pitch for the playful "who leads how long" exchange.
            BeatLayer.Bass => SynthVoiceFactory.CreateTone(f, _rate, Waveform.Sine,
                                new Envelope { Attack = 0.005, Decay = decayOverride ?? 2.0, Sustain = 0.0, Release = 0.4 },
                                holdSeconds: 0.0, gain: 0.60f),
            // Splash — a rare, dark, soft mid-low appearance. Slow attack (no sharp transient) and
            // no high content, so it adds variety without capturing attention (per the research).
            BeatLayer.Splash => SynthVoiceFactory.CreateTone(f, _rate, Waveform.WarmPad,
                                new Envelope { Attack = 0.05, Decay = 0.5, Sustain = 0.3, Release = 0.6 },
                                holdSeconds: 0.25, gain: 0.32f),
            // Zion: the propelling synth — a bright, short saw stab driving the repetitive ostinato.
            BeatLayer.Melody when SignalsToBeat.IsZion(_spec.Preset) =>
                                SynthVoiceFactory.CreateTone(f, _rate, Waveform.Saw,
                                new Envelope { Attack = 0.004, Decay = 0.10, Sustain = 0.45, Release = 0.12 },
                                holdSeconds: 0.06, gain: 0.30f),
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
            // Code Groove drum kit (fixed timbres — pitch is ignored). The kick is a DEEP BASS THUMP:
            // low ~42 Hz body, long round decay, gentle pitch drop and almost no click — boom, not a
            // wooden knock. Snare = a soft clap. Hat = an airy, high, mostly-noise tick (not pitched
            // wood) so it whispers the eighths rather than sounding like a mallet.
            // Zion: a punchy, tight techno kick (more click/attack, shorter body) for the four-on-the-floor.
            BeatLayer.Kick when SignalsToBeat.IsZion(_spec.Preset) =>
                                PercussionFactory.CreateKick(50.0, _rate,
                                pitchStartMultiple: 2.5, pitchDropSeconds: 0.025,
                                bodyDecaySeconds: 0.16, clickAmount: 0.28, gain: 1.0f),
            BeatLayer.Kick  => PercussionFactory.CreateKick(42.0, _rate,
                                pitchStartMultiple: 2.0, pitchDropSeconds: 0.05,
                                bodyDecaySeconds: 0.40, clickAmount: 0.03, gain: 1.0f),
            BeatLayer.Snare => PercussionFactory.CreateSnare(_rate, decaySeconds: 0.14, gain: 0.70f),
            // Zion: an airy noise hi-hat (filtered noise, no pitched tone) — the techno "tss", NOT a
            // wooden tick. Used only on the off-beats.
            BeatLayer.Hat when SignalsToBeat.IsZion(_spec.Preset) =>
                                PercussionFactory.CreateSnare(_rate, decaySeconds: 0.05, toneAmount: 0.0, gain: 0.45f),
            BeatLayer.Hat   => PercussionFactory.CreateTap(2600.0, _rate, decaySeconds: 0.022, noiseAmount: 0.9, gain: 0.40f),
            // Tribal tom — a pitched drum (a kick with a higher body + slower pitch drop), for the
            // Zion-cave tribal pattern.
            BeatLayer.Tom => PercussionFactory.CreateKick(f, _rate,
                                pitchStartMultiple: 1.8, pitchDropSeconds: 0.05,
                                bodyDecaySeconds: 0.14, clickAmount: 0.06, gain: 0.85f),
            // Tibetan singing bowl: shaped as an APPEARANCE — ascends in, holds briefly, then has
            // a long noticeable fade-out trail. ~10 s total so each appearance is roughly 2 measures
            // (at 66 BPM, 2 bars ≈ 7 s) plus a graceful trailing tail. The bass is the focus; the
            // bowl makes appearances over it.
            BeatLayer.Bowl => InstrumentFactory.CreateSingingBowl(f, _rate,
                                durationSeconds: 10.0, gain: 0.60f, attack: 1.5, sustain: 3.5),
            _ => SynthVoiceFactory.CreateTone(f, _rate, Waveform.Sine, Envelope.Pluck)
        };
        return buf.Samples;
    }

    /// <summary>
    /// The build/texture fraction for a preset at a given elapsed time. Chakra Sweep rides a steady
    /// plateau (<see cref="Conductor.SweepEnvelope"/>) so every chakra is clearly present; every other
    /// template breathes via the rise/fall <see cref="Conductor.CycleEnvelope"/>.
    /// </summary>
    private static double BuildAt(BeatPreset preset, double elapsed) =>
        SignalsToBeat.IsZion(preset)
            ? Conductor.ZionEnvelope(elapsed)    // fast build from the opening hit, then hold driving
        : preset == BeatPreset.ChakraSweep || preset == BeatPreset.CelestialSweep || SignalsToBeat.IsGroove(preset)
            ? Conductor.SweepEnvelope(elapsed)   // hold the groove steadily present (no breathing fade)
            : Conductor.CycleEnvelope(elapsed);

    /// <summary>
    /// Chakra Sweep: pick the bowl MIDI for the chakra the journey is currently on (Root→Crown,
    /// 3 min each). Leaves <see cref="_sweepBowlMidi"/> at -1 for every other template so the bowl
    /// pitch comes from the spec's own preset as before.
    /// </summary>
    private void UpdateSweepBowl(double elapsedSeconds)
    {
        _sweepBowlMidi = (_spec.Preset == BeatPreset.ChakraSweep || _spec.Preset == BeatPreset.CelestialSweep)
            ? SignalsToBeat.ChakraBowlMidi(SignalsToBeat.ChakraSweepStageAt(elapsedSeconds))
            : -1;
    }

    private void BuildSchedule()
    {
        var hits = BeatPattern.Build(_spec, _loopCount, _noteFill, _sweepBowlMidi);
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
                // No per-cycle output ducking: the breathing is expressed via WHICH voices play and
                // how dense the pattern is (the conductor's job), not by ducking the bed's volume.
                // Stacking output-gain envelopes on top of bedLevel × master made the start
                // inaudible relative to keystrokes (~21 dB down). Volume stays at bed level the
                // whole time so the bed is always perceptible; the cycle is felt via voicing.
                buffer[offset + i] = sample;

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
                    double build = BuildAt(_spec.Preset, elapsed);
                    _spec = Conductor.Step(_spec, _userArousal, elapsed, dt, lo, hi, _sensitivity, build);
                    _noteFill = build;
                    UpdateSweepBowl(elapsed); // Chakra Sweep: walk the bowl up the chakras over time
                    _loopCount++;
                    BuildSchedule(); // reuses the baked bank (scale/root unchanged)
                }
            }
            return count;
        }
    }
}
