using CodeKeys.Core.Audio;
using Xunit;

namespace CodeKeys.Tests;

public class AmbientBedTests
{
    private const int Rate = 44100;

    [Fact]
    public void BrownNoise_Is_Deterministic_For_A_Seed()
    {
        var a = AmbientBedFactory.BrownNoise(Rate, 1.0, 0.2, seed: 42);
        var b = AmbientBedFactory.BrownNoise(Rate, 1.0, 0.2, seed: 42);
        Assert.Equal(a.Samples, b.Samples);
    }

    [Fact]
    public void Beds_Are_Normalized_Quiet()
    {
        // Focus research favors quiet beds; generators normalize well below full scale.
        Assert.True(AmbientBedFactory.BrownNoise(Rate, 1.0, 0.2).Peak() <= 0.62f);
        Assert.True(AmbientBedFactory.Rain(Rate, 1.0, 0.2).Peak() <= 0.57f);
        Assert.True(AmbientBedFactory.Pad(110, Rate, 1.0, 0.2).Peak() <= 0.52f);
    }

    [Fact]
    public void MakeSeamless_Shortens_By_Crossfade()
    {
        var src = new float[Rate]; // 1 second
        var outBuf = AmbientBedFactory.MakeSeamless(src, Rate, 0.25);
        Assert.Equal(Rate - (int)(0.25 * Rate), outBuf.Length);
    }

    [Fact]
    public void MakeSeamless_Is_NoOp_When_Crossfade_Too_Long()
    {
        var src = new float[100];
        var outBuf = AmbientBedFactory.MakeSeamless(src, Rate, 10.0);
        Assert.Equal(src.Length, outBuf.Length);
    }

    [Fact]
    public void Loop_Join_Has_No_Large_Jump()
    {
        // After crossfading, the wrap point (last sample → first sample) should not
        // contain a big discontinuity that would click on every loop.
        var bed = AmbientBedFactory.BrownNoise(Rate, 2.0, 0.5, seed: 3);
        float jump = Math.Abs(bed.Samples[^1] - bed.Samples[0]);
        Assert.True(jump < 0.1f, $"loop seam jump too large: {jump}");
    }
}
