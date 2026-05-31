namespace CodeKeys.Core.Beat;

/// <summary>
/// One note of a motif: a scale <see cref="Degree"/> (relative to the motif's base octave),
/// the 16th-note <see cref="Step"/> it lands on within a one-bar motif, and its relative
/// loudness. Pitch is stored as a scale degree (not MIDI) so a motif is key-agnostic — the
/// renderer resolves degrees against the spec's scale + root, which keeps every note in key.
/// </summary>
public readonly record struct MotifNote(int Degree, int Step, double Gain);

/// <summary>
/// A short melodic cell — the seed of a tune. A motif spans exactly one bar
/// (<see cref="Steps"/> sixteenth-note steps). Unlike a scale run, a motif has contour and
/// rests so it breathes; <see cref="MotifFactory"/> grows one from a seed, and the transforms
/// (transpose / invert / resolve) are the raw material for developing it over a session.
/// </summary>
public sealed record Motif(IReadOnlyList<MotifNote> Notes)
{
    /// <summary>Sixteenth-note steps in one bar (4/4). A motif occupies a single bar.</summary>
    public const int Steps = 16;
}

/// <summary>
/// Deterministic melody generation. Same seed → same motif, so the whole beat stays
/// reproducible and unit-testable (it shares the FNV/mulberry32 infra with the brain).
/// The generator favours the shape real melodies tend to take — mostly stepwise motion with a
/// gentle pull back toward the tonic and the occasional resolving leap — which is the opposite
/// of a relentless scale run.
/// </summary>
public static class MotifFactory
{
    // Candidate onset steps within a bar: the 8th-note grid plus two syncopated 16ths so the
    // rhythm isn't robotic. Picking a subset (with rests left over) is what makes a motif breathe.
    private static readonly int[] StepGrid = { 0, 2, 3, 4, 6, 8, 10, 11, 12, 14 };

    // Stable scale tones a phrase can start or land on: tonic, 3rd, 5th.
    private static readonly int[] StableTones = { 0, 2, 4 };

    /// <summary>
    /// Grow a one-bar motif from <paramref name="seed"/>. <paramref name="scaleDegrees"/> is the
    /// number of degrees in one octave of the target scale (5 for pentatonic, 7 for diatonic);
    /// it sets the melodic register (~one octave) so the tune stays singable.
    /// </summary>
    public static Motif Generate(uint seed, int scaleDegrees)
    {
        var rng = new Prng(seed);
        int top = Math.Max(4, scaleDegrees); // ceiling for the melodic register (~one octave)

        // Rhythm: 4..7 notes placed on distinct grid steps, downbeat always articulated.
        int noteCount = 4 + (int)(rng.Next() * 4);
        var steps = PickSteps(rng, noteCount);

        // Pitch contour: open on a chord tone, move mostly by step, resolve onto a stable tone.
        var notes = new List<MotifNote>(steps.Count);
        int degree = Pick(rng, StableTones, top);   // the opening note (emitted as-is below)
        for (int i = 0; i < steps.Count; i++)
        {
            if (i == 0) { /* keep the chord-tone opening */ }
            else if (i == steps.Count - 1) degree = Pick(rng, StableTones, top); // resolve
            else degree = NextDegree(rng, degree, top);
            double gain = steps[i] % 4 == 0 ? 0.5 : 0.4; // notes on the beat sing a touch louder
            notes.Add(new MotifNote(degree, steps[i], gain));
        }
        return new Motif(notes);
    }

    /// <summary>Shift every note by <paramref name="byDegrees"/> scale steps (a sequence/transposition).</summary>
    public static Motif Transpose(Motif m, int byDegrees) =>
        new(m.Notes.Select(n => n with { Degree = n.Degree + byDegrees }).ToList());

    /// <summary>Mirror the contour around the first note's pitch (classic motivic inversion).</summary>
    public static Motif Invert(Motif m)
    {
        if (m.Notes.Count == 0) return m;
        int axis = m.Notes[0].Degree;
        return new(m.Notes.Select(n => n with { Degree = 2 * axis - n.Degree }).ToList());
    }

    /// <summary>Force the final note onto the tonic — turns a statement into a resolved answer.</summary>
    public static Motif WithResolvedEnding(Motif m)
    {
        if (m.Notes.Count == 0) return m;
        var notes = m.Notes.ToList();
        int last = notes.Count - 1;
        notes[last] = notes[last] with { Degree = 0 };
        return new(notes);
    }

    // ---- helpers ----

    private static List<int> PickSteps(Prng rng, int count)
    {
        var chosen = new SortedSet<int> { 0 }; // always land the downbeat
        // Bound the loop defensively: the grid has more slots than count ever needs.
        for (int guard = 0; chosen.Count < count && guard < 64; guard++)
            chosen.Add(StepGrid[(int)(rng.Next() * StepGrid.Length)]);
        return chosen.ToList();
    }

    private static int Pick(Prng rng, int[] choices, int top)
    {
        int v = choices[(int)(rng.Next() * choices.Length)];
        return Math.Clamp(v, 0, top);
    }

    /// <summary>
    /// Weighted next-degree move: usually a step, sometimes a repeat or a small leap, rarely a
    /// larger one. Out-of-range moves reflect back into the register, which also nudges the line
    /// back toward the centre (a soft "tonic gravity").
    /// </summary>
    private static int NextDegree(Prng rng, int cur, int top)
    {
        double r = rng.Next();
        int move =
            r < 0.55 ? (rng.Next() < 0.5 ? -1 : 1) : // step (most common)
            r < 0.75 ? 0 :                            // repeat
            r < 0.92 ? (rng.Next() < 0.5 ? -2 : 2) :  // small leap
                       (rng.Next() < 0.5 ? -3 : 3);   // larger leap (rare)

        int next = cur + move;
        if (next < 0) next = -next;                   // reflect off the floor
        if (next > top) next = top - (next - top);    // reflect off the ceiling
        return Math.Clamp(next, 0, top);
    }
}
