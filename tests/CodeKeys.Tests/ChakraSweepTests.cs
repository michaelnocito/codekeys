using System.Linq;
using CodeKeys.Core.Beat;
using Xunit;

namespace CodeKeys.Tests;

/// <summary>
/// The Chakra Sweep: a guided 21-minute journey where the singing bowl walks UP the seven chakras,
/// 3 minutes each (Root→Crown), over a steady bass+bowl bed. Only the bowl colour changes over time.
/// </summary>
public class ChakraSweepTests
{
    private const int Lo = 60, Hi = 72; // ChakraSweep preset range

    // ---- stage timing ----

    [Fact]
    public void Sweep_Has_Seven_Stages_In_Ascending_Order()
    {
        Assert.Equal(new[]
        {
            BeatPreset.Root, BeatPreset.Sacral, BeatPreset.SolarPlexus,
            BeatPreset.Heart, BeatPreset.Throat, BeatPreset.ThirdEye, BeatPreset.Crown,
        }, SignalsToBeat.ChakraSweepStages);
    }

    [Fact]
    public void Sweep_Is_Three_Minutes_Per_Chakra_And_Twenty_One_Total()
    {
        Assert.Equal(180.0, SignalsToBeat.ChakraSweepStageSeconds, 3);
        Assert.Equal(21 * 60.0, SignalsToBeat.ChakraSweepTotalSeconds, 3); // 1260 s = 21 min
    }

    [Theory]
    [InlineData(0,    BeatPreset.Root)]
    [InlineData(179,  BeatPreset.Root)]
    [InlineData(180,  BeatPreset.Sacral)]
    [InlineData(360,  BeatPreset.SolarPlexus)]
    [InlineData(540,  BeatPreset.Heart)]
    [InlineData(720,  BeatPreset.Throat)]
    [InlineData(900,  BeatPreset.ThirdEye)]
    [InlineData(1080, BeatPreset.Crown)]
    [InlineData(1259, BeatPreset.Crown)]
    public void Sweep_Bowl_Walks_Up_The_Chakras_By_Elapsed_Time(double seconds, BeatPreset expected)
    {
        Assert.Equal(expected, SignalsToBeat.ChakraSweepStageAt(seconds));
    }

    [Fact]
    public void Sweep_Holds_On_Crown_After_The_Journey_Ends()
    {
        Assert.Equal(BeatPreset.Crown, SignalsToBeat.ChakraSweepStageAt(SignalsToBeat.ChakraSweepTotalSeconds));
        Assert.Equal(BeatPreset.Crown, SignalsToBeat.ChakraSweepStageAt(99999));
    }

    [Fact]
    public void Sweep_Negative_Time_Clamps_To_Root()
    {
        Assert.Equal(BeatPreset.Root, SignalsToBeat.ChakraSweepStageAt(-50));
    }

    // ---- tonal identity ----

    [Fact]
    public void Sweep_Opens_On_The_Root_Bowl_And_Is_On_The_Musical_Bowl_Path()
    {
        // A value (not null) keeps the sweep on the shared "musical bass + bowl from t=0" code path;
        // its nominal/opening bowl is the Root (396 Hz).
        Assert.Equal(396.0, SignalsToBeat.ChakraBowlFreq(BeatPreset.ChakraSweep));
    }

    [Fact]
    public void Sweep_Has_A_Gentle_Calm_Tempo_Window()
    {
        Assert.Equal((Lo, Hi), SignalsToBeat.BpmRange(BeatPreset.ChakraSweep));
    }

    // ---- steady (non-breathing) envelope ----

    [Fact]
    public void Sweep_Envelope_Eases_In_Then_Holds_A_Steady_Plateau()
    {
        Assert.Equal(0.0, Conductor.SweepEnvelope(0), 3);                                   // starts from silence
        Assert.True(Conductor.SweepEnvelope(20) < Conductor.SweepEnvelope(40));             // rising
        Assert.Equal(Conductor.SweepPlateau, Conductor.SweepEnvelope(Conductor.SweepRiseSeconds), 3);
        Assert.Equal(Conductor.SweepPlateau, Conductor.SweepEnvelope(1000), 3);             // held, no fall
        Assert.Equal(Conductor.SweepPlateau, Conductor.SweepEnvelope(SignalsToBeat.ChakraSweepTotalSeconds), 3);
    }

    [Fact]
    public void Sweep_Bed_Stays_Present_Where_The_Breathing_Cycle_Would_Have_Faded()
    {
        // Deep into the journey the breathing cycle is well into its fall (near silence); the sweep
        // envelope is still holding its plateau, so the late chakras (Throat/ThirdEye/Crown) are heard.
        double lateJourney = 1100; // ~18 min in — inside CycleEnvelope's quiet fall
        Assert.True(Conductor.SweepEnvelope(lateJourney) > Conductor.CycleEnvelope(lateJourney));
        Assert.Equal(Conductor.SweepPlateau, Conductor.SweepEnvelope(lateJourney), 3);
    }

    // ---- Step honours the build override ----

    private static BeatSpec SweepSpec(int bpm = 66) => new(
        BeatPreset.ChakraSweep, bpm, BeatScale.MajorPentatonic, "D3", LoopBars: 4,
        Density: 0.4, Swing: 0.0, GhostNotes: 0.0, Accents: new[] { 0 },
        Layers: new[] { BeatLayer.Pulse });

    [Fact]
    public void Step_With_Build_Override_Drives_Texture_Independent_Of_The_Breathing_Cycle()
    {
        // At t=0 the breathing cycle is silent (density floor); forcing a plateau build makes the bed
        // present from the very first loop — that is what keeps the opening Root chakra audible.
        var breathing = Conductor.Step(SweepSpec(), 0.5, elapsedSeconds: 0, dtSeconds: 5, Lo, Hi);
        var plateau   = Conductor.Step(SweepSpec(), 0.5, elapsedSeconds: 0, dtSeconds: 5, Lo, Hi,
                                       sensitivity: 1.0, buildOverride: Conductor.SweepPlateau);
        Assert.True(plateau.Density > breathing.Density);
    }

    [Fact]
    public void Step_On_The_Plateau_Keeps_Bass_Pulse_And_Bowl()
    {
        var spec = Conductor.Step(SweepSpec(), 0.5, elapsedSeconds: 200, dtSeconds: 5, Lo, Hi,
                                  sensitivity: 1.0, buildOverride: Conductor.SweepPlateau);
        Assert.Contains(BeatLayer.Pulse, spec.Layers);
        Assert.Contains(BeatLayer.Bass, spec.Layers);
        Assert.Contains(BeatLayer.Bowl, spec.Layers); // chakra-style: bowl from the start
        Assert.Equal(BeatPreset.ChakraSweep, spec.Preset); // tonal identity preserved
    }

    // ---- the bowl-midi override actually selects the chakra bowl ----

    [Fact]
    public void Pattern_Bowl_Override_Strikes_The_Requested_Chakra_Bowl()
    {
        var spec = SweepSpec() with { Layers = new[] { BeatLayer.Pulse, BeatLayer.Bowl } };
        int heartMidi = SignalsToBeat.ChakraBowlMidi(BeatPreset.Heart);

        var hits = BeatPattern.Build(spec, cycle: 0, intensity: 1.0, bowlMidiOverride: heartMidi);

        var bowl = hits.Where(h => h.Layer == BeatLayer.Bowl).ToList();
        Assert.NotEmpty(bowl);
        Assert.All(bowl, h => Assert.Equal(heartMidi, h.Midi));
    }

    [Fact]
    public void Pattern_Without_Override_Is_Unchanged_For_Other_Templates()
    {
        // Default bowlMidiOverride = -1 → a normal chakra still strikes its own Solfeggio bowl.
        var spec = new BeatSpec(BeatPreset.Crown, 66, BeatScale.MajorPentatonic, "D3", 4,
            0.4, 0.0, 0.0, new[] { 0 }, new[] { BeatLayer.Pulse, BeatLayer.Bowl });

        var hits = BeatPattern.Build(spec, cycle: 0);

        var bowl = hits.Where(h => h.Layer == BeatLayer.Bowl).ToList();
        Assert.NotEmpty(bowl);
        Assert.All(bowl, h => Assert.Equal(SignalsToBeat.ChakraBowlMidi(BeatPreset.Crown), h.Midi));
    }

    [Fact]
    public void Sweep_Uses_The_Musical_IIVI_Bass_Like_The_Other_Bowl_Templates()
    {
        var spec = SweepSpec() with { Layers = new[] { BeatLayer.Pulse, BeatLayer.Bass } };
        var hits = BeatPattern.Build(spec, cycle: 0);
        // Bar-start bass on every bar (the I-I-V-I foundation) → at least one Bass hit at step 0.
        Assert.Contains(hits, h => h.Layer == BeatLayer.Bass && h.Step == 0);
    }
}
