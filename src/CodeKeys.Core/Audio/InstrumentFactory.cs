namespace CodeKeys.Core.Audio;

/// <summary>
/// Richer pitched instruments built by additive / FM synthesis: acoustic piano,
/// Rhodes electric piano, a detuned "supersaw" for synthwave leads, and a marimba.
/// All render one-shot, click-free, normalized buffers.
/// </summary>
public static class InstrumentFactory
{
    /// <summary>
    /// Acoustic-ish piano: a stack of harmonics with slight inharmonic stretch, where
    /// higher partials decay faster, under a hammer attack and a long body decay.
    /// </summary>
    public static SampleBuffer CreatePiano(double freq, int sampleRate, double durationSeconds = 0.6, float gain = 0.85f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        int count = Math.Max(1, (int)Math.Ceiling(durationSeconds * sampleRate));
        var s = new float[count];

        const int partials = 7;
        const double inharmonic = 0.0004; // gentle string stretch
        const double attack = 0.004;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double bodyAtt = t < attack ? t / attack : 1.0;
            double v = 0;
            for (int k = 1; k <= partials; k++)
            {
                double fk = freq * k * Math.Sqrt(1.0 + inharmonic * k * k);
                if (fk > sampleRate / 2.0) break; // skip aliasing partials
                double ampK = (1.0 / k) * Math.Exp(-t * (1.5 + 0.9 * k)); // highs die first
                v += ampK * Math.Sin(2.0 * Math.PI * fk * t);
            }
            s[i] = (float)(v * bodyAtt * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.85f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.006);
        return buf;
    }

    /// <summary>
    /// Rhodes-style electric piano: two-operator FM (sine carrier + sine modulator) with a
    /// falling modulation index — bell-like attack settling into a warm tone.
    /// </summary>
    public static SampleBuffer CreateRhodes(double freq, int sampleRate, double durationSeconds = 0.5, float gain = 0.85f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        int count = Math.Max(1, (int)Math.Ceiling(durationSeconds * sampleRate));
        var s = new float[count];
        const double attack = 0.003;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double att = t < attack ? t / attack : 1.0;
            double bodyDecay = Math.Exp(-t * 3.2);
            double index = 2.2 * Math.Exp(-t * 14.0); // bright tine attack -> mellow
            double mod = index * Math.Sin(2.0 * Math.PI * freq * 1.0 * t);
            double car = Math.Sin(2.0 * Math.PI * freq * t + mod);
            s[i] = (float)(car * bodyDecay * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.85f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.006);
        return buf;
    }

    /// <summary>
    /// "Supersaw": several detuned saw oscillators summed for a wide, bright analog lead —
    /// the synthwave staple.
    /// </summary>
    public static SampleBuffer CreateSuperSaw(
        double freq, int sampleRate, Envelope env,
        double holdSeconds = 0.18, int voices = 5, double detuneCents = 14, float gain = 0.7f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        double total = env.TotalSeconds(holdSeconds);
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        // Detune spread symmetric around the centre frequency.
        var freqs = new double[voices];
        for (int v = 0; v < voices; v++)
        {
            double offset = (voices == 1) ? 0 : (v - (voices - 1) / 2.0) / Math.Max(1, voices - 1);
            double cents = offset * detuneCents;
            freqs[v] = freq * Math.Pow(2.0, cents / 1200.0);
        }
        var phases = new double[voices];

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double amp = env.AmplitudeAt(t, holdSeconds);
            double v = 0;
            for (int k = 0; k < voices; k++)
            {
                v += Oscillator.Sample(Waveform.Saw, phases[k], freqs[k]);
                phases[k] += 2.0 * Math.PI * freqs[k] / sampleRate;
            }
            s[i] = (float)(v / voices * amp * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.85f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.004);
        return buf;
    }

    /// <summary>
    /// Tibetan singing bowl: a hammered metal bowl. Built from inharmonic partials with the
    /// classic ratios (1, 2.76, 5.4, 8.93) — bell-like, not a clean harmonic series — under a
    /// soft attack and a long resonant decay. Each partial is paired with a slightly detuned
    /// copy, producing the characteristic shimmering "warble" you hear from a real bowl.
    /// <para><paramref name="attack"/> is the fade-in time (s). A longer attack reads as "bowed"
    /// rather than "struck" — the bowl swells in instead of crashing in.</para>
    /// </summary>
    public static SampleBuffer CreateSingingBowl(double freq, int sampleRate,
        double durationSeconds = 1.4, float gain = 0.85f, double attack = 0.06)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        int count = Math.Max(1, (int)Math.Ceiling(durationSeconds * sampleRate));
        var s = new float[count];

        // Inharmonic ratios + per-partial amplitudes + decay rates (higher partials fade faster).
        // Decay rates are deliberately gentle — real Tibetan bowls sustain for 8-30 seconds, not the
        // 2-3 seconds a struck piano-like envelope gives. With the 0.20 outer multiplier (vs 0.6
        // before) the fundamental is still at ~5% at 15 s, so the buffer carries a long, slowly
        // dissolving ring instead of a quick puff.
        double[] ratios = { 1.00, 2.76, 5.40, 8.93 };
        double[] amps   = { 1.00, 0.55, 0.28, 0.14 };
        double[] decays = { 1.0, 1.5, 2.3, 3.2 };
        const double decayMultiplier = 0.20;
        const double detuneHz = 0.6;      // small detune between paired partials -> shimmer

        // Smoothstep fade-in (3p²-2p³) over the attack window — softer than a linear ramp, so
        // the bowl swells in with no perceptible attack onset.
        double SmoothAttack(double t)
        {
            if (t >= attack) return 1.0;
            double p = t / attack;
            return p * p * (3 - 2 * p);
        }

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double att = SmoothAttack(t);
            double v = 0;
            for (int k = 0; k < ratios.Length; k++)
            {
                double f = freq * ratios[k];
                if (f > sampleRate / 2.0) break;
                double envK = Math.Exp(-t * decays[k] * decayMultiplier);
                double primary = Math.Sin(2.0 * Math.PI * f * t);
                double shimmer = Math.Sin(2.0 * Math.PI * (f + detuneHz) * t);
                v += amps[k] * envK * 0.5 * (primary + shimmer);
            }
            s[i] = (float)(v * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.85f);
        // Longer fade-out tail (50ms vs 12ms) so the bowl decays into silence without a final clip.
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.050);
        return buf;
    }

    /// <summary>Soft wooden mallet: a fundamental plus a quiet 4th-harmonic "bar" partial, fast decay.</summary>
    public static SampleBuffer CreateMarimba(double freq, int sampleRate, double durationSeconds = 0.35, float gain = 0.85f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        int count = Math.Max(1, (int)Math.Ceiling(durationSeconds * sampleRate));
        var s = new float[count];
        const double attack = 0.003;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double att = t < attack ? t / attack : 1.0;
            double fund = Math.Sin(2.0 * Math.PI * freq * t) * Math.Exp(-t * 7.0);
            double bar = 0.25 * Math.Sin(2.0 * Math.PI * freq * 4.0 * t) * Math.Exp(-t * 18.0);
            s[i] = (float)((fund + bar) * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.82f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.005);
        return buf;
    }
}
