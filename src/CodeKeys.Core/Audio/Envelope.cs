namespace CodeKeys.Core.Audio;

/// <summary>
/// A linear-ish ADSR amplitude envelope, in seconds. CodeKeys uses soft attacks
/// (never zero) so notes fade in instead of clicking, and gentle releases so they
/// ring out and mix instead of cutting off abruptly.
/// </summary>
public sealed class Envelope
{
    public double Attack { get; init; } = 0.006;   // seconds — soft, click-free
    public double Decay { get; init; } = 0.08;
    public double Sustain { get; init; } = 0.5;     // level 0..1
    public double Release { get; init; } = 0.25;

    /// <summary>A warm plucked-note default (short body, gentle tail).</summary>
    public static Envelope Pluck => new() { Attack = 0.006, Decay = 0.12, Sustain = 0.35, Release = 0.35 };

    /// <summary>A soft felt-tap: very quiet, almost no tail.</summary>
    public static Envelope FeltTap => new() { Attack = 0.004, Decay = 0.05, Sustain = 0.0, Release = 0.06 };

    /// <summary>A bell: instant-ish attack, long ringing tail.</summary>
    public static Envelope Bell => new() { Attack = 0.003, Decay = 0.9, Sustain = 0.0, Release = 1.2 };

    /// <summary>
    /// Total time the envelope is non-silent for a note held <paramref name="holdSeconds"/>,
    /// i.e. attack+decay (sustained for the hold) then release. CodeKeys notes are
    /// one-shot, so hold is effectively the attack+decay body.
    /// </summary>
    public double TotalSeconds(double holdSeconds)
        => Math.Max(holdSeconds, Attack + Decay) + Release;

    /// <summary>Envelope amplitude at time <paramref name="t"/> seconds for a note held <paramref name="hold"/> seconds.</summary>
    public double AmplitudeAt(double t, double hold)
    {
        if (t < 0) return 0;
        double bodyEnd = Math.Max(hold, Attack + Decay);

        if (t < Attack)
            return t / Attack;                                   // 0 → 1 ramp
        if (t < Attack + Decay)
            return 1.0 - (1.0 - Sustain) * (t - Attack) / Decay; // 1 → sustain
        if (t < bodyEnd)
            return Sustain;                                       // hold
        double r = t - bodyEnd;
        if (r < Release)
            return Sustain * (1.0 - r / Release);                 // sustain → 0
        return 0;
    }
}
