using CodeKeys.Core.Audio;
using Xunit;

namespace CodeKeys.Tests;

public class PercussionTests
{
    private const int Rate = 44100;

    [Fact]
    public void Kick_Has_Length_And_Stays_In_Range()
    {
        var k = PercussionFactory.CreateKick(110, Rate);
        Assert.True(k.Length > 0);
        Assert.True(k.Peak() <= 1.0f);
    }

    [Fact]
    public void Kick_Is_Click_Free_At_Both_Ends()
    {
        var k = PercussionFactory.CreateKick(110, Rate);
        Assert.True(Math.Abs(k.Samples[0]) < 0.02f);   // 2ms soft attack
        Assert.True(Math.Abs(k.Samples[^1]) < 0.01f);  // faded tail
    }

    [Fact]
    public void Kick_Is_Short()
    {
        // Low-cognitive-load: hits are brief (well under half a second).
        Assert.True(PercussionFactory.CreateKick(110, Rate).DurationSeconds < 0.5);
    }

    [Fact]
    public void Kick_Rejects_NonPositive_Frequency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PercussionFactory.CreateKick(0, Rate));
    }

    [Fact]
    public void Tap_Is_Deterministic_For_A_Seed()
    {
        var a = PercussionFactory.CreateTap(150, Rate, seed: 9);
        var b = PercussionFactory.CreateTap(150, Rate, seed: 9);
        Assert.Equal(a.Samples, b.Samples);
    }

    [Fact]
    public void Tap_Stays_In_Range_And_Ends_Quiet()
    {
        var t = PercussionFactory.CreateTap(150, Rate);
        Assert.True(t.Peak() <= 1.0f);
        Assert.True(Math.Abs(t.Samples[^1]) < 0.01f);
    }
}
