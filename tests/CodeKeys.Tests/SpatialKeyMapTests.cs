using CodeKeys.Core.Input;
using CodeKeys.Core.Music;
using Xunit;

namespace CodeKeys.Tests;

public class SpatialKeyMapTests
{
    private static SpatialKeyMap Map() =>
        new(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"), octaves: 2);

    [Fact]
    public void Every_Layout_Key_Is_Pitched()
    {
        var map = Map();
        Assert.Equal(KeyboardLayout.DefaultOrder.Count, map.PitchedKeyCount);
    }

    [Fact]
    public void Resolution_Is_Deterministic()
    {
        var map = Map();
        int vk = 'K';
        Assert.Equal(map.Resolve(vk).MidiNote, map.Resolve(vk).MidiNote);
    }

    [Fact]
    public void Layout_Rises_Low_To_High()
    {
        var map = Map();
        int firstVk = KeyboardLayout.DefaultOrder[0];                                   // bottom-left
        int lastVk = KeyboardLayout.DefaultOrder[^1];                                    // top-right
        Assert.True(map.Resolve(firstVk).MidiNote < map.Resolve(lastVk).MidiNote);
    }

    [Fact]
    public void Lowest_Key_Is_Root_And_Highest_Is_Top_Octave()
    {
        var map = Map();
        int root = NoteUtil.ParseNoteName("C3");
        Assert.Equal(root, map.Resolve(KeyboardLayout.DefaultOrder[0]).MidiNote);
        Assert.Equal(root + 24, map.Resolve(KeyboardLayout.DefaultOrder[^1]).MidiNote); // 2 octaves up
    }

    [Fact]
    public void Space_Enter_Backspace_Have_Own_Categories()
    {
        var map = Map();
        Assert.Equal(KeyCategory.Space, map.Resolve(VirtualKey.Space).Category);
        Assert.Equal(KeyCategory.Enter, map.Resolve(VirtualKey.Enter).Category);
        Assert.Equal(KeyCategory.Backspace, map.Resolve(VirtualKey.Back).Category);
    }

    [Theory]
    [InlineData(VirtualKey.ShiftL)]
    [InlineData(VirtualKey.ControlR)]
    [InlineData(VirtualKey.MenuL)]
    [InlineData(VirtualKey.LWin)]
    [InlineData(VirtualKey.CapsLock)]
    public void Pure_Modifiers_Are_Silent(int vk)
    {
        Assert.Equal(KeyCategory.Silent, Map().Resolve(vk).Category);
    }

    [Fact]
    public void Unmapped_Key_Is_Silent()
    {
        // F12 (0x7B) isn't in the layout and isn't a modifier → silent.
        Assert.Equal(KeyCategory.Silent, Map().Resolve(0x7B).Category);
    }

    [Fact]
    public void Pitched_Keys_Stay_In_Scale()
    {
        var map = Map();
        var allowed = new HashSet<int>();
        foreach (var deg in Enumerable.Range(0, map.Scale.DegreeSpan(map.Octaves)))
            allowed.Add(map.Scale.DegreeToMidi(map.RootMidi, deg) % 12);

        foreach (var vk in KeyboardLayout.DefaultOrder)
        {
            int pc = map.Resolve(vk).MidiNote % 12;
            Assert.Contains(pc, allowed);
        }
    }
}
