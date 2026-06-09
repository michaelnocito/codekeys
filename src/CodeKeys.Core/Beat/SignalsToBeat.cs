using CodeKeys.Core.Music;

namespace CodeKeys.Core.Beat;

/// <summary>
/// The "brain" (module 2 of 3): pure, deterministic, no audio. Turns typing
/// <see cref="Signals"/> into a <see cref="BeatSpec"/>. Same text always yields the same
/// beat — the FNV-1a seed + mulberry32 PRNG are ported bit-for-bit from the TS original,
/// so it is trivially unit-testable and reproducible.
/// </summary>
public static class SignalsToBeat
{
    // ---- preset ranges (signals pick the point inside them) ----
    private readonly record struct PresetRange(int BpmLo, int BpmHi, BeatScale Scale, string Root, BeatLayer[] Base);

    private static readonly IReadOnlyDictionary<BeatPreset, PresetRange> Presets =
        new Dictionary<BeatPreset, PresetRange>
        {
            [BeatPreset.Focused] = new(60, 72, BeatScale.Dorian, "D3", new[] { BeatLayer.Pulse }),
            [BeatPreset.Relaxed] = new(60, 70, BeatScale.MajorPentatonic, "C3", new[] { BeatLayer.Pad }),
            [BeatPreset.Burnout] = new(75, 88, BeatScale.Major, "F3", new[] { BeatLayer.Pad, BeatLayer.Pulse }),
            [BeatPreset.Silly]   = new(100, 130, BeatScale.MajorPentatonic, "C4", new[] { BeatLayer.Marimba, BeatLayer.Pulse }),
            // Chakra tunings — same low bass hum (D3 → D2 = ~73 Hz) and pulse as Focused, so the
            // bass character Mike likes is preserved. The Tibetan singing bowl is what varies, ringing
            // at the chakra's Solfeggio frequency (see ChakraBowlFreq).
            // Root chakra: slower tempo range (research: root = slow, grounding, "deep tones felt as
            // much as heard"). Lower BPM lets the deep bass be more sustained / felt.
            [BeatPreset.Root]        = new(54, 66, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
            [BeatPreset.Sacral]      = new(60, 72, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
            [BeatPreset.SolarPlexus] = new(60, 72, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
            [BeatPreset.Heart]       = new(60, 72, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
            [BeatPreset.Throat]      = new(60, 72, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
            [BeatPreset.ThirdEye]    = new(60, 72, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
            [BeatPreset.Crown]       = new(60, 72, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
            // Space Clearing — faster pacing (72-84 BPM) so the bowl rings sweep through the room
            // with more motion; 432 Hz bowl ("universe vibration", widely cited for space cleansing).
            [BeatPreset.SpaceClearing] = new(72, 84, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
            // Chakra Sweep — same bed as a single chakra (D3 MajorPentatonic, gentle 60-72 BPM). The
            // singing bowl is what walks up the seven chakras over the 21-minute journey (see
            // ChakraSweepStageAt); everything else stays put so only the bowl colour changes.
            [BeatPreset.ChakraSweep] = new(60, 72, BeatScale.MajorPentatonic, "D3", new[] { BeatLayer.Pulse }),
        };

    // ---- Chakra Sweep: a guided 21-minute walk up the seven chakras ----

    /// <summary>Seconds the bowl dwells on each chakra during the sweep (3 minutes).</summary>
    public const double ChakraSweepStageSeconds = 180.0;

    /// <summary>The seven chakras in ascending order — the order the sweep's bowl walks through.</summary>
    public static readonly IReadOnlyList<BeatPreset> ChakraSweepStages = new[]
    {
        BeatPreset.Root, BeatPreset.Sacral, BeatPreset.SolarPlexus,
        BeatPreset.Heart, BeatPreset.Throat, BeatPreset.ThirdEye, BeatPreset.Crown,
    };

    /// <summary>Total length of the sweep — 7 chakras × 3 min = 21 minutes.</summary>
    public static double ChakraSweepTotalSeconds => ChakraSweepStageSeconds * ChakraSweepStages.Count;

    /// <summary>
    /// Which chakra the sweep's bowl is tuned to at the given elapsed time. Each chakra holds for
    /// <see cref="ChakraSweepStageSeconds"/>; once the journey reaches the top it holds on Crown.
    /// </summary>
    public static BeatPreset ChakraSweepStageAt(double elapsedSeconds)
    {
        int i = (int)Math.Floor(Math.Max(0, elapsedSeconds) / ChakraSweepStageSeconds);
        if (i >= ChakraSweepStages.Count) i = ChakraSweepStages.Count - 1;
        return ChakraSweepStages[i];
    }

    /// <summary>Deterministically derive a beat from typing signals and a mood preset.</summary>
    public static BeatSpec Of(Signals sig, BeatPreset preset)
    {
        var p = Presets[preset];
        string seedStr = string.IsNullOrEmpty(sig.Text) ? PresetName(preset) : sig.Text;
        var rand = new Prng(Fnv.Hash(seedStr));

        // typing speed -> density + tempo bias (fast ~80ms, slow ~500ms)
        double avgGap = sig.AvgGapMs > 0 ? sig.AvgGapMs : 250;
        double speed = NormInv(avgGap, 80, 500);
        double density = Clamp(0.25 + speed * 0.6, 0, 1);
        int bpm = (int)Math.Round(Lerp(p.BpmLo, p.BpmHi, speed), MidpointRounding.AwayFromZero);

        // erratic rhythm -> swing (handle raw-ms or pre-normalized input)
        double variance = sig.GapVariance > 1 ? Clamp(sig.GapVariance / 300, 0, 1) : Clamp(sig.GapVariance, 0, 1);
        double swing = Clamp(variance * 0.6, 0, 0.6);

        // backspaces relative to length -> ghost notes (the secret sauce)
        double ghostNotes = Clamp(sig.Backspaces / (double)Math.Max(sig.CharCount, 8), 0, 1);

        // length -> loop length
        int loopBars = sig.CharCount < 40 ? 2 : sig.CharCount < 100 ? 4 : 8;
        int steps = loopBars * 16;

        // caps + punctuation -> accents
        double punctNorm = Clamp(sig.PunctCount / 6.0, 0, 1);
        int accentCount = (int)Math.Round(
            (Clamp(sig.CapsRatio, 0, 1) + punctNorm) * 0.5 * (steps / 4.0),
            MidpointRounding.AwayFromZero);

        var seen = new HashSet<int>();
        var accentList = new List<int>(accentCount);
        for (int i = 0; i < accentCount; i++)
        {
            int v = (int)Math.Floor(rand.Next() * steps);
            if (seen.Add(v)) accentList.Add(v); // keep first occurrence (matches TS filter)
        }
        accentList.Sort();
        var accents = accentList.ToArray();

        // layers
        var layers = new List<BeatLayer>(p.Base);
        if (ghostNotes > 0.15 && !layers.Contains(BeatLayer.Ghost)) layers.Add(BeatLayer.Ghost);
        if (loopBars >= 4 && !layers.Contains(BeatLayer.Melody)) layers.Add(BeatLayer.Melody);

        return new BeatSpec(preset, bpm, p.Scale, p.Root, loopBars, density, swing, ghostNotes, accents, layers.ToArray());
    }

    /// <summary>
    /// Drift the loop each cycle so it doesn't get stale. Keeps tonal identity
    /// (preset/scale/root/bpm/loopBars); rerolls only density + accents. Near-zero cost.
    /// </summary>
    public static BeatSpec Evolve(BeatSpec spec, int cycle)
    {
        uint seed = unchecked(Fnv.Hash(PresetName(spec.Preset)) ^ ((uint)cycle * 2654435761u));
        var rand = new Prng(seed);
        int steps = spec.LoopBars * 16;

        double density = Clamp(spec.Density + (rand.Next() - 0.5) * 0.2, 0.15, 1);

        var accents = new int[spec.Accents.Length];
        for (int i = 0; i < accents.Length; i++)
            accents[i] = (int)Clamp(spec.Accents[i] + Math.Floor((rand.Next() - 0.5) * 4), 0, steps - 1);

        return spec with { Density = density, Accents = accents };
    }

    // ---- tonal bridge: BeatSpec -> CodeKeys music types (so key-sounds read the same scale) ----

    public static Scale ToScale(BeatScale scale) => scale switch
    {
        BeatScale.Dorian => Music.Scale.Dorian,
        BeatScale.Major => Music.Scale.Major,
        BeatScale.MajorPentatonic => Music.Scale.MajorPentatonic,
        _ => Music.Scale.MajorPentatonic
    };

    /// <summary>The root note of a spec as a MIDI number (for the spatial key map / synth).</summary>
    public static int RootMidi(BeatSpec spec) => NoteUtil.ParseNoteName(spec.Root);

    /// <summary>The tempo range a preset can move within — the Conductor maps arousal onto it.</summary>
    public static (int Lo, int Hi) BpmRange(BeatPreset preset)
    {
        var p = Presets[preset];
        return (p.BpmLo, p.BpmHi);
    }

    /// <summary>
    /// The Solfeggio bowl frequency (Hz) for a chakra preset; <c>null</c> for non-chakra moods. These
    /// are the seven Solfeggio frequencies widely associated with the chakras in sound-healing practice
    /// (Dr Joseph Puleo, 1970s) — not peer-reviewed medicine, but the strongest belief-grounded set,
    /// per Mike's "studied or strongly believed" framing.
    /// </summary>
    public static double? ChakraBowlFreq(BeatPreset preset) => preset switch
    {
        BeatPreset.Root          => 396.0,
        BeatPreset.Sacral        => 417.0,
        BeatPreset.SolarPlexus   => 528.0,
        BeatPreset.Heart         => 639.0,
        BeatPreset.Throat        => 741.0,
        BeatPreset.ThirdEye      => 852.0,
        BeatPreset.Crown         => 963.0,
        BeatPreset.SpaceClearing => 432.0,  // "universe vibration" / healing frequency for space cleansing
        // The sweep's NOMINAL/opening bowl is the Root (396 Hz); the live bowl frequency walks up the
        // chakras over the journey and is selected per stage by the renderer (BeatSequencer), not here.
        // Returning a value (rather than null) keeps the sweep on the "musical bass + bowl from t=0"
        // code path shared by every chakra template.
        BeatPreset.ChakraSweep => 396.0,
        _ => null,
    };

    /// <summary>The sentinel MIDI used as a bank key for the chakra bowl voice (must not collide
    /// with the natural scale-degree MIDIs used for other bowl voices).</summary>
    public static int ChakraBowlMidi(BeatPreset preset)
    {
        var f = ChakraBowlFreq(preset);
        return f.HasValue ? (int)Math.Round(NoteUtil.FrequencyToMidi(f.Value)) : -1;
    }

    // ---- helpers (ported 1:1) ----

    private static double Clamp(double x, double lo, double hi) => Math.Min(hi, Math.Max(lo, x));
    private static double Lerp(double a, double b, double t) => a + (b - a) * Clamp(t, 0, 1);

    /// <summary>value in [fast,slow] -> 1..0 (fast typing = high).</summary>
    private static double NormInv(double v, double fast, double slow) =>
        Clamp(1 - (v - fast) / (slow - fast), 0, 1);

    internal static string PresetName(BeatPreset p) => p switch
    {
        BeatPreset.Focused => "focused",
        BeatPreset.Relaxed => "relaxed",
        BeatPreset.Burnout => "burnout",
        BeatPreset.Silly => "silly",
        _ => "focused"
    };
}
