using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;

namespace CodeKeys.Core.Presets;

/// <summary>A baked, ready-to-play preset: the key map plus the rendered voice buffers.</summary>
public sealed record BakedPreset(SpatialKeyMap Map, KeyVoiceSet Voices);

/// <summary>
/// A selectable keystroke sound set. <see cref="Build"/> renders all its voices into RAM
/// for the given sample rate. (Built-in for now; the folder/manifest pack system loads
/// these from disk later — same shape.)
/// </summary>
public sealed class Preset
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    private readonly Func<int, BakedPreset> _build;

    public Preset(string id, string name, string description, Func<int, BakedPreset> build)
    {
        Id = id;
        Name = name;
        Description = description;
        _build = build;
    }

    public BakedPreset Build(int sampleRate) => _build(sampleRate);

    public override string ToString() => Name;
}
