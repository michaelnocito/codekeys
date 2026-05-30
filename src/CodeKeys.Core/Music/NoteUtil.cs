namespace CodeKeys.Core.Music;

/// <summary>
/// Equal-temperament note math. MIDI note 69 = A4 = 440 Hz.
/// </summary>
public static class NoteUtil
{
    public const int A4Midi = 69;
    public const double A4Hz = 440.0;

    /// <summary>Convert a MIDI note number to its frequency in Hz.</summary>
    public static double MidiToFrequency(int midiNote)
        => A4Hz * Math.Pow(2.0, (midiNote - A4Midi) / 12.0);

    /// <summary>
    /// Parse a note name like "C4", "F#3", "Eb5" into a MIDI note number.
    /// Octave numbering is scientific pitch (C4 = middle C = MIDI 60).
    /// </summary>
    public static int ParseNoteName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Note name is empty.", nameof(name));

        name = name.Trim();
        int i = 0;
        char letter = char.ToUpperInvariant(name[i++]);
        int semitone = letter switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => throw new FormatException($"Invalid note letter '{letter}' in \"{name}\".")
        };

        while (i < name.Length && (name[i] == '#' || name[i] == 'b'))
        {
            semitone += name[i] == '#' ? 1 : -1;
            i++;
        }

        if (i >= name.Length || !int.TryParse(name[i..], out int octave))
            throw new FormatException($"Missing or invalid octave in note name \"{name}\".");

        // MIDI: C-1 = 0, so C4 = 60 => (octave + 1) * 12 + semitone.
        return (octave + 1) * 12 + semitone;
    }
}
