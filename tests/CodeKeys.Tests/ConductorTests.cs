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
        // Same inputs at the peak of the cycle (build=1), more sensitivity → bigger tempo move.
        double t = Conductor.BuildupSeconds;
        var calm   = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: t, dtSeconds: 30, Lo, Hi, sensitivity: 0.5);
        var snappy = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: t, dtSeconds: 30, Lo, Hi, sensitivity: 2.0);
        Assert.True(snappy.Bpm > calm.Bpm);
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
    public void Step_Start_Has_Bass_Pulse_And_Bowl_No_Other_Voices()
    {
        // From t=0 in Tibetan Beat (Focused) mode: Bass hum + Pulse + Bowl. Ghost/Splash wait.
        var next = Conductor.Step(Spec(), 0.6, elapsedSeconds: 0, dtSeconds: 5, Lo, Hi);
        Assert.Contains(BeatLayer.Pulse, next.Layers);
        Assert.Contains(BeatLayer.Bass, next.Layers);
        Assert.Contains(BeatLayer.Bowl, next.Layers); // Tibetan Beat has bowls from the start
        Assert.DoesNotContain(BeatLayer.Ghost, next.Layers);
        Assert.DoesNotContain(BeatLayer.Splash, next.Layers);
        Assert.DoesNotContain(BeatLayer.Pad, next.Layers); // never the chord
        AssertNoHighTones(next.Layers);
        Assert.True(next.Density < 0.10); // very sparse to start
    }

    [Fact]
    public void Step_Voices_Enter_One_At_A_Time_Across_The_Build()
    {
        // Bass + Pulse + Bowl from the start (Tibetan Beat); Ghost taps at >0.30 envelope;
        // Splashes at >0.70.
        var t0   = Conductor.Step(Spec(), 0.6, 0,    5, Lo, Hi);
        var t450 = Conductor.Step(Spec(), 0.6, 450,  5, Lo, Hi); // envelope 0.5625
        var tEnd = Conductor.Step(Spec(), 0.6, Conductor.BuildupSeconds, 5, Lo, Hi);

        Assert.DoesNotContain(BeatLayer.Ghost,  t0.Layers);
        Assert.Contains(BeatLayer.Ghost,        t450.Layers); // ~7.5 min: soft taps joined
        Assert.DoesNotContain(BeatLayer.Splash, t450.Layers);
        Assert.Contains(BeatLayer.Splash,       tEnd.Layers);

        foreach (var spec in new[] { t0, t450, tEnd })
        {
            Assert.Contains(BeatLayer.Pulse, spec.Layers); // accent throughout
            Assert.Contains(BeatLayer.Bass,  spec.Layers); // hum throughout (the foundation)
            Assert.Contains(BeatLayer.Bowl,  spec.Layers); // bowls throughout (Tibetan Beat / chakras)
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

    // ---- breathing cycle (rise → peak → fall → repeat) ----

    [Fact]
    public void Cycle_Envelope_Reaches_Peak_Then_Returns_To_Silence()
    {
        Assert.Equal(0.0, Conductor.CycleEnvelope(0), 3);
        Assert.Equal(1.0, Conductor.CycleEnvelope(Conductor.BuildupSeconds), 3);     // peak at end of rise
        Assert.Equal(0.0, Conductor.CycleEnvelope(Conductor.CycleSeconds), 3);       // silence at end of fall
    }

    [Fact]
    public void Cycle_Envelope_Fall_Is_Faster_Than_Rise()
    {
        // The fall traverses the same 0..1 range as the rise but in less time (FallSpeedFactor = 1.25).
        Assert.True(Conductor.FallSeconds < Conductor.BuildupSeconds);
        double ratio = Conductor.BuildupSeconds / Conductor.FallSeconds;
        Assert.Equal(Conductor.FallSpeedFactor, ratio, 6);
    }

    [Fact]
    public void Cycle_Envelope_Loops_After_One_Full_Cycle()
    {
        // After CycleSeconds the pattern repeats — t = 0 and t = CycleSeconds * k should match.
        foreach (var t in new[] { 30.0, 300.0, Conductor.BuildupSeconds + 100, Conductor.CycleSeconds - 50 })
        {
            Assert.Equal(Conductor.CycleEnvelope(t),
                         Conductor.CycleEnvelope(t + Conductor.CycleSeconds), 6);
            Assert.Equal(Conductor.CycleEnvelope(t),
                         Conductor.CycleEnvelope(t + 2 * Conductor.CycleSeconds), 6);
        }
    }

    [Fact]
    public void Cycle_Envelope_Falls_After_The_Peak()
    {
        // Just past the peak we should be below the peak; further into the fall we should be lower.
        double justAfterPeak = Conductor.CycleEnvelope(Conductor.BuildupSeconds + 60);
        double laterInFall   = Conductor.CycleEnvelope(Conductor.BuildupSeconds + 240);
        Assert.True(justAfterPeak < 1.0);
        Assert.True(laterInFall < justAfterPeak);
    }

    [Fact]
    public void Step_Arousal_Response_Is_Gated_By_The_Build()
    {
        // During the build (t small), even strong arousal should barely move the tempo. At the
        // peak (end of rise), the same arousal moves it as far as the rate-limit allows.
        var inBuild = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: 30, dtSeconds: 30, Lo, Hi);
        var built   = Conductor.Step(Spec(bpm: Lo), userArousal: 0.0, elapsedSeconds: Conductor.BuildupSeconds, dtSeconds: 30, Lo, Hi);
        Assert.True(built.Bpm - Lo > inBuild.Bpm - Lo);
    }

    [Fact]
    public void Step_End_Of_Cycle_Returns_To_Foundation_Hum_Pulse_And_Bowl()
    {
        // The end of the fall = back to the Tibetan Beat foundation: Bass + Pulse + Bowl,
        // with the additive Ghost/Splash gone. Density drops to the floor so the schedule is sparse.
        var quiet = Conductor.Step(Spec(), 0.5, elapsedSeconds: Conductor.CycleSeconds, dtSeconds: 5, Lo, Hi);
        Assert.Contains(BeatLayer.Pulse, quiet.Layers);
        Assert.Contains(BeatLayer.Bass,  quiet.Layers); // hum persists across the cycle endpoints
        Assert.Contains(BeatLayer.Bowl,  quiet.Layers); // bowl is part of the Tibetan Beat identity
        Assert.DoesNotContain(BeatLayer.Splash, quiet.Layers);
        Assert.DoesNotContain(BeatLayer.Ghost,  quiet.Layers);
        Assert.True(quiet.Density < 0.10);
    }

    [Fact]
    public void BpmRange_Returns_The_Preset_Window()
    {
        Assert.Equal((60, 72), SignalsToBeat.BpmRange(BeatPreset.Focused));
    }

    // ---- chakra presets ----

    [Theory]
    [InlineData(BeatPreset.Root,        396.0)]
    [InlineData(BeatPreset.Sacral,      417.0)]
    [InlineData(BeatPreset.SolarPlexus, 528.0)]
    [InlineData(BeatPreset.Heart,       639.0)]
    [InlineData(BeatPreset.Throat,      741.0)]
    [InlineData(BeatPreset.ThirdEye,    852.0)]
    [InlineData(BeatPreset.Crown,       963.0)]
    public void Chakra_Preset_Tunes_Bowl_To_The_Solfeggio_Frequency(BeatPreset preset, double expectedHz)
    {
        Assert.Equal(expectedHz, SignalsToBeat.ChakraBowlFreq(preset));
    }

    [Fact]
    public void Non_Chakra_Presets_Have_No_Bowl_Frequency_Override()
    {
        foreach (var p in new[] { BeatPreset.Focused, BeatPreset.Relaxed, BeatPreset.Burnout, BeatPreset.Silly })
            Assert.Null(SignalsToBeat.ChakraBowlFreq(p));
    }

    [Theory]
    [InlineData(BeatPreset.Root)]
    [InlineData(BeatPreset.Heart)]
    [InlineData(BeatPreset.Crown)]
    public void Chakra_Bowl_Rings_From_The_Very_Start(BeatPreset preset)
    {
        // The bowl IS the chakra's identity, so it must be in the layer set from t=0 (unlike the
        // non-chakra build where the bowl only joins after build > 0.5).
        var spec = Conductor.Step(Spec(layers: new[] { BeatLayer.Pulse }) with { Preset = preset },
                                  userArousal: 0.5, elapsedSeconds: 0, dtSeconds: 5, Lo, Hi);
        Assert.Contains(BeatLayer.Bowl, spec.Layers);
    }
}
