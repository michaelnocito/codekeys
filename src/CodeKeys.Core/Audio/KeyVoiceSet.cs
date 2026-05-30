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
    /// Bake a synth voice set from a spatial map. Every distinct note in the map gets one
    /// rendered buffer; the rhythm keys get musical sounds derived from the same root.
    /// </summary>
    public static KeyVoiceSet BakeSynth(
        SpatialKeyMap map,
        int sampleRate,
        Waveform wave,
        Envelope env,
        double holdSeconds = 0.18,
        float gain = 0.7f)
    {
        // One buffer per distinct MIDI note used by the map (keys sharing a note share a buffer).
        var distinctNotes = new HashSet<int>();
        foreach (var vk in KeyboardLayout.DefaultOrder)
        {
            var s = map.Resolve(vk);
            if (s.Category == KeyCategory.Pitched) distinctNotes.Add(s.MidiNote);
        }

        var pitched = new Dictionary<int, SampleBuffer>(distinctNotes.Count);
        foreach (var midi in distinctNotes)
        {
            double freq = NoteUtil.MidiToFrequency(midi);
            pitched[midi] = SynthVoiceFactory.CreateTone(freq, sampleRate, wave, env, holdSeconds, gain);
        }

        // Rhythm sounds, in-key relative to the map root.
        double rootFreq = NoteUtil.MidiToFrequency(map.RootMidi);
        var space = SynthVoiceFactory.CreateTone(rootFreq / 2.0, sampleRate, Waveform.Sine, Envelope.FeltTap, 0.05, gain * 0.7f);
        var enter = SynthVoiceFactory.CreateTone(rootFreq * 3.0, sampleRate, Waveform.FmBell, Envelope.Bell, 0.05, gain * 0.6f);
        var backspace = SynthVoiceFactory.CreateTone(rootFreq / 1.5, sampleRate, Waveform.Triangle, Envelope.FeltTap, 0.04, gain * 0.6f);

        return new KeyVoiceSet(sampleRate, pitched, space, enter, backspace);
    }
}
