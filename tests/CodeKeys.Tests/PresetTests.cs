using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Presets;
using Xunit;

namespace CodeKeys.Tests;

public class PresetTests
{
    private const int Rate = 44100;

    [Fact]
    public void Library_Is_Locked_To_Midnight()
    {
        // Bowl Bass Keys ships with a single keystroke voicing — Midnight. The other presets
        // (Pulse/Thock/Keyboard/AfterDark/Electric/GrandPiano/Rhodes/Marimba) are kept dormant
        // in PresetLibrary.cs but not exposed via All / ById, so the app stays focused.
        var ids = PresetLibrary.All.Select(p => p.Id).ToList();
        Assert.Single(ids);
        Assert.Equal("midnight", ids[0]);
    }

    [Fact]
    public void Default_Is_Midnight()
    {
        Assert.Equal("midnight", PresetLibrary.Default.Id);
    }

    [Fact]
    public void ById_Is_Case_Insensitive_For_Midnight()
    {
        Assert.NotNull(PresetLibrary.ById("MIDNIGHT"));
        Assert.NotNull(PresetLibrary.ById("midnight"));
        Assert.Null(PresetLibrary.ById("nope"));
    }

    [Fact]
    public void Dormant_Presets_Are_Not_Exposed()
    {
        foreach (var id in new[] { "pulse", "thock", "keyboard", "after-dark", "electric", "piano", "rhodes", "marimba", "neon-nights" })
            Assert.Null(PresetLibrary.ById(id));
    }

    [Fact]
    public void Midnight_Builds_And_Sounds_All_Keys()
    {
        var baked = PresetLibrary.Default.Build(Rate);
        foreach (var vk in KeyboardLayout.DefaultOrder)
            Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(vk)));
        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Space)));
        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Enter)));
        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Back)));
    }
}
