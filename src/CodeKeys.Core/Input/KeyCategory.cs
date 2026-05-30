namespace CodeKeys.Core.Input;

/// <summary>
/// What kind of sound a key press should make. Pitched keys get a note from the
/// spatial map; the rhythm keys (space/enter/backspace) get their own distinct
/// sounds; everything else is silent.
/// </summary>
public enum KeyCategory
{
    /// <summary>No sound (pure modifiers, function keys, arrows, etc.).</summary>
    Silent,
    /// <summary>A pitched note from the 2-octave spatial scale.</summary>
    Pitched,
    Space,
    Enter,
    Backspace
}

/// <summary>
/// The resolved decision for a single key press: its category, and (for pitched
/// keys) the MIDI note and frequency it should sound.
/// </summary>
public readonly record struct KeySound(KeyCategory Category, int MidiNote, double Frequency)
{
    public static readonly KeySound Silent = new(KeyCategory.Silent, 0, 0);

    public bool IsAudible => Category != KeyCategory.Silent;
}
