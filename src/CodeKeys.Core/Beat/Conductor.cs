namespace CodeKeys.Core.Beat;

/// <summary>
/// The adaptive "conductor" (pure, deterministic): it keeps the typist in a flow band by gently
/// steering the beat instead of jumping it. Grounded in two ideas from the research:
///  • the ISO PRINCIPLE — meet the user near their current arousal, then *lead* gradually;
///  • the YERKES-DODSON inverted-U — performance peaks at an *optimal* (not minimal) arousal, so
///    the target is a flow band and the response is COUNTER-ACTIVE: speeding up / erratic typing
///    eases the beat down to settle the user; slowing / idle typing lifts it to re-activate them.
/// Every change is rate-limited, so it's a slow unobtrusive ramp (~a minute to cross the range),
/// never an abrupt switch. A session ARC also builds the bed in over the first ~12 minutes.
///
/// HONESTY: this regulates *psychological* arousal/attention. It does NOT entrain heart rate —
/// the evidence for tempo→heart-rate sync is weak — so we make no physiological claims.
/// </summary>
public static class Conductor
{
    // ---- tunables (expected to be tuned by ear) ----
    // Philosophy: this is a BACKGROUND pulse. It holds steady and only guides when it's really sure
    // the user has drifted — the user is doing another task, so we create space, we don't compete.
    // The resting/flow ANCHOR. Maps to ~66 BPM in the Focused range — squarely inside the
    // 60–80 BPM "relaxed-focus" band the research favours. Over- or under-stimulation is always
    // steered back toward this point (see MusicalTarget): it's the home the pulse returns to.
    public const double FlowCenter = 0.5;
    public const double LeadGain   = 0.25;   // how hard we steer back — low, so any guidance is gentle
    public const double Deadband   = 0.18;   // only react once the user is CLEARLY outside this band
    public const double ArousalMin = 0.20;   // never fully dead
    public const double ArousalMax = 0.85;   // never frantic
    public const double SlewPerSec = 0.004;  // max arousal change per second (very gentle ramp)
    public const double ResponsivenessFullAt = 300; // seconds: adaptation fades IN slowly from the base beat

    // session-arc phase boundaries, in seconds
    public const double EstablishUntil   = 120;  // 0–2 min: pad + pulse only, sparse
    public const double StatementUntil   = 360;  // 2–6 min: the melody enters
    public const double DevelopmentUntil = 720;  // 6–12 min: marimba/harmony joins, fuller
    // 12 min+ : a sustained Flow plateau (the arousal thermostat does the moment-to-moment work)

    public enum Phase { Establish, Statement, Development, Flow }

    public static Phase PhaseAt(double elapsedSeconds) =>
        elapsedSeconds < EstablishUntil   ? Phase.Establish :
        elapsedSeconds < StatementUntil   ? Phase.Statement :
        elapsedSeconds < DevelopmentUntil ? Phase.Development :
                                            Phase.Flow;

    /// <summary>
    /// Estimate the typist's arousal in [0,1] from a privacy-safe signals snapshot: mostly speed,
    /// plus erraticness (gap variance) and struggle (backspaces) as stress amplifiers.
    /// </summary>
    public static double Estimate(Signals s)
    {
        // No recent typing → read as low engagement (idle) so the bed gently keeps things moving.
        if (s.CharCount < 2 || s.AvgGapMs <= 0) return 0.25;

        double speed    = NormInv(s.AvgGapMs, 80, 500);                                   // fast typing → high
        double erratic  = s.GapVariance > 1 ? Clamp(s.GapVariance / 300, 0, 1) : Clamp(s.GapVariance, 0, 1);
        double struggle = Clamp(s.Backspaces / (double)Math.Max(s.CharCount, 8), 0, 1);
        return Clamp(0.55 * speed + 0.25 * erratic + 0.20 * struggle, 0, 1);
    }

    /// <summary>
    /// The arousal the music should aim for given the user's: a counter-active reflection about the
    /// flow centre — over-aroused → aim lower (settle), under-aroused → aim higher (activate). Inside
    /// the <see cref="Deadband"/> we don't react at all (aim = centre / hold), so the pulse only
    /// guides once the user has clearly drifted; the response then ramps in from the band edge.
    /// </summary>
    public static double MusicalTarget(double userArousal)
    {
        double dev = userArousal - FlowCenter;
        double beyond = Math.Abs(dev) <= Deadband ? 0.0 : dev - Math.Sign(dev) * Deadband;
        return Clamp(FlowCenter - LeadGain * beyond, ArousalMin, ArousalMax);
    }

    /// <summary>
    /// Advance the beat one loop toward the flow target and along the session arc. Preserves tonal
    /// identity (preset/scale/root/loopBars) so the renderer never rebakes; only tempo, density and
    /// active layers move — and tempo/density only by a small rate-limited step.
    /// </summary>
    public static BeatSpec Step(BeatSpec current, double userArousal, double elapsedSeconds,
                                double dtSeconds, int bpmLo, int bpmHi)
    {
        // Read current musical arousal back from where the tempo sits in the preset's range.
        double m = bpmHi > bpmLo ? Clamp((current.Bpm - bpmLo) / (double)(bpmHi - bpmLo), 0, 1) : 0.5;

        // Responsiveness fades in over the first few minutes: early on the beat just holds the base
        // pulse, then it gradually starts responding to typing — a slow, perceptible transition.
        double responsiveness = Clamp(elapsedSeconds / ResponsivenessFullAt, 0, 1);

        double target   = MusicalTarget(userArousal);
        double maxDelta = SlewPerSec * Math.Max(dtSeconds, 0);
        double move     = Clamp(target - m, -maxDelta, maxDelta) * responsiveness;
        double next     = Clamp(m + move, ArousalMin, ArousalMax);

        int bpm = (int)Math.Round(bpmLo + (bpmHi - bpmLo) * next, MidpointRounding.AwayFromZero);

        var phase = PhaseAt(elapsedSeconds);
        double arcMult = phase switch
        {
            Phase.Establish => 0.55,
            Phase.Statement => 0.75,
            _               => 0.9, // Development + Flow — kept under 1 to leave space
        };
        double density = Clamp((0.28 + 0.42 * next) * arcMult, 0.12, 0.85);

        // The arc owns the two "developmental" voices; pad/pulse/ghost are kept as-is.
        var layers = current.Layers.Where(l => l != BeatLayer.Melody && l != BeatLayer.Marimba).ToList();
        if (phase >= Phase.Statement)   layers.Add(BeatLayer.Melody);
        if (phase >= Phase.Development) layers.Add(BeatLayer.Marimba);

        return current with { Bpm = bpm, Density = density, Layers = layers.ToArray() };
    }

    private static double Clamp(double x, double lo, double hi) => Math.Min(hi, Math.Max(lo, x));

    /// <summary>value in [fast,slow] ms → 1..0 (fast typing = high arousal).</summary>
    private static double NormInv(double v, double fast, double slow) => Clamp(1 - (v - fast) / (slow - fast), 0, 1);
}
