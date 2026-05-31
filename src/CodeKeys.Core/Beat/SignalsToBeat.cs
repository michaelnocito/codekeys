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
            [BeatPreset.Focused] = new(60, 72, BeatScale.Dorian, "D3", new[] { BeatLayer.Pulse, BeatLayer.Bass }),
            [BeatPreset.Relaxed] = new(60, 70, BeatScale.MajorPentatonic, "C3", new[] { BeatLayer.Pad }),
            [BeatPreset.Burnout] = new(75, 88, BeatScale.Major, "F3", new[] { BeatLayer.Pad, BeatLayer.Pulse }),
            [BeatPreset.Silly]   = new(100, 130, BeatScale.MajorPentatonic, "C4", new[] { BeatLayer.Marimba, BeatLayer.Pulse }),
        };

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
