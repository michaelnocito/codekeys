using System.Collections.Generic;
using System.Linq;
using CodeKeys.Core.Beat;
using Xunit;

namespace CodeKeys.Tests;

/// <summary>
/// The optional "living events" channel: a self-calibrating, derivative-driven accent detector
/// (PlantWave-style — fire when a change is large relative to the signal's own recent variability).
/// </summary>
public class LivingEventsTests
{
    private static List<LivingEventKind> Feed(LivingEventDetector d, params double[] arousals)
        => arousals.Select(d.Push).ToList();

    // A short steady priming run (small alternating deltas) that gets past warmup with a tiny stddev.
    private static readonly double[] Prime = { 0.30, 0.32, 0.30, 0.32, 0.30 };

    [Fact]
    public void First_Reading_Never_Fires()
    {
        Assert.Equal(LivingEventKind.None, new LivingEventDetector().Push(0.5));
    }

    [Fact]
    public void Does_Not_Fire_During_Warmup_Even_On_A_Big_Jump()
    {
        var d = new LivingEventDetector();
        // Only two readings in → no history yet → a huge jump still can't fire.
        Assert.Equal(LivingEventKind.None, d.Push(0.20));
        Assert.Equal(LivingEventKind.None, d.Push(0.95));
    }

    [Fact]
    public void Rising_Burst_Fires_A_Chime_Event()
    {
        var d = new LivingEventDetector();
        Feed(d, Prime);
        Assert.Equal(LivingEventKind.Rising, d.Push(0.60)); // +0.30 jump, far above recent spread
    }

    [Fact]
    public void Falling_Settle_Fires_A_Splash_Event()
    {
        var d = new LivingEventDetector();
        Feed(d, Prime);
        Assert.Equal(LivingEventKind.Falling, d.Push(0.05)); // -0.25 drop
    }

    [Fact]
    public void Steady_Typing_Never_Fires()
    {
        var d = new LivingEventDetector();
        // Constant, then sub-floor jitter (±0.01 < MinDelta) — nothing should ever fire.
        var results = Feed(d, 0.5, 0.5, 0.5, 0.5, 0.5, 0.51, 0.50, 0.51, 0.50, 0.51, 0.50);
        Assert.All(results, r => Assert.Equal(LivingEventKind.None, r));
    }

    [Fact]
    public void Events_Respect_The_Cooldown_After_Firing()
    {
        var d = new LivingEventDetector();
        Feed(d, Prime);
        Assert.Equal(LivingEventKind.Rising, d.Push(0.60)); // fires, starts cooldown
        // The next Cooldown readings are suppressed even though they are large changes.
        Assert.Equal(LivingEventKind.None, d.Push(0.90));
        Assert.Equal(LivingEventKind.None, d.Push(0.60));
    }

    [Fact]
    public void Reset_Clears_History_So_Warmup_Restarts()
    {
        var d = new LivingEventDetector();
        Feed(d, Prime);
        d.Reset();
        // After reset, the first reading is a baseline again and an immediate jump can't fire (warmup).
        Assert.Equal(LivingEventKind.None, d.Push(0.30));
        Assert.Equal(LivingEventKind.None, d.Push(0.90));
    }

    [Fact]
    public void Detector_Is_Deterministic()
    {
        var seq = new[] { 0.30, 0.32, 0.30, 0.32, 0.30, 0.60, 0.10, 0.12, 0.55 };
        Assert.Equal(Feed(new LivingEventDetector(), seq), Feed(new LivingEventDetector(), seq));
    }
}
