using System.Linq;
using CodeKeys.Core.Presets;
using Xunit;

namespace CodeKeys.Tests;

/// <summary>The selectable keystroke sound packs (the beat is unchanged across all of them).</summary>
public class PresetLibraryTests
{
    private const int Rate = 48000;

    [Fact]
    public void Exposes_Six_Packs()
    {
        Assert.Equal(6, PresetLibrary.All.Count);
    }

    [Fact]
    public void Default_Is_Deep_And_Warm()
    {
        Assert.Equal("deep-warm", PresetLibrary.Default.Id);
        Assert.Same(PresetLibrary.Default, PresetLibrary.All[0]); // default is also the first listed
    }

    [Fact]
    public void Includes_The_Silly_Pack_And_Four_Complementary_Plus_Default()
    {
        var ids = PresetLibrary.All.Select(p => p.Id).ToList();
        Assert.Equal(new[] { "deep-warm", "soft-mallet", "warm-keys", "felt-piano", "water-drops", "boings" }, ids);
    }

    [Fact]
    public void Pack_Ids_And_Names_Are_Unique()
    {
        Assert.Equal(PresetLibrary.All.Count, PresetLibrary.All.Select(p => p.Id).Distinct().Count());
        Assert.Equal(PresetLibrary.All.Count, PresetLibrary.All.Select(p => p.Name).Distinct().Count());
    }

    [Theory]
    [InlineData("deep-warm")]
    [InlineData("soft-mallet")]
    [InlineData("warm-keys")]
    [InlineData("felt-piano")]
    [InlineData("water-drops")]
    [InlineData("boings")]
    public void Every_Pack_Builds_Into_Audible_Voices(string id)
    {
        var preset = PresetLibrary.ById(id);
        Assert.NotNull(preset);

        var baked = preset!.Build(Rate);

        Assert.True(baked.Voices.PitchedCount > 0, "should bake at least one pitched key");
        Assert.True(baked.Voices.Space.Samples.Length > 0);
        Assert.True(baked.Voices.Enter.Samples.Length > 0);
        Assert.True(baked.Voices.Backspace.Samples.Length > 0);
    }

    [Fact]
    public void ById_Is_Case_Insensitive_And_Null_For_Unknown()
    {
        Assert.NotNull(PresetLibrary.ById("WATER-DROPS"));
        Assert.Null(PresetLibrary.ById("does-not-exist"));
    }
}
