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
