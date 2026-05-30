using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Presets;
using Xunit;

namespace CodeKeys.Tests;

public class PresetTests
{
    private const int Rate = 44100;

    [Fact]
    public void Library_Has_The_Three_Starter_Presets()
    {
        var ids = PresetLibrary.All.Select(p => p.Id).ToHashSet();
        Assert.Contains("keyboard", ids);
        Assert.Contains("pulse", ids);
        Assert.Contains("thock", ids);
    }

    [Fact]
    public void Default_Is_The_Low_Beat()
    {
        Assert.Equal("pulse", PresetLibrary.Default.Id);
    }

    [Fact]
    public void ById_Is_Case_Insensitive()
    {
        Assert.NotNull(PresetLibrary.ById("PULSE"));
        Assert.Null(PresetLibrary.ById("nope"));
    }

    [Theory]
    [InlineData("keyboard")]
    [InlineData("pulse")]
    [InlineData("thock")]
    public void Every_Preset_Sounds_All_Layout_And_Rhythm_Keys(string id)
    {
        var baked = PresetLibrary.ById(id)!.Build(Rate);

        foreach (var vk in KeyboardLayout.DefaultOrder)
            Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(vk)));

        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Space)));
        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Enter)));
        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Back)));
    }

    [Fact]
    public void Pulse_Uses_A_Narrower_Pitch_Range_Than_Keyboard()
    {
        // Low-cognitive-load presets minimize pitch variation (near steady-state).
        int keyboardNotes = PresetLibrary.ById("keyboard")!.Build(Rate).Voices.PitchedCount;
        int pulseNotes = PresetLibrary.ById("pulse")!.Build(Rate).Voices.PitchedCount;
        Assert.True(pulseNotes < keyboardNotes,
            $"expected Pulse ({pulseNotes}) to use fewer distinct notes than Keyboard ({keyboardNotes})");
    }
}
