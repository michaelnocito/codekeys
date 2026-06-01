using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Music;

namespace CodeKeys.Core.Presets;

/// <summary>
/// The selectable keystroke sound packs. The BEAT (bowls + bass bed) is unchanged across all of
/// them — these only change what each keystroke sounds like.
///
/// Six are exposed:
///   • "Deep &amp; Warm"  — the default: deep bass thump + drum pops + an occasional smooth synth.
///   • "Soft Mallet"   — mellow wooden marimba.
///   • "Warm Keys"     — warm Rhodes electric piano.
///   • "Felt Piano"    — soft, muted acoustic piano.
///   • "Water Drops"   — gentle liquid droplets.
///   • "Boings"        — deliberately silly cartoon boings/pops/zaps (breaks the calm on purpose).
///
/// The four "complementary" pitched packs (Soft Mallet / Warm Keys / Felt Piano / Water Drops) are
/// tuned to D major-pentatonic — the key of the chakra / Space Clearing / Sweep beds — so the keys
/// stay consonant with whatever beat is playing. The default is bass-percussive (key-neutral), and
/// the silly pack ignores the rules. Older voicings (Keyboard / Pulse / Thock / After Dark /
/// Electric) are kept private below for easy revival.
/// </summary>
public static class PresetLibrary
{
    public static IReadOnlyList<Preset> All { get; } = new[]
    {
        DeepWarm(),   // default
        SoftMallet(),
        WarmKeys(),
        FeltPiano(),
        WaterDrops(),
        Boings(),     // silly
    };

    public static Preset Default => ById("deep-warm") ?? All[0]; // the deep-beat blend Mike dialed in

    public static Preset? ById(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    // ---- the six exposed packs ----

    // --- Deep & Warm (default): deep bass thump + drum pops + occasional smooth synth/snare ---
    private static Preset DeepWarm() => new(
        "deep-warm", "Deep & Warm",
        "Deep bass thump + drum pops, with an occasional smooth synth and snare. The default set.",
        rate =>
        {
            // Low + two octaves so there's room for zones: thumps low, pops mid, synth up top.
            var map = new SpatialKeyMap(Scale.MinorPentatonic, NoteUtil.ParseNoteName("A2"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var smoothEnv = new Envelope { Attack = 0.010, Decay = 0.18, Sustain = 0.40, Release = 0.26 };

            SampleBuffer RenderBlend(int i, int n, double freq)
            {
                // Top ~25% of pitches → smooth synth; the rest alternate deep thump / drum pop.
                if (i >= n * 0.75)
                    return SynthVoiceFactory.CreateTone(freq, rate, Waveform.WarmPad, smoothEnv, holdSeconds: 0.16, gain: 0.5f);
                return (i % 2 == 0)
                    ? PercussionFactory.CreateKick(freq, rate, bodyDecaySeconds: 0.22, clickAmount: 0.08)
                    : PercussionFactory.CreateTap(freq, rate, decaySeconds: 0.07, noiseAmount: 0.20);
            }

            var space = PercussionFactory.CreateKick(root * 0.75, rate, bodyDecaySeconds: 0.26, clickAmount: 0.07);
            var enter = PercussionFactory.CreateSnare(rate, decaySeconds: 0.16);
            var backspace = PercussionFactory.CreateTap(root, rate, decaySeconds: 0.06, noiseAmount: 0.18);

            var voices = KeyVoiceSet.BakeNotes(map, rate, RenderBlend, space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Soft Mallet: mellow wooden marimba (in D pentatonic to sit with the bed) ---
    private static Preset SoftMallet() => new(
        "soft-mallet", "Soft Mallet",
        "Mellow wooden marimba — soft and rounded.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("D3"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = InstrumentFactory.CreateMarimba(root / 2.0, rate);
            var enter = InstrumentFactory.CreateMarimba(root * 2.0, rate);
            var backspace = InstrumentFactory.CreateMarimba(root / 1.5, rate, 0.25);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => InstrumentFactory.CreateMarimba(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Warm Keys: warm FM electric piano (Rhodes), in D pentatonic ---
    private static Preset WarmKeys() => new(
        "warm-keys", "Warm Keys",
        "Warm electric-piano (Rhodes) tone — lush and soft.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("D3"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = InstrumentFactory.CreateRhodes(root / 2.0, rate, 0.45);
            var enter = InstrumentFactory.CreateRhodes(root * 2.0, rate, 0.45);
            var backspace = InstrumentFactory.CreateRhodes(root / 1.5, rate, 0.3);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => InstrumentFactory.CreateRhodes(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Felt Piano: soft, muted acoustic piano, in D pentatonic ---
    private static Preset FeltPiano() => new(
        "felt-piano", "Felt Piano",
        "Soft, muted acoustic piano — intimate and quiet.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("D3"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            // "Felt/muted" = lower gain (the 3rd positional arg is durationSeconds, so pass gain by name).
            var space = InstrumentFactory.CreatePiano(root / 2.0, rate, gain: 0.50f);
            var enter = InstrumentFactory.CreatePiano(root * 2.0, rate, gain: 0.50f);
            var backspace = InstrumentFactory.CreatePiano(root / 1.5, rate, durationSeconds: 0.40, gain: 0.40f);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => InstrumentFactory.CreatePiano(f, rate, gain: 0.55f),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Water Drops: gentle liquid droplets, in D pentatonic (meditative, pairs with the bowls) ---
    private static Preset WaterDrops() => new(
        "water-drops", "Water Drops",
        "Soft liquid droplets — gentle and meditative.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("D3"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = ToyVoiceFactory.CreateDroplet(root / 2.0, rate, decaySeconds: 0.28);
            var enter = ToyVoiceFactory.CreateDroplet(root * 1.5, rate, decaySeconds: 0.26);
            var backspace = ToyVoiceFactory.CreateDroplet(root / 1.3, rate, decaySeconds: 0.16, gain: 0.6f);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => ToyVoiceFactory.CreateDroplet(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // --- Boings (silly): cartoon boings, pops, and zaps — pure fun, breaks the calm on purpose ---
    private static Preset Boings() => new(
        "boings", "Boings (silly)",
        "Cartoon boings, pops, and zaps. Pure fun — breaks the calm on purpose.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = ToyVoiceFactory.CreatePop(root, rate);
            var enter = ToyVoiceFactory.CreateBoing(root / 2.0, rate, decaySeconds: 0.5, wobbleDepth: 0.26); // big sproing
            var backspace = ToyVoiceFactory.CreateZap(root * 2.0, rate);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => ToyVoiceFactory.CreateBoing(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    // ---- dormant voicings (kept for easy revival; not exposed) ----

    private static Preset Keyboard() => new(
        "keyboard", "Keyboard (melodic)",
        "The fun pitched instrument. More distracting — kept as a template.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);
            var voices = KeyVoiceSet.BakeSynth(map, rate, Waveform.WarmPad, Envelope.Pluck);
            return new BakedPreset(map, voices);
        });

    private static Preset Pulse() => new(
        "pulse", "Pulse (low beat)",
        "Soft low kick per key. Punchy but calm — designed to fade into the background.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MinorPentatonic, NoteUtil.ParseNoteName("A2"), octaves: 1);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = PercussionFactory.CreateKick(root * 0.75, rate, bodyDecaySeconds: 0.24, clickAmount: 0.08);
            var enter = PercussionFactory.CreateKick(root, rate, clickAmount: 0.22);
            var backspace = PercussionFactory.CreateKick(root * 0.9, rate, bodyDecaySeconds: 0.12, clickAmount: 0.06);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => PercussionFactory.CreateKick(f, rate),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

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

    private static Preset AfterDark() => new(
        "after-dark", "After Dark (dark pluck)",
        "Low plucky synth-bass + 808 sub & clap. Inspired by The Weeknd × Daft Punk, 'Starboy'.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MinorPentatonic, NoteUtil.ParseNoteName("A2"), octaves: 1);

            var space = PercussionFactory.CreateSub(55, rate, decaySeconds: 0.30);
            var enter = PercussionFactory.CreateSnare(rate, decaySeconds: 0.13, toneAmount: 0.15);
            var backspace = PercussionFactory.CreateKick(70, rate, bodyDecaySeconds: 0.10);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => StringFactory.CreatePluckedString(f, rate, durationSeconds: 0.32, decay: 0.99, brightness: 0.45),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });

    private static Preset Electric() => new(
        "electric", "Electric (guitar)",
        "Clean plucked electric-guitar tone across two octaves.",
        rate =>
        {
            var map = new SpatialKeyMap(Scale.MinorPentatonic, NoteUtil.ParseNoteName("E2"), octaves: 2);
            double root = NoteUtil.MidiToFrequency(map.RootMidi);

            var space = StringFactory.CreatePluckedString(root, rate, durationSeconds: 0.20, decay: 0.985, brightness: 0.3);
            var enter = StringFactory.CreatePluckedString(root * 2.0, rate, durationSeconds: 0.35, decay: 0.996, brightness: 0.7);
            var backspace = PercussionFactory.CreateTap(120, rate, decaySeconds: 0.05, noiseAmount: 0.15);

            var voices = KeyVoiceSet.Bake(map, rate,
                f => StringFactory.CreatePluckedString(f, rate, durationSeconds: 0.5, decay: 0.996, brightness: 0.6),
                space, enter, backspace);
            return new BakedPreset(map, voices);
        });
}
