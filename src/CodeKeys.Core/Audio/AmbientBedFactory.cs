namespace CodeKeys.Core.Audio;

/// <summary>
/// Procedurally generates the continuous ambient bed loops (brown noise, evolving
/// pad, rain-like texture). Each returns a buffer that loops seamlessly — the join
/// is crossfaded so there's no audible click or pulse at the loop point. Generation
/// is seeded so output is deterministic and testable.
/// </summary>
public static class AmbientBedFactory
{
    /// <summary>Deep, smooth brown noise — the most-loved focus texture.</summary>
    public static SampleBuffer BrownNoise(int sampleRate, double seconds = 8.0, double crossfade = 0.75, int seed = 1)
    {
        var rng = new Random(seed);
        int raw = (int)((seconds + crossfade) * sampleRate);
        var s = new float[raw];

        double last = 0;
        for (int i = 0; i < raw; i++)
        {
            double white = rng.NextDouble() * 2.0 - 1.0;
            // Leaky integrator → brown (−6 dB/oct) spectrum, with leak to stop DC drift.
            last = (last + 0.02 * white) * 0.998;
            s[i] = (float)last;
        }

        var loop = MakeSeamless(s, sampleRate, crossfade);
        var buf = new SampleBuffer(loop, sampleRate);
        buf.NormalizeInPlace(0.6f);
        return buf;
    }

    /// <summary>A slow evolving pad: a few detuned warm partials under a gentle tremolo.</summary>
    public static SampleBuffer Pad(double rootFreq, int sampleRate, double seconds = 12.0, double crossfade = 1.5, int seed = 7)
    {
        int raw = (int)((seconds + crossfade) * sampleRate);
        var s = new float[raw];

        // A soft minor-ish chord stack: root, fifth, octave, with slight detune for movement.
        double[] partials = { 1.0, 1.5, 2.0, 2.997, 3.003 };
        double[] weights  = { 1.0, 0.6, 0.5, 0.35, 0.35 };

        for (int i = 0; i < raw; i++)
        {
            double t = i / (double)sampleRate;
            double trem = 0.85 + 0.15 * Math.Sin(2 * Math.PI * 0.07 * t); // ~14s breathing
            double v = 0;
            for (int k = 0; k < partials.Length; k++)
                v += weights[k] * Math.Sin(2 * Math.PI * rootFreq * partials[k] * t);
            s[i] = (float)(v * trem);
        }

        var loop = MakeSeamless(s, sampleRate, crossfade);
        var buf = new SampleBuffer(loop, sampleRate);
        buf.NormalizeInPlace(0.5f);
        return buf;
    }

    /// <summary>A gentle steady rain texture: high-passed white noise with soft droplet amplitude bursts.</summary>
    public static SampleBuffer Rain(int sampleRate, double seconds = 8.0, double crossfade = 0.75, int seed = 11)
    {
        var rng = new Random(seed);
        int raw = (int)((seconds + crossfade) * sampleRate);
        var s = new float[raw];

        double prevWhite = 0;
        double env = 0.3;
        for (int i = 0; i < raw; i++)
        {
            double white = rng.NextDouble() * 2.0 - 1.0;
            // Simple one-pole high-pass to thin the noise toward a hiss/patter.
            double hp = white - prevWhite;
            prevWhite = white;

            // Slowly wandering amplitude with occasional brighter "patter" swells.
            if (rng.NextDouble() < 0.0008) env = 0.6 + rng.NextDouble() * 0.4;
            env += (0.3 - env) * 0.0006; // drift back toward calm
            s[i] = (float)(hp * env * 0.5);
        }

        var loop = MakeSeamless(s, sampleRate, crossfade);
        var buf = new SampleBuffer(loop, sampleRate);
        buf.NormalizeInPlace(0.55f);
        return buf;
    }

    /// <summary>
    /// Fold the tail of <paramref name="src"/> back over its head via a crossfade so the
    /// resulting (shorter) buffer loops without a seam. Result length = src.Length − crossfadeSamples.
    /// </summary>
    public static float[] MakeSeamless(float[] src, int sampleRate, double crossfadeSeconds)
    {
        int x = (int)(crossfadeSeconds * sampleRate);
        if (x <= 0 || x >= src.Length) return (float[])src.Clone();

        int outLen = src.Length - x;
        var outBuf = new float[outLen];
        Array.Copy(src, outBuf, outLen);

        // Blend the trailing x samples (which would otherwise be dropped) into the head.
        for (int i = 0; i < x; i++)
        {
            float headW = i / (float)x;          // head rises 0→1
            float tailW = 1f - headW;             // tail falls 1→0
            outBuf[i] = src[i] * headW + src[outLen + i] * tailW;
        }
        return outBuf;
    }
}
