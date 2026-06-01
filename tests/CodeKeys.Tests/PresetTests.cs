using CodeKeys.Core.Input;
using CodeKeys.Core.Presets;
using Xunit;

namespace CodeKeys.Tests;

public class PresetTests
{
    private const int Rate = 44100;

    [Fact]
    public void Dormant_Presets_Are_Not_Exposed()
    {
        // Older voicings are kept in PresetLibrary.cs for revival but not surfaced via All / ById,
        // so the picker stays focused on the six chosen packs.
        foreach (var id in new[] { "pulse", "thock", "keyboard", "after-dark", "electric", "midnight", "piano", "rhodes", "marimba", "neon-nights" })
            Assert.Null(PresetLibrary.ById(id));
    }

    [Theory]
    [InlineData("deep-warm")]
    [InlineData("soft-mallet")]
    [InlineData("warm-keys")]
    [InlineData("felt-piano")]
    [InlineData("water-drops")]
    [InlineData("boings")]
    public void Every_Pack_Sounds_All_Keys(string id)
    {
        var preset = PresetLibrary.ById(id);
        Assert.NotNull(preset);

        var baked = preset!.Build(Rate);
        foreach (var vk in KeyboardLayout.DefaultOrder)
            Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(vk)));
        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Space)));
        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Enter)));
        Assert.NotNull(baked.Voices.Resolve(baked.Map.Resolve(VirtualKey.Back)));
    }
}
