using CodeKeys.Core.Audio;
using Xunit;

namespace CodeKeys.Tests;

public class InstrumentTests
{
    private const int Rate = 44100;

    private static void AssertSaneVoice(SampleBuffer b)
    {
        Assert.True(b.Length > 0);
        Assert.True(b.Peak() <= 1.0f);
        Assert.True(Math.Abs(b.Samples[^1]) < 0.02f); // click-free tail
    }

    [Fact] public void Piano_Is_Sane() => AssertSaneVoice(InstrumentFactory.CreatePiano(261.6, Rate));
    [Fact] public void Rhodes_Is_Sane() => AssertSaneVoice(InstrumentFactory.CreateRhodes(261.6, Rate));
    [Fact] public void Marimba_Is_Sane() => AssertSaneVoice(InstrumentFactory.CreateMarimba(261.6, Rate));

    [Fact]
    public void SuperSaw_Is_Sane()
    {
        var b = InstrumentFactory.CreateSuperSaw(440, Rate, Envelope.Pluck);
        Assert.True(b.Length > 0);
        Assert.True(b.Peak() <= 1.0f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Instruments_Reject_NonPositive_Frequency(double freq)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InstrumentFactory.CreatePiano(freq, Rate));
        Assert.Throws<ArgumentOutOfRangeException>(() => InstrumentFactory.CreateRhodes(freq, Rate));
        Assert.Throws<ArgumentOutOfRangeException>(() => InstrumentFactory.CreateMarimba(freq, Rate));
        Assert.Throws<ArgumentOutOfRangeException>(() => InstrumentFactory.CreateSuperSaw(freq, Rate, Envelope.Pluck));
    }

    [Fact]
    public void PluckedString_Is_Deterministic_And_Sane()
    {
        var a = StringFactory.CreatePluckedString(110, Rate, seed: 4);
        var b = StringFactory.CreatePluckedString(110, Rate, seed: 4);
        Assert.Equal(a.Samples, b.Samples);
        AssertSaneVoice(a);
    }

    [Fact]
    public void PluckedString_Rejects_NonPositive_Frequency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StringFactory.CreatePluckedString(0, Rate));
    }

    [Fact]
    public void Snare_Is_Deterministic_And_In_Range()
    {
        var a = PercussionFactory.CreateSnare(Rate, seed: 7);
        var b = PercussionFactory.CreateSnare(Rate, seed: 7);
        Assert.Equal(a.Samples, b.Samples);
        Assert.True(a.Peak() <= 1.0f);
        Assert.True(Math.Abs(a.Samples[^1]) < 0.02f);
    }

    [Fact]
    public void Sub_Is_Sane()
    {
        var b = PercussionFactory.CreateSub(55, Rate);
        AssertSaneVoice(b);
    }
}
