using CodeKeys.Core.Beat;
using Xunit;

namespace CodeKeys.Tests;

public class ConductorTests
{
    // Focused preset tempo range, used throughout.
    private const int Lo = 72, Hi = 84;

    private static BeatSpec Spec(int bpm = 78, BeatLayer[]? layers = null) => new(
        BeatPreset.Focused, bpm, BeatScale.Dorian, "D3", LoopBars: 4,
        Density: 0.6, Swing: 0.2, GhostNotes: 0.1, Accents: new[] { 0, 16 },
        Layers: layers ?? new[] { BeatLayer.Pad, BeatLayer.Pulse, BeatLayer.Melody });

    private static Signals Typing(double avgGap, int chars = 40, int backspaces = 1, double variance = 80) =>
        new() { Text = "", DurationMs = 8000, CharCount = chars, Backspaces = backspaces, AvgGapMs = avgGap, GapVariance = variance, PunctCount = 2 };

    // ---- arousal estimation ----

    [Fact]
    public void Estimate_Idle_Reads_Low()
    {
        Assert.Equal(0.25, Conductor.Estimate(new Signals { Text = "" }), 3);
    }

    [Fact]
    public void Estimate_Is_Always_In_Range()
    {
        foreach (var gap in new[] { 60.0, 120, 250, 500, 900 })
            Assert.InRange(Conductor.Estimate(Typing(gap)), 0.0, 1.0);
    }

    [Fact]
    public void Estimate_Fast_Typing_Beats_Slow_Typing()
    {
        Assert.True(Conductor.Estimate(Typing(90)) > Conductor.Estimate(Typing(450)));
    }

    [Fact]
    public void Estimate_Backspaces_And_Erraticness_Raise_Arousal()
    {
        double calm = Conductor.Estimate(Typing(200, chars: 40, backspaces: 0, variance: 30));
        double struggling = Conductor.Estimate(Typing(200, chars: 40, backspaces: 12, variance: 300));
        Assert.True(struggling > calm);
    }

    // ---- counter-active target (the heart of the design) ----

    [Fact]
    public void MusicalTarget_Is_Counter_Active()
    {
        double overAroused = Conductor.MusicalTarget(0.95);
        double underAroused = Conductor.MusicalTarget(0.15);
        Assert.True(overAroused < Conductor.FlowCenter);   // too hot → aim lower (settle)
        Assert.True(underAroused > Conductor.FlowCenter);  // too cold → aim higher (activate)
        Assert.Equal(Conductor.FlowCenter, Conductor.MusicalTarget(Conductor.FlowCenter), 3);
    }

    [Fact]
    public void MusicalTarget_Decreases_As_User_Arousal_Rises()
    {
        double prev = double.MaxValue;
        for (double a = 0.0; a <= 1.0; a += 0.1)
        {
            double t = Conductor.MusicalTarget(a);
            Assert.True(t <= prev + 1e-9);
            prev = t;
        }
    }

    // ---- gentle, rate-limited stepping ----

    [Fact]
    public void Step_Is_Rate_Limited_Per_Second()
    {
        const double dt = 10;
        var next = Conductor.Step(Spec(bpm: 72), userArousal: 0.0, elapsedSeconds: 800, dtSeconds: dt, Lo, Hi);
        int maxBpm = (int)System.Math.Ceiling(Conductor.SlewPerSec * dt * (Hi - Lo)) + 1; // + rounding slack
        Assert.True(System.Math.Abs(next.Bpm - 72) <= maxBpm);
    }

    [Fact]
    public void Step_Over_Aroused_User_Eases_Tempo_Down()
    {
        var next = Conductor.Step(Spec(bpm: Hi), userArousal: 1.0, elapsedSeconds: 800, dtSeconds: 30, Lo, Hi);
        Assert.True(next.Bpm < Hi); // frantic typing → calm the beat
    }

    [Fact]
    public void Step_Under_Aroused_User_Lifts_Tempo_Up()
    {
        var next = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: 800, dtSeconds: 30, Lo, Hi);
        Assert.True(next.Bpm > Lo); // disengaged typing → energize the beat
    }

    [Fact]
    public void Step_With_Zero_Dt_Keeps_The_Tempo()
    {
        var next = Conductor.Step(Spec(bpm: 80), userArousal: 0.1, elapsedSeconds: 800, dtSeconds: 0, Lo, Hi);
        Assert.Equal(80, next.Bpm);
    }

    [Fact]
    public void Step_Preserves_Tonal_Identity()
    {
        var cur = Spec(bpm: 78);
        var next = Conductor.Step(cur, 0.7, 800, 20, Lo, Hi);
        Assert.Equal(cur.Preset, next.Preset);
        Assert.Equal(cur.Scale, next.Scale);
        Assert.Equal(cur.Root, next.Root);
        Assert.Equal(cur.LoopBars, next.LoopBars);
    }

    // ---- session arc ----

    [Fact]
    public void Step_In_Establish_Has_No_Melody_Or_Marimba()
    {
        var next = Conductor.Step(Spec(), 0.6, elapsedSeconds: 10, dtSeconds: 5, Lo, Hi);
        Assert.DoesNotContain(BeatLayer.Melody, next.Layers);
        Assert.DoesNotContain(BeatLayer.Marimba, next.Layers);
        Assert.Contains(BeatLayer.Pulse, next.Layers); // base voices stay
    }

    [Fact]
    public void Step_In_Statement_Adds_Melody_But_Not_Marimba()
    {
        var next = Conductor.Step(Spec(), 0.6, elapsedSeconds: 200, dtSeconds: 5, Lo, Hi);
        Assert.Contains(BeatLayer.Melody, next.Layers);
        Assert.DoesNotContain(BeatLayer.Marimba, next.Layers);
    }

    [Fact]
    public void Step_In_Development_Adds_Melody_And_Marimba()
    {
        var next = Conductor.Step(Spec(), 0.6, elapsedSeconds: 700, dtSeconds: 5, Lo, Hi);
        Assert.Contains(BeatLayer.Melody, next.Layers);
        Assert.Contains(BeatLayer.Marimba, next.Layers);
    }

    [Theory]
    [InlineData(0, Conductor.Phase.Establish)]
    [InlineData(119, Conductor.Phase.Establish)]
    [InlineData(120, Conductor.Phase.Statement)]
    [InlineData(359, Conductor.Phase.Statement)]
    [InlineData(360, Conductor.Phase.Development)]
    [InlineData(719, Conductor.Phase.Development)]
    [InlineData(720, Conductor.Phase.Flow)]
    [InlineData(5000, Conductor.Phase.Flow)]
    public void PhaseAt_Maps_Elapsed_Time(double seconds, Conductor.Phase expected)
    {
        Assert.Equal(expected, Conductor.PhaseAt(seconds));
    }

    [Fact]
    public void Step_Is_Deterministic()
    {
        var a = Conductor.Step(Spec(), 0.7, 500, 12, Lo, Hi);
        var b = Conductor.Step(Spec(), 0.7, 500, 12, Lo, Hi);
        Assert.Equal(a.Bpm, b.Bpm);
        Assert.Equal(a.Density, b.Density);
        Assert.Equal(a.Layers, b.Layers);
    }

    [Fact]
    public void BpmRange_Returns_The_Preset_Window()
    {
        Assert.Equal((72, 84), SignalsToBeat.BpmRange(BeatPreset.Focused));
    }
}
