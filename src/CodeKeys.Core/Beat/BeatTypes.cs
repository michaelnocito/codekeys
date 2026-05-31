namespace CodeKeys.Core.Beat;

/// <summary>The mood/character of a generated beat. Each maps to a tempo range, scale, root, and base layers.</summary>
public enum BeatPreset { Focused, Relaxed, Burnout, Silly }

/// <summary>The scales a beat can use. This is the tonal source of truth key-sounds must read.</summary>
public enum BeatScale { Dorian, Major, MajorPentatonic }

/// <summary>The voices a beat can activate. <see cref="Melody"/> plays the developing motif.</summary>
public enum BeatLayer { Pad, Pulse, Marimba, Melody, Ghost }

/// <summary>
/// Typing telemetry captured over a session window. Fed into <see cref="SignalsToBeat"/>.
/// Mirrors the TS <c>Signals</c> interface.
/// </summary>
public sealed record Signals
{
    public string Text { get; init; } = "";
    public double DurationMs { get; init; }
    public int CharCount { get; init; }
    public int Backspaces { get; init; }

    /// <summary>Mean ms between keys.</summary>
    public double AvgGapMs { get; init; }

    /// <summary>Rhythm steadiness — raw ms std-dev OR 0..1; normalized defensively.</summary>
    public double GapVariance { get; init; }

    /// <summary>0..1.</summary>
    public double CapsRatio { get; init; }

    public int PunctCount { get; init; }
}

/// <summary>
/// The deterministic output of the brain: everything a renderer needs to play a beat.
/// <see cref="Scale"/> + <see cref="Root"/> are the single tonal source of truth — key-sound
/// presets must take their notes from these so they can never clash with the bed.
/// Mirrors the TS <c>BeatSpec</c> interface.
/// </summary>
public sealed record BeatSpec(
    BeatPreset Preset,
    int Bpm,
    BeatScale Scale,
    string Root,
    int LoopBars,        // 2 | 4 | 8
    double Density,      // 0..1
    double Swing,        // 0..0.6
    double GhostNotes,   // 0..1
    int[] Accents,       // step indices 0 .. LoopBars*16 - 1
    BeatLayer[] Layers);
