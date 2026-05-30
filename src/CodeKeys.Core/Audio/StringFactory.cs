namespace CodeKeys.Core.Audio;

/// <summary>
/// Karplus–Strong plucked-string synthesis: a short noise burst fed through a tuned
/// delay line with a damping filter in the feedback path. Cheap and convincingly
/// string-like — used for the electric-guitar preset and bright synth plucks.
/// </summary>
public static class StringFactory
{
    /// <summary>
    /// Pluck a string at <paramref name="freq"/>.
    /// </summary>
    /// <param name="decay">Feedback gain (0.90..0.999). Higher = longer sustain.</param>
    /// <param name="brightness">0..1 — how much high end survives each pass (1 = bright/twangy).</param>
    /// <param name="seed">Seed for the excitation noise (deterministic output).</param>
    public static SampleBuffer CreatePluckedString(
        double freq,
        int sampleRate,
        double durationSeconds = 0.45,
        double decay = 0.995,
        double brightness = 0.5,
        int seed = 1,
        float gain = 0.9f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        int n = Math.Max(2, (int)Math.Round(sampleRate / freq));
        var line = new double[n];

        // Excite with band-limited noise; brightness controls a one-pole low-pass on the burst.
        var rng = new Random(seed);
        double lp = 0;
        double a = Math.Clamp(brightness, 0.0, 1.0);
        for (int i = 0; i < n; i++)
        {
            double white = rng.NextDouble() * 2.0 - 1.0;
            lp = a * white + (1 - a) * lp;
            line[i] = lp;
        }

        int count = Math.Max(1, (int)Math.Ceiling(durationSeconds * sampleRate));
        var s = new float[count];

        int idx = 0;
        // Damping factor in the averaging filter: brighter => keep more of the raw sample.
        double damp = 0.5 + 0.49 * a; // 0.5 (dark) .. ~0.99 (bright)
        for (int i = 0; i < count; i++)
        {
            double cur = line[idx];
            s[i] = (float)(cur * gain);

            int next = (idx + 1) % n;
            // Lowpass-averaging feedback (the classic Karplus–Strong string-damping step).
            line[idx] = decay * (damp * cur + (1 - damp) * line[next]);
            idx = next;
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.9f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.006);
        return buf;
    }
}
