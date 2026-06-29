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
    // Dreamflow — a flowing, almost-psychedelic late-90s new-age bed. NO kick, NO bass boom:
    // lush detuned pads drifting through a wandering chord progression, a soft floating motif, and
    // shimmer that eases in. The "pad-flow" templates are handled specially by the conductor so they
    // never get the standard Pulse+Bass thumps (see SignalsToBeat.IsPadFlow / Conductor.Step).
    Dreamflow,
    // Celestial Sweep — Dreamflow's lush pad wash PLUS the Chakra Sweep's walking bowl: the bowl
    // climbs Root→Crown (396–963 Hz) over a 21-minute journey while the detuned pad chord drifts
    // through its wandering progression underneath. The "90s new-age flow + chakra sweep" blend.
    // Handled as a pad-flow template (no kick/bass/pulse) with a sweep bowl layered in.
    CelestialSweep,
    // Code Groove — a steady lo-fi coding beat: kick on 1 & 3, snare backbeat on 2 & 4, swung
    // eighth-note hats, a simple root/fifth bassline, and a soft motif that drifts in later. Held
    // steadily present (not the breathing fade) so it grooves the whole work session. Handled as a
    // "groove" template by the conductor (see SignalsToBeat.IsGroove / Conductor.Step).
    CodeGroove,
    // Zion — a Matrix-rave driving techno bed (Fluke "Zion"): a big opening hit that builds to heavy
    // drums — four-on-the-floor kick, thudding rolling bass, tribal toms, a propelling repetitive
    // synth and offbeat hats. Hypnotic + repetitive (good for deep-focus flow), held driving once
    // built. Handled as a "techno" template by the conductor (see SignalsToBeat.IsZion).
    Zion
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
/// <remarks><see cref="Kick"/>/<see cref="Snare"/>/<see cref="Hat"/> are the drum kit (Code Groove +
/// Zion), distinct from the atmospheric <see cref="Pulse"/> sub-hum used by the bowl beds.
/// <see cref="Tom"/> is the tribal tom used by the Zion Matrix-techno bed.</remarks>
public enum BeatLayer { Pad, Pulse, Marimba, Melody, Chime, Bass, Splash, Ghost, Bowl, Kick, Snare, Hat, Tom }

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
