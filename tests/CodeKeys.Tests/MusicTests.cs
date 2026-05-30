using CodeKeys.Core.Music;
using Xunit;

namespace CodeKeys.Tests;

public class MusicTests
{
    [Fact]
    public void A4_Is_440Hz()
    {
        Assert.Equal(440.0, NoteUtil.MidiToFrequency(69), 6);
    }

    [Fact]
    public void Octave_Up_Doubles_Frequency()
    {
        double a4 = NoteUtil.MidiToFrequency(69);
        double a5 = NoteUtil.MidiToFrequency(81);
        Assert.Equal(a4 * 2.0, a5, 6);
    }

    [Theory]
    [InlineData("C4", 60)]
    [InlineData("A4", 69)]
    [InlineData("C-1", 0)]
    [InlineData("F#3", 54)]
    [InlineData("Eb5", 75)]
    public void ParseNoteName_Works(string name, int expected)
    {
        Assert.Equal(expected, NoteUtil.ParseNoteName(name));
    }

    [Fact]
    public void ParseNoteName_Rejects_Garbage()
    {
        Assert.Throws<FormatException>(() => NoteUtil.ParseNoteName("H2"));
    }

    [Fact]
    public void Scale_Degree_Zero_Is_Root()
    {
        Assert.Equal(60, Scale.MajorPentatonic.DegreeToMidi(60, 0));
    }

    [Fact]
    public void Scale_Wraps_Into_Next_Octave()
    {
        // Major pentatonic has 5 degrees; degree 5 is the root one octave up.
        Assert.Equal(72, Scale.MajorPentatonic.DegreeToMidi(60, 5));
    }

    [Fact]
    public void Scale_Negative_Degree_Wraps_Down()
    {
        // Degree -1 is the top degree of the octave below the root.
        int midi = Scale.MajorPentatonic.DegreeToMidi(60, -1);
        Assert.Equal(60 - 12 + 9, midi); // last interval (9) an octave down
    }

    [Fact]
    public void DegreeSpan_Counts_Inclusive_Top_Root()
    {
        // 5 intervals * 2 octaves + 1 = 11 degrees across two octaves.
        Assert.Equal(11, Scale.MajorPentatonic.DegreeSpan(2));
    }
}
