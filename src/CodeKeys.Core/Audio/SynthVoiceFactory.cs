namespace CodeKeys.Core.Audio;

/// <summary>
/// Renders one-shot pitched note buffers in RAM. Packs call this at load time to
/// pre-bake every key's note, so playing a key is just streaming a buffer — no
/// per-keystroke synthesis or disk access.
/// </summary>
public static class SynthVoiceFactory
{
    /// <summary>
    /// Render a single note to a mono buffer.
    /// </summary>
    /// <param name="frequency">Fundamental in Hz.</param>
    /// <param name="sampleRate">Output sample rate.</param>
    /// <param name="wave">Oscillator shape.</param>
    /// <param name="env">Amplitude envelope (soft attack keeps it click-free).</param>
    /// <param name="holdSeconds">How long the note body sustains before release.</param>
    /// <param name="gain">Peak gain 0..1.</param>
    public static SampleBuffer CreateTone(
        double frequency,
        int sampleRate,
        Waveform wave,
        Envelope env,
        double holdSeconds = 0.18,
        float gain = 0.8f)
    {
        if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));

        double total = env.TotalSeconds(holdSeconds);
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var samples = new float[count];

        double phaseStep = 2.0 * Math.PI * frequency / sampleRate;
        double phase = 0;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double amp = env.AmplitudeAt(t, holdSeconds);
            double s = Oscillator.Sample(wave, phase, frequency) * amp * gain;
            samples[i] = (float)s;
            phase += phaseStep;
        }

        // Guarantee the very last sample lands at zero so back-to-back notes never click.
        FadeOutTail(samples, sampleRate, 0.004);
        return new SampleBuffer(samples, sampleRate);
    }

    /// <summary>
    /// Render a lush, evolving pad note — the late-90s new-age "flow" sound (Enigma / Delerium /
    /// Robert Miles): several slightly DETUNED warm-pad oscillators beating gently against each other
    /// for analog width, plus a quiet octave-up shimmer, under a long soft attack and a long release
    /// so chords bloom in and overlap bar-to-bar into a continuous wash rather than a struck note.
    /// </summary>
    public static SampleBuffer CreatePad(
        double frequency,
        int sampleRate,
        double holdSeconds = 3.0,
        float gain = 0.5f)
    {
        if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));

        var env = new Envelope { Attack = 0.7, Decay = 1.0, Sustain = 0.85, Release = 2.5 };
        double total = env.TotalSeconds(holdSeconds);
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var samples = new float[count];

        // Detuned voices (in cents-ish ratios) + an octave-up sine for air. Weights sum is used to
        // normalize so the stacked partials don't overflow before the gain/envelope are applied.
        double[] freqs   = { frequency, frequency * 1.004, frequency * 0.994, frequency * 2.0 };
        double[] weights = { 1.0,       0.8,               0.8,               0.18 };
        Waveform[] waves = { Waveform.WarmPad, Waveform.WarmPad, Waveform.WarmPad, Waveform.Sine };
        double norm = 0;
        foreach (var w in weights) norm += w;

        var phase = new double[freqs.Length];
        var step = new double[freqs.Length];
        for (int p = 0; p < freqs.Length; p++) step[p] = 2.0 * Math.PI * freqs[p] / sampleRate;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double amp = env.AmplitudeAt(t, holdSeconds);
            double s = 0;
            for (int p = 0; p < freqs.Length; p++)
            {
                s += Oscillator.Sample(waves[p], phase[p], freqs[p]) * weights[p];
                phase[p] += step[p];
            }
            samples[i] = (float)(s / norm * amp * gain);
        }

        FadeOutTail(samples, sampleRate, 0.01);
        return new SampleBuffer(samples, sampleRate);
    }

    /// <summary>Apply a short linear fade over the final <paramref name="seconds"/> to kill end-of-buffer clicks.</summary>
    public static void FadeOutTail(float[] samples, int sampleRate, double seconds)
    {
        int fade = Math.Min(samples.Length, (int)(seconds * sampleRate));
        if (fade <= 1) return;
        int start = samples.Length - fade;
        for (int i = 0; i < fade; i++)
        {
            float g = 1f - i / (float)(fade - 1);
            samples[start + i] *= g;
        }
    }
}
