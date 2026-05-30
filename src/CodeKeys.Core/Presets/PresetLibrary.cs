using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Music;

namespace CodeKeys.Core.Presets;

/// <summary>
/// The built-in keystroke presets.
///
///   • "Keyboard" — the melodic template: a fun, pitched 2-octave instrument. Higher
///     cognitive interference (every key a different pitch = changing-state), kept as a
///     starting point to clone.
///   • "Pulse" / "Thock" — low-cognitive-interference percussive sets: low frequency,
///     very narrow pitch variation (near steady-state -> habituates), short decay,
///     consistent timbre. The satisfying "beat" comes from the transient + low body,
///     not from melody.
///
/// Pitches/lengths here are intentionally conservative and easy to tune by ear.
/// </summary>
public static class PresetLibrary
{
    public static IReadOnlyList<Preset> All { get; } = new[]
    {
        Keyboard(),
        Pulse(),
        Thock(),
    };

    public static Preset Default => All[1]; // Pulse — the low-beat set Mike asked to lead with

    public static Preset? ById(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    // --- Keyboard: the melodic template (current behaviour) ---
    private static Preset Keyboard() => new(
        "keyboard", "Keyboard (melodic)",
        "The fun pitched instrument. More distracting — kept as a template.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);
            var voices = KeyVoiceSet.BakeSynth(map, rate, Waveform.WarmPad, Envelope.Pluck);
            return new BakedPreset(map, voices);
        });

    // --- Pulse: soft low kick, the satisfying low beat ---
    private static Preset Pulse() => new(
        "pulse", "Pulse (low beat)",
        "Soft low kick per key. Punchy but calm — designed to fade into the background.",
        rate =>
        {
            // Narrow, low range (one octave from A2 ≈ 110 Hz) → minimal pitch variation.
            var map = new SpatialKeyMap(Scale.MinorPentatonic, NoteUtil.ParseNoteName("A2"), octaves: 1);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = PercussionFactory.CreateKick(root * 0.75, rate, bodyDecaySeconds: 0.24, clickAmount: 0.08);
            var enter = PercussionFactory.CreateKick(root, rate, clickAmount: 0.22);            // slightly snappier accent
            var backspace = PercussionFactory.CreateKick(root * 0.9, rate, bodyDecaySeconds: 0.12, clickAmount: 0.06);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => PercussionFactory.CreateKick(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Thock: deep mechanical tap, lowest interference ---
    private static Preset Thock() => new(
        "thock", "Thock (deep tap)",
        "Deep wooden key-tap. The most neutral, lowest-distraction set.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("A2"), octaves: 1);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = PercussionFactory.CreateTap(root * 0.8, rate, decaySeconds: 0.11, noiseAmount: 0.18);
            var enter = PercussionFactory.CreateTap(root * 1.5, rate, decaySeconds: 0.08, noiseAmount: 0.3);
            var backspace = PercussionFactory.CreateTap(root * 0.9, rate, decaySeconds: 0.06, noiseAmount: 0.2);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => PercussionFactory.CreateTap(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });
}
