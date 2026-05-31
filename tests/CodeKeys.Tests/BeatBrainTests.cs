using CodeKeys.Core.Beat;
using Xunit;

namespace CodeKeys.Tests;

public class BeatBrainTests
{
    private static Signals Sample(string text = "the quick brown fox", double avgGap = 200) => new()
    {
        Text = text,
        DurationMs = 4000,
        CharCount = text.Length,
        Backspaces = 1,
        AvgGapMs = avgGap,
        GapVariance = 120,
        CapsRatio = 0.1,
        PunctCount = 1,
    };

    [Fact]
    public void Same_Signals_Yield_Identical_Beat()
    {
        var a = SignalsToBeat.Of(Sample(), BeatPreset.Focused);
        var b = SignalsToBeat.Of(Sample(), BeatPreset.Focused);

        Assert.Equal(a.Bpm, b.Bpm);
        Assert.Equal(a.Scale, b.Scale);
        Assert.Equal(a.Root, b.Root);
        Assert.Equal(a.LoopBars, b.LoopBars);
        Assert.Equal(a.Density, b.Density);
        Assert.Equal(a.Swing, b.Swing);
        Assert.Equal(a.GhostNotes, b.GhostNotes);
        Assert.Equal(a.Accents, b.Accents);
        Assert.Equal(a.Layers, b.Layers);
    }

    [Theory]
    [InlineData(BeatPreset.Focused, 60, 72, BeatScale.Dorian, "D3")]
    [InlineData(BeatPreset.Relaxed, 60, 70, BeatScale.MajorPentatonic, "C3")]
    [InlineData(BeatPreset.Burnout, 75, 88, BeatScale.Major, "F3")]
    [InlineData(BeatPreset.Silly, 100, 130, BeatScale.MajorPentatonic, "C4")]
    public void Preset_Sets_Scale_Root_And_Bpm_In_Range(BeatPreset preset, int lo, int hi, BeatScale scale, string root)
    {
        var spec = SignalsToBeat.Of(Sample(), preset);
        Assert.Equal(scale, spec.Scale);
        Assert.Equal(root, spec.Root);
        Assert.InRange(spec.Bpm, lo, hi);
    }

    [Fact]
    public void Faster_Typing_Means_Higher_Tempo_And_Density()
    {
        var fast = SignalsToBeat.Of(Sample(avgGap: 80), BeatPreset.Focused);
        var slow = SignalsToBeat.Of(Sample(avgGap: 500), BeatPreset.Focused);
        Assert.True(fast.Bpm >= slow.Bpm);
        Assert.True(fast.Density > slow.Density);
        Assert.Equal(72, fast.Bpm); // speed=1 -> top of range
        Assert.Equal(60, slow.Bpm); // speed=0 -> bottom
    }

    [Theory]
    [InlineData(20, 2)]
    [InlineData(60, 4)]
    [InlineData(150, 8)]
    public void Length_Drives_Loop_Bars(int charCount, int expectedBars)
    {
        var sig = Sample() with { CharCount = charCount };
        Assert.Equal(expectedBars, SignalsToBeat.Of(sig, BeatPreset.Focused).LoopBars);
    }

    [Fact]
    public void Backspaces_Drive_Ghost_Notes_And_Layer()
    {
        var clean = Sample() with { Backspaces = 0, CharCount = 20 };
        var messy = Sample() with { Backspaces = 10, CharCount = 20 };

        Assert.Equal(0, SignalsToBeat.Of(clean, BeatPreset.Relaxed).GhostNotes);
        var messySpec = SignalsToBeat.Of(messy, BeatPreset.Relaxed);
        Assert.True(messySpec.GhostNotes > 0.15);
        Assert.Contains(BeatLayer.Ghost, messySpec.Layers);
    }

    [Fact]
    public void Long_Text_Adds_Melody_Layer()
    {
        var spec = SignalsToBeat.Of(Sample() with { CharCount = 120 }, BeatPreset.Relaxed);
        Assert.Contains(BeatLayer.Melody, spec.Layers); // loopBars >= 4
    }

    [Fact]
    public void Accents_Are_Sorted_Distinct_And_In_Range()
    {
        var sig = Sample() with { CharCount = 30, CapsRatio = 1.0, PunctCount = 6 };
        var spec = SignalsToBeat.Of(sig, BeatPreset.Focused);
        int steps = spec.LoopBars * 16;

        Assert.All(spec.Accents, a => Assert.InRange(a, 0, steps - 1));
        Assert.Equal(spec.Accents.Distinct().Count(), spec.Accents.Length);
        var sorted = spec.Accents.OrderBy(x => x).ToArray();
        Assert.Equal(sorted, spec.Accents);
    }

    [Fact]
    public void Swing_Stays_In_Range_For_Raw_Or_Normalized_Variance()
    {
        var raw = SignalsToBeat.Of(Sample() with { GapVariance = 250 }, BeatPreset.Focused);   // raw ms
        var norm = SignalsToBeat.Of(Sample() with { GapVariance = 0.8 }, BeatPreset.Focused);  // pre-normalized
        Assert.InRange(raw.Swing, 0, 0.6);
        Assert.InRange(norm.Swing, 0, 0.6);
    }

    [Fact]
    public void Evolve_Keeps_Tonal_Identity_And_Only_Drifts_Density_Accents()
    {
        var spec = SignalsToBeat.Of(Sample() with { CharCount = 30 }, BeatPreset.Focused);
        var next = SignalsToBeat.Evolve(spec, cycle: 1);

        Assert.Equal(spec.Preset, next.Preset);
        Assert.Equal(spec.Scale, next.Scale);
        Assert.Equal(spec.Root, next.Root);
        Assert.Equal(spec.Bpm, next.Bpm);
        Assert.Equal(spec.LoopBars, next.LoopBars);
        Assert.Equal(spec.Accents.Length, next.Accents.Length);

        int steps = next.LoopBars * 16;
        Assert.All(next.Accents, a => Assert.InRange(a, 0, steps - 1));
        Assert.InRange(next.Density, 0.15, 1.0);
    }

    [Fact]
    public void Evolve_Is_Deterministic_Per_Cycle()
    {
        var spec = SignalsToBeat.Of(Sample(), BeatPreset.Focused);
        var x = SignalsToBeat.Evolve(spec, 3);
        var y = SignalsToBeat.Evolve(spec, 3);
        Assert.Equal(x.Density, y.Density);
        Assert.Equal(x.Accents, y.Accents);
    }

    [Fact]
    public void Empty_Text_Falls_Back_To_Preset_Seed_And_Is_Stable()
    {
        var sig = new Signals { Text = "", CharCount = 0, AvgGapMs = 200 };
        var a = SignalsToBeat.Of(sig, BeatPreset.Silly);
        var b = SignalsToBeat.Of(sig, BeatPreset.Silly);
        Assert.Equal(a.Accents, b.Accents);
        Assert.Equal(a.Bpm, b.Bpm);
    }

    [Fact]
    public void Tonal_Bridge_Maps_To_Music_Types()
    {
        var spec = SignalsToBeat.Of(Sample(), BeatPreset.Focused);
        Assert.Equal("dorian", SignalsToBeat.ToScale(spec.Scale).Name);
        Assert.Equal(50, SignalsToBeat.RootMidi(spec)); // D3 = MIDI 50
    }
}
