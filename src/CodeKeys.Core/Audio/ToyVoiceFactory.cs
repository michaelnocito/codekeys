namespace CodeKeys.Core.Audio;

/// <summary>
/// One-shot "character" voices that don't fit the percussion/instrument factories: a calm liquid
/// water-drop (for the meditative "Water Drops" pack) and a few playful cartoon voices (boing / pop
/// / zap) for the deliberately-silly pack. All are pure, deterministic raw-sample synthesis with
/// soft attacks + faded tails so they never click.
/// </summary>
public static class ToyVoiceFactory
{
    /// <summary>
    /// A water droplet: a sine whose pitch sweeps quickly UP (the "ploop" resonance of a drop)
    /// under a fast exponential decay. Gentle and consonant — sits nicely over the bowl bed.
    /// </summary>
    public static SampleBuffer CreateDroplet(double freq, int sampleRate, double decaySeconds = 0.22, float gain = 0.7f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        double total = decaySeconds + 0.03;
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        double tau = Math.Max(1e-4, decaySeconds / 4.0);
        const double riseTau = 0.018;   // pitch climbs over ~18 ms
        const double attack = 0.003;
        double phase = 0;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            // f starts at `freq` and rises toward ~2.2× — the characteristic drop "ploop".
            double f = freq * (2.2 - 1.2 * Math.Exp(-t / riseTau));
            phase += 2.0 * Math.PI * f / sampleRate;

            double body = Math.Sin(phase) * Math.Exp(-t / tau);
            double att = t < attack ? t / attack : 1.0;
            s[i] = (float)(body * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.8f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.005);
        return buf;
    }

    /// <summary>
    /// A cartoon "boing": a triangle tone that slides DOWN in pitch with a decaying vibrato wobble —
    /// the springy comedy sound. Intentionally a little out of the calm guidelines (silly pack only).
    /// </summary>
    public static SampleBuffer CreateBoing(double freq, int sampleRate, double decaySeconds = 0.4,
        double wobbleHz = 13.0, double wobbleDepth = 0.18, float gain = 0.7f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        double total = decaySeconds + 0.05;
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        double tau = Math.Max(1e-4, decaySeconds / 4.0);
        const double attack = 0.004;
        double phase = 0;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double slide = 1.0 - 0.25 * (1.0 - Math.Exp(-t / 0.12));                  // drops ~25%
            double vib = 1.0 + wobbleDepth * Math.Exp(-t / (decaySeconds * 0.5)) * Math.Sin(2.0 * Math.PI * wobbleHz * t);
            double f = freq * slide * vib;
            phase += 2.0 * Math.PI * f / sampleRate;

            double body = Oscillator.Sample(Waveform.Triangle, phase, f) * Math.Exp(-t / tau);
            double att = t < attack ? t / attack : 1.0;
            s[i] = (float)(body * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.82f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.006);
        return buf;
    }

    /// <summary>A short bubble "pop": a quick upward pitch blip with a tiny click and fast decay.</summary>
    public static SampleBuffer CreatePop(double freq, int sampleRate, double decaySeconds = 0.07, float gain = 0.7f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        double total = decaySeconds + 0.02;
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        double tau = Math.Max(1e-4, decaySeconds / 4.0);
        const double attack = 0.0015;
        double phase = 0;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double f = freq * (0.7 + 1.6 * (1.0 - Math.Exp(-t / 0.008))); // fast upward blip
            phase += 2.0 * Math.PI * f / sampleRate;

            double body = Math.Sin(phase) * Math.Exp(-t / tau);
            double click = t < 0.003 ? 0.25 * Math.Exp(-t / 0.0008) * Math.Sin(2.0 * Math.PI * 1800.0 * t) : 0;
            double att = t < attack ? t / attack : 1.0;
            s[i] = (float)((body + click) * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.8f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.004);
        return buf;
    }

    /// <summary>A descending "zap"/blip (saw) — a quick comedic swoop, used for the silly backspace.</summary>
    public static SampleBuffer CreateZap(double freq, int sampleRate, double decaySeconds = 0.12, float gain = 0.65f)
    {
        if (freq <= 0) throw new ArgumentOutOfRangeException(nameof(freq));

        double total = decaySeconds + 0.02;
        int count = Math.Max(1, (int)Math.Ceiling(total * sampleRate));
        var s = new float[count];

        double tau = Math.Max(1e-4, decaySeconds / 4.0);
        const double attack = 0.002;
        double phase = 0;

        for (int i = 0; i < count; i++)
        {
            double t = i / (double)sampleRate;
            double f = freq * (0.6 + 2.5 * Math.Exp(-t / 0.04)); // falls from ~3.1× down to ~0.6×
            phase += 2.0 * Math.PI * f / sampleRate;

            double body = Oscillator.Sample(Waveform.Saw, phase, f) * Math.Exp(-t / tau);
            double att = t < attack ? t / attack : 1.0;
            s[i] = (float)(body * att * gain);
        }

        var buf = new SampleBuffer(s, sampleRate);
        buf.NormalizeInPlace(0.78f);
        SynthVoiceFactory.FadeOutTail(s, sampleRate, 0.005);
        return buf;
    }
}
