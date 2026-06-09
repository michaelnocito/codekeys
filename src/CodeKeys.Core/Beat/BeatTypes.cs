namespace CodeKeys.Core.Beat;

/// <summary>
/// The mood/character of a generated beat. Each maps to a tempo range, scale, root, and base layers.
/// The seven chakra entries keep the same low bass hum but tune the singing bowl to the Solfeggio
/// frequency associated with that chakra (396, 417, 528, 639, 741, 852, 963 Hz).
/// </summary>
public enum BeatPreset
{
    Focused, Relaxed, Burnout, Silly,
    // Chakra tunings — same low bass hum + a singing bowl at the Solfeggio frequency.
    Root, Sacral, SolarPlexus, Heart, Throat, ThirdEye, Crown,
    // Space Clearing — bowl tuned to 432 Hz ("universe vibration"); faster pacing for sweeping energy.
    SpaceClearing,
    // Chakra Sweep — a guided 21-minute journey: the bowl walks UP the seven chakras, 3 min each
    // (Root→Crown), over a steady bass+bowl bed. The bowl frequency is time-driven, not fixed.
    ChakraSweep,
    // Focus — evidence-based classroom / deep-work preset. Steady 60-68 BPM Dorian groove with a
    // continuous 40 Hz isochronic tone (340 Hz carrier, AM-modulated) baked into the ambient bed,
    // plus a white-noise floor. Research: 40 Hz gamma + white noise improved sustained attention
    // (p=0.002, n=64, within-subjects crossover, Scientific Reports 2025, PMC11799511).
    // NOTE: engine is mono so these are isochronic (monaural) rather than binaural beats — works
    // on speakers as well as headphones.
    Focus
}

/// <summary>The scales a beat can use. This is the tonal source of truth key-sounds must read.</summary>
public enum BeatScale { Dorian, Major, MajorPentatonic }

/// <summary>
/// The voices a beat can activate. <see cref="Bass"/> = deep low boom (the body); Pulse + Ghost =
/// the drums; <see cref="Splash"/> = a rare soft dark one-off for variety; <see cref="Bowl"/> = a
/// Tibetan singing bowl strike that rings for a couple seconds (sparse, atmospheric).
/// <see cref="Pad"/>/<see cref="Melody"/>/<see cref="Marimba"/>/<see cref="Chime"/> are kept in
/// code but no longer used by the conductor (a chord or high tones that pulled focus).
/// </summary>
public enum BeatLayer { Pad, Pulse, Marimba, Melody, Chime, Bass, Splash, Ghost, Bowl }

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
