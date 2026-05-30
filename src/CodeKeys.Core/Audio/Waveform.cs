namespace CodeKeys.Core.Audio;

/// <summary>Oscillator shapes available to synth packs.</summary>
public enum Waveform
{
    Sine,
    Triangle,
    Square,
    Saw,
    /// <summary>Sine plus a softened set of odd/even partials — a warm "electric piano" tone.</summary>
    WarmPad,
    /// <summary>Two-operator FM, bright metallic attack decaying to a pure tone — for bells.</summary>
    FmBell
}

public static class Oscillator
{
    private const double TwoPi = Math.PI * 2.0;

    public static Waveform FromName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "sine" => Waveform.Sine,
        "triangle" => Waveform.Triangle,
        "square" => Waveform.Square,
        "saw" or "sawtooth" => Waveform.Saw,
        "warmpad" or "warm-pad" or "warm" => Waveform.WarmPad,
        "fmbell" or "fm-bell" or "bell" => Waveform.FmBell,
        _ => Waveform.Sine
    };

    /// <summary>Sample a waveform at phase <paramref name="phase"/> (radians) for fundamental <paramref name="freq"/>.</summary>
    public static double Sample(Waveform wave, double phase, double freq)
    {
        double p = phase % TwoPi;
        if (p < 0) p += TwoPi;

        return wave switch
        {
            Waveform.Sine => Math.Sin(p),
            Waveform.Triangle => 2.0 / Math.PI * Math.Asin(Math.Sin(p)),
            Waveform.Square => Math.Sin(p) >= 0 ? 1.0 : -1.0,
            Waveform.Saw => 2.0 * (p / TwoPi) - 1.0,
            Waveform.WarmPad =>
                0.70 * Math.Sin(p)
                + 0.18 * Math.Sin(2 * p)
                + 0.08 * Math.Sin(3 * p)
                + 0.04 * Math.Sin(4 * p),
            // FM: carrier modulated by a partial above it; index falls off over the note for a bell-like strike.
            Waveform.FmBell => Math.Sin(p + 2.0 * Math.Sin(p * 1.41)),
            _ => Math.Sin(p)
        };
    }
}
