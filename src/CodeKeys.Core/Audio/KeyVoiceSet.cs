using CodeKeys.Core.Input;
using CodeKeys.Core.Music;

namespace CodeKeys.Core.Audio;

/// <summary>
/// A fully pre-baked set of keystroke voices: one buffer per pitched key plus the
/// three rhythm sounds (space/enter/backspace). Built once at pack-load so playing a
/// key is a pure RAM lookup. Resolving a <see cref="KeySound"/> returns the buffer to play.
/// </summary>
public sealed class KeyVoiceSet
{
    private readonly Dictionary<int, SampleBuffer> _pitched;

    public SampleBuffer Space { get; }
    public SampleBuffer Enter { get; }
    public SampleBuffer Backspace { get; }
    public int SampleRate { get; }

    private KeyVoiceSet(int sampleRate, Dictionary<int, SampleBuffer> pitched,
        SampleBuffer space, SampleBuffer enter, SampleBuffer backspace)
    {
        SampleRate = sampleRate;
        _pitched = pitched;
        Space = space;
        Enter = enter;
        Backspace = backspace;
    }

    public int PitchedCount => _pitched.Count;

    /// <summary>
    /// Look up the buffer for a resolved key sound. Pitched keys are keyed by MIDI note
    /// (so identical notes share a buffer); rhythm keys return their dedicated sound.
    /// Returns null for silent keys.
    /// </summary>
    public SampleBuffer? Resolve(KeySound sound) => sound.Category switch
    {
        KeyCategory.Pitched => _pitched.TryGetValue(sound.MidiNote, out var b) ? b : null,
        KeyCategory.Space => Space,
        KeyCategory.Enter => Enter,
        KeyCategory.Backspace => Backspace,
        _ => null
    };

    /// <summary>
    /// General builder: every distinct pitched note in the map gets one rendered buffer
    /// via <paramref name="renderNote"/> (given the note's frequency in Hz). Keys sharing a
    /// note share a buffer. Presets supply the renderer (tonal, percussive, …) and the
    /// three rhythm sounds.
    /// </summary>
    public static KeyVoiceSet Bake(
        SpatialKeyMap map,
        int sampleRate,
        Func<double, SampleBuffer> renderNote,
        SampleBuffer space,
        SampleBuffer enter,
        SampleBuffer backspace)
    {
        var distinctNotes = new HashSet<int>();
        foreach (var vk in KeyboardLayout.DefaultOrder)
        {
            var s = map.Resolve(vk);
            if (s.Category == KeyCategory.Pitched) distinctNotes.Add(s.MidiNote);
        }

        var pitched = new Dictionary<int, SampleBuffer>(distinctNotes.Count);
        foreach (var midi in distinctNotes)
            pitched[midi] = renderNote(NoteUtil.MidiToFrequency(midi));

        return new KeyVoiceSet(sampleRate, pitched, space, enter, backspace);
    }

    /// <summary>
    /// Bake a melodic synth voice set (the "Keyboard" preset). Pitched keys are tones on
    /// the scale; the rhythm keys get musical sounds derived from the same root.
    /// </summary>
    public static KeyVoiceSet BakeSynth(
        SpatialKeyMap map,
        int sampleRate,
        Waveform wave,
        Envelope env,
        double holdSeconds = 0.18,
        float gain = 0.7f)
    {
        double rootFreq = NoteUtil.MidiToFrequency(map.RootMidi);
        var space = SynthVoiceFactory.CreateTone(rootFreq / 2.0, sampleRate, Waveform.Sine, Envelope.FeltTap, 0.05, gain * 0.7f);
        var enter = SynthVoiceFactory.CreateTone(rootFreq * 3.0, sampleRate, Waveform.FmBell, Envelope.Bell, 0.05, gain * 0.6f);
        var backspace = SynthVoiceFactory.CreateTone(rootFreq / 1.5, sampleRate, Waveform.Triangle, Envelope.FeltTap, 0.04, gain * 0.6f);

        return Bake(map, sampleRate,
            f => SynthVoiceFactory.CreateTone(f, sampleRate, wave, env, holdSeconds, gain),
            space, enter, backspace);
    }
}
