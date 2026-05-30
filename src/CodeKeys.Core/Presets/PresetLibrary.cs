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
        NeonNights(),
        AfterDark(),
        Electric(),
        GrandPiano(),
        Rhodes(),
        Marimba(),
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

    // --- Neon Nights: 80s synthwave (inspired by The Weeknd, "Blinding Lights") ---
    private static Preset NeonNights() => new(
        "neon-nights", "Neon Nights (synthwave)",
        "Bright detuned-saw synth + gated beat. Inspired by The Weeknd, 'Blinding Lights'.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MinorPentatonic, NoteUtil.ParseNoteName("F3"), octaves: 2);
            var env = new Envelope { Attack = 0.005, Decay = 0.12, Sustain = 0.30, Release = 0.16 };

            var space = PercussionFactory.CreateKick(55, rate, bodyDecaySeconds: 0.18, clickAmount: 0.10);   // four-on-floor kick
            var enter = PercussionFactory.CreateSnare(rate, decaySeconds: 0.18);                              // gated snare
            var backspace = PercussionFactory.CreateKick(55, rate, bodyDecaySeconds: 0.10, clickAmount: 0.08);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => InstrumentFactory.CreateSuperSaw(f, rate, env, holdSeconds: 0.14),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- After Dark: dark sparse R&B (inspired by The Weeknd × Daft Punk, "Starboy") ---
    private static Preset AfterDark() => new(
        "after-dark", "After Dark (dark pluck)",
        "Low plucky synth-bass + 808 sub & clap. Inspired by The Weeknd × Daft Punk, 'Starboy'.",
        rate =>
        {
            // A-minor, low and narrow for that moody, bouncy backbone.
            var map = new SpatialKeyMap(Scale.MinorPentatonic, NoteUtil.ParseNoteName("A2"), octaves: 1);

            var space = PercussionFactory.CreateSub(55, rate, decaySeconds: 0.30);                 // 808 sub kick
            var enter = PercussionFactory.CreateSnare(rate, decaySeconds: 0.13, toneAmount: 0.15); // clap
            var backspace = PercussionFactory.CreateKick(70, rate, bodyDecaySeconds: 0.10);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => StringFactory.CreatePluckedString(f, rate, durationSeconds: 0.32, decay: 0.99, brightness: 0.45),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Electric: clean electric guitar (Karplus–Strong) ---
    private static Preset Electric() => new(
        "electric", "Electric (guitar)",
        "Clean plucked electric-guitar tone across two octaves.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MinorPentatonic, NoteUtil.ParseNoteName("E2"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = StringFactory.CreatePluckedString(root, rate, durationSeconds: 0.20, decay: 0.985, brightness: 0.3); // palm-mute
            var enter = StringFactory.CreatePluckedString(root * 2.0, rate, durationSeconds: 0.35, decay: 0.996, brightness: 0.7);
            var backspace = PercussionFactory.CreateTap(120, rate, decaySeconds: 0.05, noiseAmount: 0.15);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => StringFactory.CreatePluckedString(f, rate, durationSeconds: 0.5, decay: 0.996, brightness: 0.6),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Grand Piano ---
    private static Preset GrandPiano() => new(
        "piano", "Grand Piano",
        "Acoustic piano tone (additive synthesis).",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = InstrumentFactory.CreatePiano(root / 2.0, rate, 0.5);
            var enter = InstrumentFactory.CreatePiano(root * 2.0, rate, 0.5);
            var backspace = InstrumentFactory.CreatePiano(root / 1.5, rate, 0.3);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => InstrumentFactory.CreatePiano(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Rhodes: warm electric piano (bonus) ---
    private static Preset Rhodes() => new(
        "rhodes", "Rhodes (electric piano)",
        "Warm FM electric-piano tone — softer than the grand.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = InstrumentFactory.CreateRhodes(root / 2.0, rate, 0.45);
            var enter = InstrumentFactory.CreateRhodes(root * 2.0, rate, 0.45);
            var backspace = InstrumentFactory.CreateRhodes(root / 1.5, rate, 0.3);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => InstrumentFactory.CreateRhodes(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Marimba: soft wooden mallet (bonus) ---
    private static Preset Marimba() => new(
        "marimba", "Marimba",
        "Soft wooden mallet — mellow and rounded.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = InstrumentFactory.CreateMarimba(root / 2.0, rate);
            var enter = InstrumentFactory.CreateMarimba(root * 2.0, rate);
            var backspace = InstrumentFactory.CreateMarimba(root / 1.5, rate, 0.25);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => InstrumentFactory.CreateMarimba(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });
}
