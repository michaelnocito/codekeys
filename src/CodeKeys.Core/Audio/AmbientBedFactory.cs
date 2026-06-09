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

    /// <summary>
    /// Pink noise (1/f spectrum, −3 dB/oct) — the perceptual sweet spot between white (too sharp)
    /// and brown (too rumbly). Standard in focus/ADHD research and most focus apps; sounds like
    /// a gentle, even hiss rather than a deep roar. Generated via Paul Kellet's IIR filter.
    /// </summary>
    public static SampleBuffer PinkNoise(int sampleRate, double seconds = 8.0, double crossfade = 0.75, int seed = 3)
    {
        var rng = new Random(seed);
        int raw = (int)((seconds + crossfade) * sampleRate);
        var s = new float[raw];

        // Paul Kellet's pink-noise IIR approximation (7 poles).
        double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
        for (int i = 0; i < raw; i++)
        {
            double w = rng.NextDouble() * 2.0 - 1.0;
            b0 = 0.99886 * b0 + w * 0.0555179;
            b1 = 0.99332 * b1 + w * 0.0750759;
            b2 = 0.96900 * b2 + w * 0.1538520;
            b3 = 0.86650 * b3 + w * 0.3104856;
            b4 = 0.55000 * b4 + w * 0.5329522;
            b5 = -0.7616 * b5 - w * 0.0168980;
            double pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362;
            b6 = w * 0.115926;
            s[i] = (float)(pink * 0.11); // scale before normalize
        }

        var loop = MakeSeamless(s, sampleRate, crossfade);
        var buf = new SampleBuffer(loop, sampleRate);
        buf.NormalizeInPlace(0.55f);
        return buf;
    }

    /// <summary>
    /// Flat-spectrum white noise — the research-backed focus bed texture (paired with isochronic tone).
    /// Gain intentionally kept low (0.28) so it sits under the beat without masking keystrokes.
    /// </summary>
    public static SampleBuffer WhiteNoise(int sampleRate, double seconds = 8.0, double crossfade = 0.75, int seed = 17)
    {
        var rng = new Random(seed);
        int raw = (int)((seconds + crossfade) * sampleRate);
        var s = new float[raw];
        for (int i = 0; i < raw; i++)
            s[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        var loop = MakeSeamless(s, sampleRate, crossfade);
        var buf = new SampleBuffer(loop, sampleRate);
        buf.NormalizeInPlace(0.28f);
        return buf;
    }

    /// <summary>
    /// 40 Hz isochronic tone: a 340 Hz carrier amplitude-modulated at 40 Hz using a raised-cosine
    /// pulse (50% duty, smooth on/off to avoid clicks at the switching boundary).
    ///
    /// Research basis: 40 Hz gamma binaural beats with a 340 Hz carrier + white noise background
    /// improved sustained attention performance vs. pure-tone control (p = 0.002, n = 64,
    /// within-subjects crossover, Scientific Reports 2025, PMC11799511). This engine is mono,
    /// so we use isochronic modulation (monaural — works on speakers and headphones alike) rather
    /// than the stereo-only binaural variant studied. The carrier and modulation frequency are the
    /// same; only the delivery mechanism differs.
    ///
    /// Keep the output gain very low (~0.10) — this is a subliminal neurological background layer,
    /// not a foreground musical element.
    /// </summary>
    public static SampleBuffer IsochronicTone(int sampleRate, double seconds = 8.0, double crossfade = 0.75)
    {
        // 40 Hz period = 25 ms. Each pulse: 50% duty = 12.5 ms on, 12.5 ms off.
        // Raised-cosine taper on the leading and trailing 20% of the ON window.
        const double carrierHz = 340.0;
        const double isoHz = 40.0;

        int raw = (int)((seconds + crossfade) * sampleRate);
        var s = new float[raw];

        for (int i = 0; i < raw; i++)
        {
            double t = i / (double)sampleRate;
            double carrier = Math.Sin(2.0 * Math.PI * carrierHz * t);

            // Phase within one 40 Hz cycle, 0..1
            double isoPhase = (t * isoHz) % 1.0;

            double env;
            if (isoPhase < 0.5)
            {
                // ON half: cosine taper in (0..0.2) and out (0.8..1.0) of the ON window.
                double half = isoPhase / 0.5; // 0..1 within the ON half
                if (half < 0.2)
                    env = 0.5 * (1.0 - Math.Cos(Math.PI * half / 0.2));
                else if (half > 0.8)
                    env = 0.5 * (1.0 - Math.Cos(Math.PI * (1.0 - half) / 0.2));
                else
                    env = 1.0;
            }
            else
            {
                env = 0.0; // OFF half — silence between pulses
            }

            s[i] = (float)(carrier * env);
        }

        var loop = MakeSeamless(s, sampleRate, crossfade);
        var buf = new SampleBuffer(loop, sampleRate);
        buf.NormalizeInPlace(0.90f); // normalized cleanly; gain applied at the mix site in BeatSequencer
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
