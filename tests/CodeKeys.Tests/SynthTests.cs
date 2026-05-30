using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Music;
using Xunit;

namespace CodeKeys.Tests;

public class SynthTests
{
    private const int Rate = 44100;

    [Fact]
    public void Tone_Has_Positive_Length()
    {
        var buf = SynthVoiceFactory.CreateTone(440, Rate, Waveform.Sine, Envelope.Pluck);
        Assert.True(buf.Length > 0);
    }

    [Fact]
    public void Tone_Stays_In_Range()
    {
        var buf = SynthVoiceFactory.CreateTone(440, Rate, Waveform.WarmPad, Envelope.Pluck, gain: 0.9f);
        Assert.True(buf.Peak() <= 1.0f);
    }

    [Fact]
    public void Tone_Starts_And_Ends_Click_Free()
    {
        var buf = SynthVoiceFactory.CreateTone(440, Rate, Waveform.Sine, Envelope.Pluck);
        // Soft attack starts near zero; faded tail ends at zero.
        Assert.True(Math.Abs(buf.Samples[0]) < 0.02f);
        Assert.True(Math.Abs(buf.Samples[^1]) < 0.001f);
    }

    [Fact]
    public void Tone_Rejects_NonPositive_Frequency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SynthVoiceFactory.CreateTone(0, Rate, Waveform.Sine, Envelope.Pluck));
    }

    [Fact]
    public void Envelope_Is_Zero_At_Start_And_After_Release()
    {
        var env = Envelope.Pluck;
        Assert.Equal(0.0, env.AmplitudeAt(0, 0.1), 6);
        double total = env.TotalSeconds(0.1);
        Assert.Equal(0.0, env.AmplitudeAt(total + 0.01, 0.1), 6);
    }

    [Fact]
    public void Envelope_Attack_Is_Gradual()
    {
        var env = Envelope.Pluck;
        // Halfway through the attack, amplitude should be roughly half — never a hard jump to 1.
        double mid = env.AmplitudeAt(env.Attack / 2, 0.1);
        Assert.InRange(mid, 0.3, 0.7);
    }

    [Fact]
    public void BakeSynth_Covers_All_Distinct_Notes()
    {
        var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"));
        var set = KeyVoiceSet.BakeSynth(map, Rate, Waveform.WarmPad, Envelope.Pluck);

        var distinct = new HashSet<int>();
        foreach (var vk in KeyboardLayout.DefaultOrder)
            distinct.Add(map.Resolve(vk).MidiNote);

        Assert.Equal(distinct.Count, set.PitchedCount);
    }

    [Fact]
    public void BakeSynth_Resolves_Rhythm_Keys_And_Silence()
    {
        var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"));
        var set = KeyVoiceSet.BakeSynth(map, Rate, Waveform.WarmPad, Envelope.Pluck);

        Assert.NotNull(set.Resolve(map.Resolve(VirtualKey.Space)));
        Assert.NotNull(set.Resolve(map.Resolve(VirtualKey.Enter)));
        Assert.NotNull(set.Resolve(map.Resolve(VirtualKey.Back)));
        Assert.Null(set.Resolve(KeySound.Silent));
    }
}
