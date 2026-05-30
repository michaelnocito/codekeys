namespace CodeKeys.Core.Music;

/// <summary>
/// A musical scale defined by its semitone offsets within one octave (starting at 0).
/// Used to lay keys out "in key" so no keystroke ever sounds wrong against another.
/// </summary>
public sealed class Scale
{
    /// <summary>Semitone offsets from the root, ascending, within a single octave (0..11).</summary>
    public IReadOnlyList<int> Intervals { get; }

    public string Name { get; }

    public Scale(string name, IReadOnlyList<int> intervals)
    {
        if (intervals is null || intervals.Count == 0)
            throw new ArgumentException("A scale needs at least one interval.", nameof(intervals));
        Name = name;
        Intervals = intervals;
    }

    public static readonly Scale MajorPentatonic =
        new("major-pentatonic", new[] { 0, 2, 4, 7, 9 });

    public static readonly Scale MinorPentatonic =
        new("minor-pentatonic", new[] { 0, 3, 5, 7, 10 });

    public static readonly Scale Major =
        new("major", new[] { 0, 2, 4, 5, 7, 9, 11 });

    public static readonly Scale NaturalMinor =
        new("minor", new[] { 0, 2, 3, 5, 7, 8, 10 });

    /// <summary>Look a scale up by its manifest name; falls back to major pentatonic.</summary>
    public static Scale FromName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "major-pentatonic" or "pentatonic" => MajorPentatonic,
        "minor-pentatonic" => MinorPentatonic,
        "major" => Major,
        "minor" or "natural-minor" => NaturalMinor,
        _ => MajorPentatonic
    };

    /// <summary>
    /// The MIDI note for a scale degree counted from <paramref name="rootMidi"/>.
    /// Degree 0 is the root; degrees past the top of the octave wrap up into the next octave.
    /// Negative degrees are supported (wrap downward).
    /// </summary>
    public int DegreeToMidi(int rootMidi, int degree)
    {
        int n = Intervals.Count;
        // Floor-division so negative degrees wrap correctly.
        int octave = (int)Math.Floor(degree / (double)n);
        int idx = degree - octave * n; // 0..n-1
        return rootMidi + octave * 12 + Intervals[idx];
    }

    /// <summary>Number of distinct scale degrees spanning <paramref name="octaves"/> octaves (inclusive of the top root).</summary>
    public int DegreeSpan(int octaves) => Intervals.Count * octaves + 1;
}
