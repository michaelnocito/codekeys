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
    public void MusicalTarget_Holds_Inside_The_Deadband()
    {
        // Small deviations (within the band) → no steering at all: the aim stays at the centre,
        // so the pulse only guides once the user has clearly drifted.
        Assert.Equal(Conductor.FlowCenter, Conductor.MusicalTarget(Conductor.FlowCenter + 0.10), 3);
        Assert.Equal(Conductor.FlowCenter, Conductor.MusicalTarget(Conductor.FlowCenter - 0.10), 3);
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
    public void Step_Early_Session_Holds_The_Base_Pulse()
    {
        // At t=0 responsiveness is 0, so even frantic typing doesn't move the tempo yet — the
        // adaptation fades in slowly over the first minutes (the "base beat → responding" transition).
        var next = Conductor.Step(Spec(bpm: 78), userArousal: 1.0, elapsedSeconds: 0, dtSeconds: 30, Lo, Hi);
        Assert.Equal(78, next.Bpm);
    }

    [Fact]
    public void Higher_Sensitivity_Reacts_Faster()
    {
        // Same inputs, more sensitivity → a bigger tempo move toward the target (less gradual).
        var calm   = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: 800, dtSeconds: 30, Lo, Hi, sensitivity: 0.5);
        var snappy = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: 800, dtSeconds: 30, Lo, Hi, sensitivity: 2.0);
        Assert.True(snappy.Bpm > calm.Bpm); // under-aroused → both lift; higher sensitivity lifts more
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

    private static void AssertNoHighTones(BeatLayer[] layers)
    {
        Assert.DoesNotContain(BeatLayer.Melody, layers);
        Assert.DoesNotContain(BeatLayer.Marimba, layers);
        Assert.DoesNotContain(BeatLayer.Chime, layers);
    }

    [Fact]
    public void Step_Start_Is_Just_The_Lone_Tapper_No_Other_Voices()
    {
        // At t=0 only Pulse — the "person 1 tapping". Bass/Ghost/Splash all wait their turn.
        var next = Conductor.Step(Spec(), 0.6, elapsedSeconds: 0, dtSeconds: 5, Lo, Hi);
        Assert.Contains(BeatLayer.Pulse, next.Layers);
        Assert.DoesNotContain(BeatLayer.Ghost, next.Layers);
        Assert.DoesNotContain(BeatLayer.Bass, next.Layers);
        Assert.DoesNotContain(BeatLayer.Splash, next.Layers);
        Assert.DoesNotContain(BeatLayer.Pad, next.Layers); // never the chord
        AssertNoHighTones(next.Layers);
        Assert.True(next.Density < 0.10); // very sparse to start
    }

    [Fact]
    public void Step_Voices_Enter_One_At_A_Time_Across_The_Build()
    {
        // The additive-minimalism contract: Ghost > Bass > Splash, each waiting for the next
        // envelope threshold. By the end of the build, everything we keep is present.
        var t0   = Conductor.Step(Spec(), 0.6, 0,    5, Lo, Hi);
        var t300 = Conductor.Step(Spec(), 0.6, 300,  5, Lo, Hi); // envelope 0.25
        var t450 = Conductor.Step(Spec(), 0.6, 450,  5, Lo, Hi); // envelope 0.5625
        var tEnd = Conductor.Step(Spec(), 0.6, Conductor.BuildupSeconds, 5, Lo, Hi);

        Assert.DoesNotContain(BeatLayer.Ghost, t0.Layers);
        Assert.Contains(BeatLayer.Ghost,  t300.Layers); // ~5 min: soft taps have joined
        Assert.DoesNotContain(BeatLayer.Bass, t300.Layers); // bass hasn't yet
        Assert.Contains(BeatLayer.Bass,   t450.Layers); // ~7.5 min: deep bass has joined
        Assert.Contains(BeatLayer.Splash, tEnd.Layers); // by full build, splashes too

        foreach (var spec in new[] { t0, t300, t450, tEnd })
        {
            Assert.Contains(BeatLayer.Pulse, spec.Layers); // tapper is there throughout
            Assert.DoesNotContain(BeatLayer.Pad, spec.Layers);
            AssertNoHighTones(spec.Layers);
        }
    }

    [Fact]
    public void Step_Density_Climbs_With_The_Build()
    {
        var t0   = Conductor.Step(Spec(), 0.5, 0,                          5, Lo, Hi);
        var t300 = Conductor.Step(Spec(), 0.5, 300,                        5, Lo, Hi);
        var tEnd = Conductor.Step(Spec(), 0.5, Conductor.BuildupSeconds,   5, Lo, Hi);
        Assert.True(t0.Density < t300.Density);
        Assert.True(t300.Density < tEnd.Density);
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

    // ---- additive build envelope ----

    [Fact]
    public void Build_Envelope_Spans_Zero_To_One_And_Starts_Almost_Imperceptible()
    {
        Assert.Equal(0.0, Conductor.BuildupEnvelope(0), 3);
        Assert.Equal(1.0, Conductor.BuildupEnvelope(Conductor.BuildupSeconds), 3);
        Assert.Equal(1.0, Conductor.BuildupEnvelope(Conductor.BuildupSeconds * 5), 3); // clamps
        Assert.True(Conductor.BuildupEnvelope(60) < 0.02);                              // first minute: ~1%
        Assert.True(Conductor.BuildupEnvelope(150) < Conductor.BuildupEnvelope(300));   // monotonic rise
    }

    [Fact]
    public void Build_Envelope_Is_Ease_In_Slow_At_Start()
    {
        // ease-in (p²): at the midpoint we should still be only ~25% through (vs 50% on smoothstep).
        // This is what makes the first half "almost not noticeable" — the slow organic add-on.
        double half = Conductor.BuildupEnvelope(Conductor.BuildupSeconds / 2);
        Assert.True(half < 0.30, $"half-way envelope {half} should still be sparse");
    }

    [Fact]
    public void Step_Arousal_Response_Is_Gated_By_The_Build()
    {
        // During the build (t small), even strong arousal should barely move the tempo. After the
        // build, the same arousal moves it as far as the rate-limit allows.
        var inBuild = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: 30, dtSeconds: 30, Lo, Hi);
        var built   = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: Conductor.BuildupSeconds, dtSeconds: 30, Lo, Hi);
        Assert.True(built.Bpm - Lo > inBuild.Bpm - Lo);
    }

    [Fact]
    public void BpmRange_Returns_The_Preset_Window()
    {
        Assert.Equal((60, 72), SignalsToBeat.BpmRange(BeatPreset.Focused));
    }
}
