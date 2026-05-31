using CodeKeys.Core.Beat;
using CodeKeys.Core.Input;
using Xunit;

namespace CodeKeys.Tests;

public class CaptureTests
{
    [Theory]
    [InlineData('A', KeyKind.Letter)]
    [InlineData('Z', KeyKind.Letter)]
    [InlineData('5', KeyKind.Digit)]
    [InlineData(VirtualKey.Space, KeyKind.Space)]
    [InlineData(VirtualKey.Enter, KeyKind.Enter)]
    [InlineData(VirtualKey.Back, KeyKind.Backspace)]
    [InlineData(0xBC, KeyKind.Punctuation)] // comma
    [InlineData(0xDB, KeyKind.Punctuation)] // [
    [InlineData(VirtualKey.ShiftL, KeyKind.Modifier)]
    [InlineData(0x70, KeyKind.Other)]       // F1
    public void Classify_Buckets_Keys(int vk, KeyKind expected)
    {
        Assert.Equal(expected, KeyClassifier.Classify(vk));
    }

    [Fact]
    public void Snapshot_Never_Stores_Text()
    {
        var c = new SignalsCollector();
        c.Record(0, KeyKind.Letter, isUpper: true);
        c.Record(100, KeyKind.Letter);
        Assert.Equal("", c.Snapshot().Text);
    }

    [Fact]
    public void Counts_Chars_Backspaces_And_Punctuation()
    {
        var c = new SignalsCollector();
        long t = 0;
        foreach (var k in new[] { KeyKind.Letter, KeyKind.Letter, KeyKind.Space, KeyKind.Punctuation, KeyKind.Backspace })
            c.Record(t += 100, k);

        var s = c.Snapshot();
        Assert.Equal(4, s.CharCount);   // 2 letters + space + punctuation (not backspace)
        Assert.Equal(1, s.Backspaces);
        Assert.Equal(1, s.PunctCount);
    }

    [Fact]
    public void Computes_Average_Gap()
    {
        var c = new SignalsCollector();
        c.Record(0, KeyKind.Letter);
        c.Record(200, KeyKind.Letter);
        c.Record(400, KeyKind.Letter);
        Assert.Equal(200, c.Snapshot().AvgGapMs, 3);
    }

    [Fact]
    public void Computes_Caps_Ratio_From_Letters_Only()
    {
        var c = new SignalsCollector();
        c.Record(0, KeyKind.Letter, isUpper: true);
        c.Record(100, KeyKind.Letter, isUpper: false);
        c.Record(200, KeyKind.Digit); // digits don't count toward caps ratio
        Assert.Equal(0.5, c.Snapshot().CapsRatio, 3);
    }

    [Fact]
    public void Modifiers_And_Other_Keys_Are_Ignored()
    {
        var c = new SignalsCollector();
        c.Record(0, KeyKind.Modifier);
        c.Record(100, KeyKind.Other);
        Assert.Equal(0, c.Snapshot().CharCount);
    }

    [Fact]
    public void Old_Events_Fall_Out_Of_The_Window()
    {
        var c = new SignalsCollector(windowMs: 1000);
        c.Record(0, KeyKind.Letter);
        c.Record(500, KeyKind.Letter);
        c.Record(2000, KeyKind.Letter); // evicts the t=0 (and t=500 is within 1000 of 2000? 2000-500=1500>1000 → also evicted)
        var s = c.Snapshot();
        Assert.Equal(1, s.CharCount); // only the t=2000 event remains
    }

    [Fact]
    public void Snapshot_Feeds_The_Brain()
    {
        var c = new SignalsCollector();
        long t = 0;
        for (int i = 0; i < 50; i++) c.Record(t += 120, KeyKind.Letter);
        var spec = SignalsToBeat.Of(c.Snapshot(), BeatPreset.Focused);
        Assert.InRange(spec.Bpm, 60, 72);
    }
}
