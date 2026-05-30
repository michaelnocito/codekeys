using CodeKeys.Core.Music;

namespace CodeKeys.Core.Input;

/// <summary>
/// Maps each typing key to a fixed note across a 2-octave in-key scale, laid out
/// low→high over the physical keyboard. Resolution is stateless and deterministic:
/// the same key always returns the same note, so the same word always plays the
/// same phrase. Space/Enter/Backspace resolve to their own rhythm categories;
/// pure modifiers and unmapped keys are silent.
/// </summary>
public sealed class SpatialKeyMap
{
    private readonly Dictionary<int, KeySound> _pitched;

    public Scale Scale { get; }
    public int RootMidi { get; }
    public int Octaves { get; }

    public SpatialKeyMap(Scale scale, int rootMidi, int octaves = 2, IReadOnlyList<int>? keyOrder = null)
    {
        if (octaves < 1) throw new ArgumentOutOfRangeException(nameof(octaves));
        Scale = scale;
        RootMidi = rootMidi;
        Octaves = octaves;

        var order = keyOrder ?? KeyboardLayout.DefaultOrder;
        _pitched = BuildPitchedMap(scale, rootMidi, octaves, order);
    }

    private static Dictionary<int, KeySound> BuildPitchedMap(
        Scale scale, int rootMidi, int octaves, IReadOnlyList<int> order)
    {
        var map = new Dictionary<int, KeySound>(order.Count);
        int n = order.Count;
        int span = scale.DegreeSpan(octaves); // number of degrees across the range

        for (int i = 0; i < n; i++)
        {
            // Spread the keys proportionally across the full 2-octave degree span,
            // so the lowest key is the root and the highest key is the top octave root.
            int degree = n == 1 ? 0 : (int)Math.Round(i * (span - 1) / (double)(n - 1));
            int midi = scale.DegreeToMidi(rootMidi, degree);
            map[order[i]] = new KeySound(KeyCategory.Pitched, midi, NoteUtil.MidiToFrequency(midi));
        }
        return map;
    }

    /// <summary>Resolve the sound for a virtual-key code. Never throws.</summary>
    public KeySound Resolve(int virtualKey)
    {
        switch (virtualKey)
        {
            case VirtualKey.Space:
                return new KeySound(KeyCategory.Space, 0, 0);
            case VirtualKey.Enter:
                return new KeySound(KeyCategory.Enter, 0, 0);
            case VirtualKey.Back:
                return new KeySound(KeyCategory.Backspace, 0, 0);
        }

        if (VirtualKey.IsPureModifier(virtualKey))
            return KeySound.Silent;

        return _pitched.TryGetValue(virtualKey, out var sound) ? sound : KeySound.Silent;
    }

    /// <summary>The number of keys that resolve to a pitched note (for diagnostics/tests).</summary>
    public int PitchedKeyCount => _pitched.Count;
}
