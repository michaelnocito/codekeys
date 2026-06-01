namespace CodeKeys.Core.Beat;

/// <summary>What a living-events reading produced this tick.</summary>
public enum LivingEventKind
{
    None,
    /// <summary>Arousal jumped UP (a burst of flow/speed) — fire a soft chime.</summary>
    Rising,
    /// <summary>Arousal dropped (settling / a correction pause) — fire a soft splash.</summary>
    Falling,
}

/// <summary>
/// A pure, deterministic "living events" detector — the optional channel inspired by how PlantWave /
/// the MIDI Sprout decide WHEN to add an extra sound. Plant devices fire a note only when a fresh
/// fluctuation is large *relative to the signal's own recent variability* (delta &gt; stddev ×
/// threshold), and drive effects from the signal's rate-of-change. We do the same with the typing
/// arousal stream: each reading we look at the CHANGE since the last reading and fire a one-shot
/// accent only when that change is large relative to the recent spread of changes — so it
/// self-calibrates to each typist and each session instead of using a fixed probability.
///
/// Rising change → a flow burst (soft chime). Falling change → settling / a correction (soft splash).
/// A warmup (needs a little history first), an absolute floor (steady signals stay silent even when
/// the local stddev is tiny), and a cooldown (no clustering) keep it sparse and tasteful.
/// </summary>
public sealed class LivingEventDetector
{
    // ---- tunables (tuned by ear; readings arrive ~every 3 s) ----
    /// <summary>How many recent deltas define "recent variability".</summary>
    public const int Window = 8;
    /// <summary>Need this many deltas of history before the std-dev test is meaningful.</summary>
    public const int Warmup = 4;
    /// <summary>A change must exceed (recent stddev × this) to fire — the self-calibrating part.</summary>
    public const double Threshold = 1.6;
    /// <summary>Absolute floor: ignore changes smaller than this so a very steady signal stays silent.</summary>
    public const double MinDelta = 0.06;
    /// <summary>Readings to stay quiet after a fire, so events never cluster.</summary>
    public const int Cooldown = 2;

    private double? _prev;
    private readonly Queue<double> _deltas = new();
    private int _cool;

    /// <summary>Forget all history (call when the bed restarts or the channel is toggled on).</summary>
    public void Reset()
    {
        _prev = null;
        _deltas.Clear();
        _cool = 0;
    }

    /// <summary>
    /// Feed the latest arousal reading (0..1). Returns whether a one-shot accent should fire now.
    /// The new change is tested against the spread of PRIOR changes (not including itself), so a
    /// genuine spike stands out instead of inflating its own threshold.
    /// </summary>
    public LivingEventKind Push(double arousal)
    {
        arousal = Math.Min(1.0, Math.Max(0.0, arousal));

        if (_prev is null) { _prev = arousal; return LivingEventKind.None; }

        double delta = arousal - _prev.Value;
        _prev = arousal;

        var result = LivingEventKind.None;
        if (_cool > 0)
        {
            _cool--;
        }
        else if (_deltas.Count >= Warmup)
        {
            double sd = StdDev(_deltas);
            double mag = Math.Abs(delta);
            if (mag >= MinDelta && mag > sd * Threshold)
            {
                result = delta > 0 ? LivingEventKind.Rising : LivingEventKind.Falling;
                _cool = Cooldown;
            }
        }

        _deltas.Enqueue(delta);
        while (_deltas.Count > Window) _deltas.Dequeue();

        return result;
    }

    private static double StdDev(Queue<double> xs)
    {
        int n = xs.Count;
        if (n == 0) return 0;
        double mean = 0;
        foreach (var x in xs) mean += x;
        mean /= n;
        double sumSq = 0;
        foreach (var x in xs) { double d = x - mean; sumSq += d * d; }
        return Math.Sqrt(sumSq / n);
    }
}
