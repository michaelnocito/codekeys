namespace CodeKeys.Core.Audio;

/// <summary>
/// Renders low, percussive one-shot hits — the low-cognitive-interference voices.
/// These are deliberately NOT melodic: a tight transient over a low-frequency body,
/// short decay, consistent timbre. Research on the "changing-state" effect says steady,
/// low-variation sound habituates and stops competing for attention, while the punch +
/// low end is what makes a hit feel satisfying (the Beat Saber "juice").
/// </summary>
public static class PercussionFactory
{
    /// <summary>
    /// A kick/thump: a sine whose pitch drops quickly from a higher start down to
    /// <paramref name="freq"/>, under an exponential amplitude decay, with a soft transient.
    /// </summary>
    public static SampleBuffer CreateKick(
        double freq,
        int sampleRate,
        double pitchStartMultiple = 3.0,   // start this many × above the body pitch
        double pitchDropSeconds = 0.035,   // how fast the pitch settles
        double bodyDecaySeconds = 0.20,    // how long the low body rings
        double clickAmount = 0.12,         // soft attack tick (0 = none)
        float gain = 0.9f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        double total = bodyDecaySeconds + 0.04;
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        double pitchTau = Math.Max(1e-4, pitchDropSeconds / 3.0);
        double bodyTau = Math.Max(1e-4, bodyDecaySeconds / 4.0);
        const double attack = 0.002; // 2ms ramp so the very start isn't a click

        double phase = 0;
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;

            // Downward pitch envelope -> the characteristic "thump".
            double f = freq * (1.0 + (pitchStartMultiple - 1.0) * Math.Exp(-t / pitchTau));
            phase += 2.0 * Math.PI * f / sampleRate;

            double body = Math.Sin(phase) * Math.Exp(-t / bodyTau);

            // Soft high tick for snap (a quick, gentle sine blip — not a harsh click).
            double click = 0;
            if (clickAmount > 0 && t < 0.006)
                click = clickAmount * Math.Exp(-t / 0.0015) * Math.Sin(2.0 * Math.PI * 2200.0 * t);

            double att = t < attack ? t / attack : 1.0;
            s[i] = (float)((body + click) * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.85f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.005);
        return buf;
    }

    /// <summary>
    /// A tap / "thock": a fast-damped low tone with a short seeded noise transient —
    /// a soft wooden knock. The lowest-interference voice (very short, near-steady timbre).
    /// </summary>
    public static SampleBuffer CreateTap(
        double freq,
        int sampleRate,
        double decaySeconds = 0.09,
        double noiseAmount = 0.25,
        int seed = 5,
        float gain = 0.85f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        double total = decaySeconds + 0.03;
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        double toneTau = Math.Max(1e-4, decaySeconds / 4.0);
        var rng = new Random(seed);
        const double attack = 0.0015;

        double phase = 0;
        double phaseStep = 2.0 * Math.PI * freq / sampleRate;
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;

            double tone = Math.Sin(phase) * Math.Exp(-t / toneTau);
            phase += phaseStep;

            // Short broadband knock at the very start.
            double noise = 0;
            if (noiseAmount > 0 && t < 0.004)
                noise = noiseAmount * (rng.NextDouble() * 2.0 - 1.0) * Math.Exp(-t / 0.0012);

            double att = t < attack ? t / attack : 1.0;
            s[i] = (float)((tone + noise) * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.8f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.004);
        return buf;
    }

    /// <summary>
    /// A snare/clap: filtered noise under a sharp decay, with a couple of fast pre-echo
    /// taps for the "clap" smear. Seeded for deterministic output.
    /// </summary>
    public static SampleBuffer CreateSnare(
        int sampleRate,
        double decaySeconds = 0.16,
        double toneFreq = 180.0,
        double toneAmount = 0.25,
        int seed = 3,
        float gain = 0.85f)
    {
        double total = decaySeconds + 0.03;
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        var rng = new Random(seed);
        double noiseTau = Math.Max(1e-4, decaySeconds / 4.0);
        double tonePhase = 0;
        double toneStep = 2.0 * Math.PI * toneFreq / sampleRate;
        const double attack = 0.0015;

        // Clap "smear": a few quick amplitude bursts at the very start.
        double[] taps = { 0.0, 0.008, 0.016 };
        double tapWidth = 0.006;

        double prev = 0;
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;

            double white = rng.NextDouble() * 2.0 - 1.0;
            double hp = white - prev; // thin the noise (high-pass) for a snappier snare
            prev = white;

            double burst = 0;
            foreach (var tap in taps)
                if (t >= tap && t < tap + tapWidth)
                    burst = Math.Max(burst, Math.Exp(-(t - tap) / 0.0025));
            double bed = Math.Exp(-t / noiseTau);
            double noiseEnv = Math.Max(bed, burst);

            double tone = toneAmount * Math.Sin(tonePhase) * Math.Exp(-t / (noiseTau * 0.7));
            tonePhase += toneStep;

            double att = t < attack ? t / attack : 1.0;
            s[i] = (float)((hp * noiseEnv + tone) * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.85f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.004);
        return buf;
    }

    /// <summary>A pure deep sub: a low sine with a soft attack and medium decay (no pitch drop).</summary>
    public static SampleBuffer CreateSub(double freq, int sampleRate, double decaySeconds = 0.28, float gain = 0.9f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        double total = decaySeconds + 0.03;
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        double tau = Math.Max(1e-4, decaySeconds / 4.0);
        double phase = 0;
        double step = 2.0 * Math.PI * freq / sampleRate;
        const double attack = 0.004;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double att = t < attack ? t / attack : 1.0;
            s[i] = (float)(Math.Sin(phase) * Math.Exp(-t / tau) * att * gain);
            phase += step;
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.88f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.005);
        return buf;
    }
}
