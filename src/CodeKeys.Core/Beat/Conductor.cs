namespace CodeKeys.Core.Beat;

/// <summary>
/// The adaptive "conductor" (pure, deterministic). Two ideas in one engine:
///
///  1. **ADDITIVE BUILD** (Reich/drum-circle "people slowly join the beat"): every session starts
///     almost silent and voices enter ONE AT A TIME over <see cref="BuildupSeconds"/> (~10 min),
///     driven by an ease-in envelope so the first minutes are barely noticeable. This is the
///     default experience — there is no separate "buildup mode" anymore.
///  2. **AROUSAL THERMOSTAT** (iso principle + Yerkes-Dodson): once the build has progressed, the
///     beat gently counter-steers tempo toward a flow band — over-aroused → ease down, under →
///     activate. The arousal response itself is **gated by the build envelope**, so during the
///     first minutes nothing reacts; it fades in as the texture does.
///
/// HONESTY: this regulates *psychological* arousal/attention. It does NOT entrain heart rate —
/// the evidence for tempo→heart-rate sync is weak — so we make no physiological claims.
/// </summary>
public static class Conductor
{
    // ---- tunables (expected to be tuned by ear) ----
    // Philosophy: this is a BACKGROUND pulse that wants to RIDE WITH the user, not corral them.
    // - Below FlowCenter (calm): hold steady at the baseline easy-flow tempo. Don't slow them down further.
    // - Between FlowCenter and TenseThreshold (flow zone): ride along with them at a fraction of
    //   their energy — they speed up, the beat picks up a touch, but never matches them 1:1.
    // - Above TenseThreshold (clearly tense): NOW counter-act and ease them back down.
    // The baseline FlowCenter maps to ~66 BPM in the Focused range — inside the research-supported
    // 60–80 BPM "relaxed-focus" band — and is the home the pulse returns to when the user is calm.
    public const double FlowCenter      = 0.5;
    public const double TenseThreshold  = 0.75;  // arousal above this = "really tense"; counter-act kicks in
    public const double RideGain        = 0.40;  // in flow zone, beat tracks arousal at this fraction (rides along)
    public const double LeadGain        = 0.40;  // how hard we counter-act once tense
    public const double ArousalMin = 0.20;   // never fully dead
    public const double ArousalMax = 0.85;   // never frantic
    public const double SlewPerSec = 0.004;  // max arousal change per second (very gentle ramp)
    public const double ResponsivenessFullAt = 300; // seconds: adaptation fades IN slowly from the base beat

    // Code Groove tempo "breathing": a slow, smooth (sine) swell of ± a few BPM layered on top of the
    // arousal ride-along, so the groove ebbs and flows at musical-phrase scale instead of locking to a
    // metronome — "well-placed tempo changes" that always stay flow-like (never a jolt). Two gentle
    // sines at different periods so the swells land at varied, non-repetitive spots.
    public const double GrooveTempoBreathSeconds = 140.0; // primary swell period (~2.3 min)
    public const double GrooveTempoBreathBpm     = 3.0;   // primary swell depth (± BPM)

    // The slow additive build over which voices enter one at a time ("people in public adding to
    // a beat" — Steve Reich's Drumming / West African drum-circle layering). DEFAULT behaviour, not
    // an opt-in: every session starts almost silent and the texture assembles over these seconds.
    public const double BuildupSeconds = 600; // 10 minutes — the RISE half of the cycle

    // After the peak the music gracefully unwinds back to silence, then the rise begins again — an
    // organic breathing pattern (like waves, or a drum circle dispersing and reforming). The fall
    // is 25% faster than the rise (Mike's "drop back down at 25% faster" request).
    public const double FallSpeedFactor = 1.25;
    public static double FallSeconds => BuildupSeconds / FallSpeedFactor;     // 480 s = 8 min
    public static double CycleSeconds => BuildupSeconds + FallSeconds;        // 1080 s = 18 min

    // Chakra Sweep uses a STEADY envelope instead of the breathing cycle: a gentle ease-in over the
    // first minute to a held plateau that stays present for the whole 21-minute journey. This keeps
    // every chakra clearly audible for its 3 minutes (the slow breathing build would leave the early
    // chakras near-silent and fade the final ones out right as the journey reaches Crown).
    public const double SweepRiseSeconds = 60.0;   // gentle fade-in to the held bed
    public const double SweepPlateau     = 0.72;   // steady texture level held across the journey

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
    /// The arousal the music should aim for given the user's. Three zones:
    ///  • Calm (≤ <see cref="FlowCenter"/>): hold steady at FlowCenter — don't slow them down further.
    ///  • Flow (FlowCenter .. <see cref="TenseThreshold"/>): RIDE ALONG at <see cref="RideGain"/>
    ///    of the deviation — the music lifts with the user but stays calmer than them, so you can
    ///    flow into a faster pace without the beat pulling you back.
    ///  • Tense (&gt; TenseThreshold): NOW counter-act — pull the target back down at
    ///    <see cref="LeadGain"/>, accelerating as the user pushes further.
    /// </summary>
    public static double MusicalTarget(double userArousal)
    {
        if (userArousal <= FlowCenter)
            return FlowCenter; // calm: hold the baseline easy flow

        if (userArousal <= TenseThreshold)
            return Clamp(FlowCenter + RideGain * (userArousal - FlowCenter), ArousalMin, ArousalMax);

        // Tense — start from the top of the ride-along range and curve back down with arousal.
        double rideTop = FlowCenter + RideGain * (TenseThreshold - FlowCenter);
        double overage = userArousal - TenseThreshold;
        return Clamp(rideTop - 2.0 * LeadGain * overage, ArousalMin, ArousalMax);
    }

    /// <summary>
    /// The rise primitive: 0..1 over <see cref="BuildupSeconds"/>, ease-in (p²). Used as the rise
    /// half of <see cref="CycleEnvelope"/>; kept public so callers can reason about a single rise.
    /// </summary>
    public static double BuildupEnvelope(double elapsedSeconds)
    {
        double p = Clamp(elapsedSeconds / BuildupSeconds, 0, 1);
        return p * p;
    }

    /// <summary>
    /// The breathing cycle envelope, **repeats forever**: an ease-in rise over
    /// <see cref="BuildupSeconds"/> (~10 min), then an ease-out fall over <see cref="FallSeconds"/>
    /// (~8 min, 25% faster than the rise), then back to the beginning. The rise is p², the fall is
    /// (1-p)² — the curves mirror, so the music slowly assembles, briefly peaks, gracefully unwinds
    /// to silence, then begins again. Returns 0..1 — the renderer + Step use it to gate volume,
    /// note-fill, voice entry, and arousal response.
    /// </summary>
    public static double CycleEnvelope(double elapsedSeconds)
    {
        double t = elapsedSeconds % CycleSeconds;
        if (t < BuildupSeconds)
        {
            double p = t / BuildupSeconds;
            return p * p; // rise
        }
        else
        {
            double p = (t - BuildupSeconds) / FallSeconds;
            double inv = 1 - p;
            return inv * inv; // fall (mirror of the rise curve, time-compressed)
        }
    }

    /// <summary>
    /// A steady "present" envelope for the Chakra Sweep: ease-in (p²) over <see cref="SweepRiseSeconds"/>
    /// up to <see cref="SweepPlateau"/>, then hold there indefinitely. No fall — the bed stays present
    /// for the whole journey so each chakra is clearly heard.
    /// </summary>
    public static double SweepEnvelope(double elapsedSeconds)
    {
        double p = Clamp(elapsedSeconds / SweepRiseSeconds, 0, 1);
        return SweepPlateau * (p * p);
    }

    /// <summary>
    /// Advance the beat one loop. Runs both the additive build (voices enter one at a time,
    /// drives note density) and the arousal thermostat (counter-steers tempo). Preserves tonal
    /// identity (preset/scale/root/loopBars) so the renderer never rebakes; only tempo, density,
    /// and active-layer membership move.
    /// </summary>
    /// <param name="sensitivity">
    /// User reactivity multiplier (1 = baseline). Scales how fast the beat moves toward target.
    /// </param>
    /// <param name="buildOverride">
    /// When &ge; 0, use this as the build/texture fraction instead of the breathing
    /// <see cref="CycleEnvelope"/>. The Chakra Sweep passes <see cref="SweepEnvelope"/> here so it
    /// rides a steady plateau rather than the rise/fall cycle. Default -1 = breathing cycle (unchanged).
    /// </param>
    public static BeatSpec Step(BeatSpec current, double userArousal, double elapsedSeconds,
                                double dtSeconds, int bpmLo, int bpmHi, double sensitivity = 1.0,
                                double buildOverride = -1.0)
    {
        // The breathing cycle: voices come in over the rise, peak briefly, unwind over the fall,
        // then begin again. `build` is the same fraction for layer thresholds and gating. The sweep
        // overrides it with a steady plateau.
        double build = buildOverride >= 0 ? buildOverride : CycleEnvelope(elapsedSeconds);

        // Read current musical arousal back from where the tempo sits in the preset's range.
        double m = bpmHi > bpmLo ? Clamp((current.Bpm - bpmLo) / (double)(bpmHi - bpmLo), 0, 1) : 0.5;

        // Two gates ride on top of the arousal response so the thermostat barely moves during the
        // build: a responsiveness ramp + the build envelope itself. Result: the first ~5 min the
        // beat doesn't really respond to typing — it's just slowly assembling.
        double responsiveness = Clamp(elapsedSeconds / ResponsivenessFullAt, 0, 1);

        double target   = MusicalTarget(userArousal);
        double maxDelta = SlewPerSec * Math.Max(dtSeconds, 0) * Math.Max(0, sensitivity);
        double move     = Clamp(target - m, -maxDelta, maxDelta) * responsiveness * build;
        // The aliveness floor also rises with the build — during the build the music IS allowed
        // to be quieter/slower than ArousalMin (that's the point); the floor reasserts as the
        // texture assembles, so steady-state still has a minimum liveness.
        double floor    = ArousalMin * build;
        double next     = Clamp(m + move, floor, ArousalMax);

        int bpm = (int)Math.Round(bpmLo + (bpmHi - bpmLo) * next, MidpointRounding.AwayFromZero);

        // Density: starts almost nothing (0.04) and rises with the build; only a small contribution
        // from arousal once the build is well underway. The renderer additionally thins kick + melody
        // by the same envelope, so even within "low density" the actual hits are still gated.
        double density = Clamp(0.04 + 0.55 * build + 0.20 * next * build, 0.04, 0.85);

        // Pad-flow templates (Dreamflow) are the exception: NO Pulse, NO Bass — a flowing pad wash
        // instead of the thump-driven bed. Pads + the floating motif play from t=0; the high shimmer
        // (Chime) eases in with the build. Returns early so none of the Pulse/Bass logic below runs.
        if (SignalsToBeat.IsPadFlow(current.Preset))
        {
            var flow = new List<BeatLayer> { BeatLayer.Pad, BeatLayer.Melody };
            if (build > 0.30) flow.Add(BeatLayer.Chime); // shimmer joins as the texture assembles
            return current with { Bpm = bpm, Density = density, Layers = flow.ToArray() };
        }

        // Groove templates (Code Groove): a real drum kit + bass, grooving from t=0 so you can code
        // to it immediately. A soft motif drifts in once you're settled (build > 0.50). Returns early
        // so the atmospheric Pulse/Bass-hum logic below never runs.
        if (SignalsToBeat.IsGroove(current.Preset))
        {
            var kit = new List<BeatLayer> { BeatLayer.Kick, BeatLayer.Snare, BeatLayer.Bass };
            if (build > 0.50) kit.Add(BeatLayer.Melody); // a gentle tune joins once you're in the flow

            // Well-placed, flow-like tempo movement: two slow sines (a primary swell plus a slower,
            // shallower one at ~1.6× the period) so the groove drifts up and eases back at varied
            // spots — gentle enough to stay in flow, but it never sits dead-on a metronome.
            double swell = GrooveTempoBreathBpm * Math.Sin(2.0 * Math.PI * elapsedSeconds / GrooveTempoBreathSeconds)
                         + GrooveTempoBreathBpm * 0.5 * Math.Sin(2.0 * Math.PI * elapsedSeconds / (GrooveTempoBreathSeconds * 1.6));
            // Ramp the swell in with the build so the opening settles before it starts breathing.
            int grooveBpm = Math.Max(1, bpm + (int)Math.Round(swell * build));
            return current with { Bpm = grooveBpm, Density = density, Layers = kit.ToArray() };
        }

        // The deep Bass hum is the FOUNDATION — always on from t=0, because Mike loves the
        // continuous low rolling drone (the half-bar Bass hits have 2s decay, so they overlap into
        // a persistent atmospheric hum). The Pulse is the gentle accent on top (sparser). Ghost
        // taps, Bowl shimmer, and Splash colour enter on the additive build's schedule. No Pad chord, no high tones.
        var layers = current.Layers
            .Where(l => l is not (BeatLayer.Pad or BeatLayer.Melody or BeatLayer.Marimba or BeatLayer.Chime or BeatLayer.Bass or BeatLayer.Splash or BeatLayer.Ghost or BeatLayer.Bowl))
            .ToList();
        if (!layers.Contains(BeatLayer.Pulse)) layers.Add(BeatLayer.Pulse); // gentle accent
        if (!layers.Contains(BeatLayer.Bass))  layers.Add(BeatLayer.Bass);  // the continuous hum
        // Every selectable template (the chakra tunings, Space Clearing, Chakra Sweep) has bowls
        // from the start. The internal `Focused` base mode keeps the same behaviour for the engine
        // and tests. Other moods (Relaxed / Burnout / Silly — dormant) keep their build-gated bowl entry.
        bool isChakra = SignalsToBeat.ChakraBowlFreq(current.Preset).HasValue;
        bool isShownBeat = isChakra || current.Preset == BeatPreset.Focused;
        if (isShownBeat) layers.Add(BeatLayer.Bowl);
        if (build > 0.30) layers.Add(BeatLayer.Ghost);  // soft taps   (~ 5.5 min in)
        if (!isShownBeat && build > 0.50) layers.Add(BeatLayer.Bowl);   // bowl strikes (~ 7 min in)
        if (build > 0.70) layers.Add(BeatLayer.Splash); // rare colour  (~ 8.4 min in)

        return current with { Bpm = bpm, Density = density, Layers = layers.ToArray() };
    }

    private static double Clamp(double x, double lo, double hi) => Math.Min(hi, Math.Max(lo, x));

    /// <summary>value in [fast,slow] ms → 1..0 (fast typing = high arousal).</summary>
    private static double NormInv(double v, double fast, double slow) => Clamp(1 - (v - fast) / (slow - fast), 0, 1);
}
