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
    /// Tibetan singing bowl: a hammered metal bowl. Inharmonic partials with the classic ratios
    /// (1, 2.76, 5.4, 8.93) shaped by an ADSR-style envelope: smoothstep attack (ascend), an
    /// optional sustain plateau (held tone in the middle), and a long slow release (descend).
    /// <para><paramref name="attack"/> = ascend time, <paramref name="sustain"/> = plateau hold,
    /// <paramref name="durationSeconds"/> sets the buffer size and the release fills the remainder
    /// (so attack + sustain + release == duration).</para>
    /// </summary>
    public static SampleBuffer CreateSingingBowl(double freq, int sampleRate,
        double durationSeconds = 1.4, float gain = 0.85f, double attack = 0.06,
        double sustain = 0.0)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        int count = Math.Max(1, (int)Math.Ceiling(durationSeconds * sampleRate));
        var s = new float[count];

        // Inharmonic ratios + per-partial amplitudes + decay rates (higher partials fade faster).
        // Very gentle per-partial decay so the upper-partial colour gradually fades during the
        // long sustain — bowl character without metallic wash. The detuned-partial shimmer that
        // produces the "warble" is now nearly off (0.08 Hz beat, 10 % weight) so the bowl reads
        // as a clean held tone rather than a wavering one.
        double[] ratios = { 1.00, 2.76, 5.40, 8.93 };
        double[] amps   = { 1.00, 0.27, 0.14, 0.07 };
        double[] decays = { 1.0, 1.5, 2.3, 3.2 };
        const double decayMultiplier = 0.04;   // very slow per-partial decay (fundamental ~30 % at 30 s)
        const double detuneHz = 0.08;          // near-zero beat → almost no warble
        const double primaryWeight = 0.90;     // primary partial overwhelmingly dominates
        const double shimmerWeight = 0.10;     // tiny detuned colouring, no perceptible wash

        // Release fills whatever time the buffer has after attack + sustain. Rate is chosen so the
        // envelope ends at ~5 % (then FadeOutTail finishes the job).
        double release = Math.Max(0.01, durationSeconds - attack - sustain);
        double releaseRate = 3.0 / release;

        double Envelope(double t)
        {
            if (t < attack)
            {
                double p = t / attack;
                return p * p * (3 - 2 * p); // ascend (smoothstep)
            }
            double afterAttack = t - attack;
            if (afterAttack < sustain) return 1.0; // held tone plateau
            double r = afterAttack - sustain;
            return Math.Exp(-r * releaseRate);     // descend (exponential)
        }

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double env = Envelope(t);
            double v = 0;
            for (int k = 0; k < ratios.Length; k++)
            {
                double f = freq * ratios[k];
                if (f > sampleRate / 2.0) break;
                double envK = Math.Exp(-t * decays[k] * decayMultiplier);
                double primary = Math.Sin(2.0 * Math.PI * f * t);
                double shimmer = Math.Sin(2.0 * Math.PI * (f + detuneHz) * t);
                v += amps[k] * envK * (primaryWeight * primary + shimmerWeight * shimmer);
            }
            s[i] = (float)(env * v * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.85f);
        // Long fade-out tail (300 ms) so the dying ring dissolves softly into silence.
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.300);
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
