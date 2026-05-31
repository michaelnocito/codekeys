using CodeKeys.Core.Beat;
using CodeKeys.Core.Music;
using Xunit;

namespace CodeKeys.Tests;

public class BeatPatternTests
{
    private static BeatSpec FullSpec(double density = 0.8) => new(
        BeatPreset.Focused, 80, BeatScale.Dorian, "D3", LoopBars: 2,
        Density: density, Swing: 0.3, GhostNotes: 0.5,
        Accents: new[] { 0, 8, 16 },
        Layers: new[] { BeatLayer.Pad, BeatLayer.Pulse, BeatLayer.Marimba, BeatLayer.Arp, BeatLayer.Ghost });

    private static HashSet<int> ScalePitchClasses(BeatSpec spec)
    {
        var scale = SignalsToBeat.ToScale(spec.Scale);
        int root = NoteUtil.ParseNoteName(spec.Root);
        var pcs = new HashSet<int>();
        for (int d = 0; d < scale.DegreeSpan(2); d++)
            pcs.Add(((scale.DegreeToMidi(root, d) % 12) + 12) % 12);
        return pcs;
    }

    [Fact]
    public void Build_Is_Deterministic()
    {
        var a = BeatPattern.Build(FullSpec());
        var b = BeatPattern.Build(FullSpec());
        Assert.Equal(a, b);
    }

    [Fact]
    public void All_Hits_Are_Within_The_Loop()
    {
        var spec = FullSpec();
        int steps = spec.LoopBars * 16;
        Assert.All(BeatPattern.Build(spec), h => Assert.InRange(h.Step, 0, steps - 1));
    }

    [Fact]
    public void Every_Pitched_Hit_Stays_In_Scale()
    {
        var spec = FullSpec();
        var allowed = ScalePitchClasses(spec);
        Assert.All(BeatPattern.Build(spec), h => Assert.Contains(((h.Midi % 12) + 12) % 12, allowed));
    }

    [Fact]
    public void Pulse_Lands_On_The_Beat()
    {
        var hits = BeatPattern.Build(FullSpec());
        var pulses = hits.Where(h => h.Layer == BeatLayer.Pulse).ToList();
        Assert.NotEmpty(pulses);
        Assert.Contains(pulses, h => h.Step == 0);
        Assert.All(pulses, h => Assert.Equal(0, h.Step % 2)); // on/off-beat, never a 16th in between
    }

    [Fact]
    public void Pad_Plays_A_Triad_At_Each_Bar_Start()
    {
        var spec = FullSpec();
        var pads = BeatPattern.Build(spec).Where(h => h.Layer == BeatLayer.Pad).ToList();
        Assert.Equal(spec.LoopBars * 3, pads.Count);            // root/3rd/5th per bar
        Assert.All(pads, h => Assert.Equal(0, h.Step % 16));    // only at bar starts
    }

    [Fact]
    public void Layers_Are_Respected()
    {
        var spec = FullSpec() with { Layers = new[] { BeatLayer.Pulse } };
        Assert.All(BeatPattern.Build(spec), h => Assert.Equal(BeatLayer.Pulse, h.Layer));
    }

    [Fact]
    public void Swing_Only_Affects_Off_Steps()
    {
        Assert.All(BeatPattern.Build(FullSpec()),
            h => Assert.Equal(h.Step % 2 == 1 ? 0.3 : 0.0, h.SwingFraction));
    }

    [Fact]
    public void Accented_Steps_Are_Louder()
    {
        // Step 0 is an accent and a downbeat pulse → full gain.
        var hits = BeatPattern.Build(FullSpec());
        var downbeat = hits.First(h => h.Layer == BeatLayer.Pulse && h.Step == 0);
        Assert.Equal(1.0, downbeat.Gain);
    }
}
