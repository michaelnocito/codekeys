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
