using CodeKeys.Core.Music;

namespace CodeKeys.Core.Beat;

/// <summary>One scheduled note in a loop: which 16th-note step, which layer, what pitch, how loud, and its swing offset.</summary>
public readonly record struct BeatHit(int Step, BeatLayer Layer, int Midi, double Gain, double SwingFraction);

/// <summary>
/// Pure, deterministic translation of a <see cref="BeatSpec"/> into a one-loop timeline of
/// hits. No audio — the renderer turns these into sound. All pitches come from the spec's
/// scale + root (the single tonal source of truth), so nothing can clash with the keys.
/// </summary>
public static class BeatPattern
{
    /// <summary>
    /// Bank-key offset for the LONG bass variant (a much-longer-decay sine baked at the same pitch).
    /// The pattern adds this to the natural MIDI when it wants the long variant; the renderer's bank
    /// keys both short and long under (Bass, midi) and (Bass, midi + LongBassOffset) respectively.
    /// </summary>
    public const int LongBassOffset = 1200; // 100 octaves — large + divisible by 12 so pitch class is preserved
    /// <summary>
    /// Build the hit timeline for one loop of <paramref name="spec"/>. <paramref name="cycle"/> is
    /// the loop index — it seeds the per-loop variation, so consecutive loops differ (varying
    /// off-beats, marimba notes, and a periodic fill) instead of repeating dead. The quarter-note
    /// downbeat pulse stays constant, so the groove still "maintains the pulse".
    /// <paramref name="intensity"/> (0..1, default 1 = full) is the note-fill factor used by
    /// "buildup" mode: below 1 it thins the kick + melody so the texture starts sparse and fills in.
    /// At 1.0 it is a no-op (and consumes no randomness), so normal playback is unchanged.
    /// </summary>
    public static IReadOnlyList<BeatHit> Build(BeatSpec spec, int cycle = 0, double intensity = 1.0)
    {
        int steps = spec.LoopBars * 16;
        var scale = SignalsToBeat.ToScale(spec.Scale);
        int root = NoteUtil.ParseNoteName(spec.Root);
        int span = scale.DegreeSpan(2);

        // Seed from the spec + the loop index so each loop gets its own (deterministic) variation.
        var rng = new Prng(Fnv.Hash($"{spec.Preset}|{spec.Bpm}|{spec.Root}|{spec.LoopBars}|{spec.Density:F3}|{cycle}"));
        var accents = new HashSet<int>(spec.Accents);
        var hits = new List<BeatHit>();

        bool Has(BeatLayer l) => Array.IndexOf(spec.Layers, l) >= 0;
        double SwingAt(int step) => (step % 2 == 1) ? spec.Swing : 0.0;

        for (int s = 0; s < steps; s++)
        {
            double swing = SwingAt(s);
            double accentGain = accents.Contains(s) ? 1.0 : 0.8;

            // Pulse: a steady quarter-note kick (the anchor that always holds), plus a light
            // off-beat "and" kick that comes and goes per loop — subtle syncopation, more when busy.
            if (Has(BeatLayer.Pulse))
            {
                if (s % 4 == 0)
                {
                    // Bar-start kicks gated by intensity too — that's the "almost not noticeable"
                    // start. The floor is bumped (0.15) so even at intensity=0 there's an
                    // ~1-per-loop chance — a perceptible heartbeat so the user knows the bed is on.
                    // And the very first beat of cycle 0 is ALWAYS played so Beat-on gives an
                    // immediate confirmation. At intensity == 1.0 short-circuits byte-identical.
                    bool isBarStart = s % 16 == 0;
                    bool firstHitEver = cycle == 0 && s == 0;
                    // Lower bar-start floor so the early cycle has occasional, sparse "pops"
                    // sitting over the continuous Bass hum instead of frequent kicks.
                    double prob = isBarStart ? 0.05 + 0.85 * intensity : intensity * 0.5;
                    if (firstHitEver || intensity >= 1.0 || rng.Next() < prob)
                        hits.Add(new BeatHit(s, BeatLayer.Pulse, root, accentGain, swing));
                }
                else if (s % 2 == 0 && rng.Next() < spec.Density * 0.30)
                    hits.Add(new BeatHit(s, BeatLayer.Pulse, root, 0.5, swing));
            }

            // Bass: deep low boom — the foundation. Per-loop pattern variance creates a playful
            // exchange of who leads and how long. Five rotating patterns vary spacing + voice length
            // (mid ~2.0s vs long ~3.6s) so the drone breathes instead of repeating identically.
            //   pattern 0: every half-bar, mostly mid                       (steady, current)
            //   pattern 1: only first half of each bar — bass rings longer  (sparse, long sustain)
            //   pattern 2: half-bar + an offbeat "answer" at s%8==6         (call-and-response)
            //   pattern 3: every half-bar, alternates root / fifth on each   (two-voice dialog)
            //   pattern 4: long-bass on bar starts, mid-bass between        (leader + chorus)
            if (Has(BeatLayer.Bass))
            {
                int pattern = cycle % 5;
                // Root chakra: only the long-sustain patterns (1 and 4). Research-grounded: root
                // wants "deep tones felt as much as heard, slow grounding bass." We anchor each
                // loop in the long-lingering bass instead of the lighter half-bar patterns.
                bool isRootChakra = spec.Preset == BeatPreset.Root;
                if (isRootChakra) pattern = (cycle % 2 == 0) ? 1 : 4;

                int halfIdx = (s / 8) % 2;     // 0 = beat 1 of bar, 1 = beat 3 of bar
                int barIdx = s / 16;

                bool playHalfBar = (s % 8 == 0) && pattern switch
                {
                    1 => halfIdx == 0,
                    4 => true,
                    _ => true,
                };

                if (playHalfBar)
                {
                    // Note choice — root by default; pattern 3 alternates root/fifth for dialog.
                    int deg = pattern switch
                    {
                        3 => halfIdx == 0 ? 0 : 4,
                        _ => rng.Next() < 0.25 ? 4 : 0,
                    };

                    // Length variant — patterns 1 and 4 favour the long lingering bass on bar starts.
                    bool useLong =
                        (pattern == 1) ||                                 // long, sparse
                        (pattern == 4 && s % 16 == 0) ||                  // leader on bar start
                        (pattern == 0 && barIdx % 2 == 0 && halfIdx == 0); // occasional long on even bars

                    int baseMidi = scale.DegreeToMidi(root - 12, deg);
                    int midi = useLong ? baseMidi + LongBassOffset : baseMidi;
                    double gain = accents.Contains(s) ? 0.6 : (halfIdx == 0 ? 0.55 : 0.45);
                    // Root chakra: 25 % bass boost so the grounding low end is the star of the show.
                    if (isRootChakra) gain = Math.Min(1.0, gain * 1.25);
                    hits.Add(new BeatHit(s, BeatLayer.Bass, midi, gain, swing));
                }

                // pattern 2: offbeat "answer" three steps past each half-bar (call-and-response).
                if (pattern == 2 && (s == 6 || s == 22 || s == 38 || s == 54))
                {
                    int midi = scale.DegreeToMidi(root - 12, 4); // fifth answers the root
                    hits.Add(new BeatHit(s, BeatLayer.Bass, midi, 0.40, 0));
                }
            }

            // Marimba: density-driven scale notes on the 8th-note grid (varies per loop via the seed).
            if (Has(BeatLayer.Marimba) && s % 2 == 0 && rng.Next() < spec.Density * 0.6)
            {
                int degree = (int)(rng.Next() * span);
                int midi = scale.DegreeToMidi(root + 12, degree);
                hits.Add(new BeatHit(s, BeatLayer.Marimba, midi, accents.Contains(s) ? 0.9 : 0.7, swing));
            }

            // Chime: sparse, soft high bells on the 8th-note grid — a little sparkle that ebbs and
            // flows with density (so it grows/shrinks "as needed") and varies per loop. A small touch.
            if (Has(BeatLayer.Chime) && s % 2 == 0 && rng.Next() < spec.Density * 0.10)
            {
                int[] tones = { 0, 2, 4 }; // high chord tones only → always consonant, never busy
                int deg = tones[(int)(rng.Next() * tones.Length)];
                int midi = scale.DegreeToMidi(root + 24, deg);
                hits.Add(new BeatHit(s, BeatLayer.Chime, midi, 0.25, swing));
            }

            // Ghost: glitchy fills, probability scaled by the backspace-driven ghost amount.
            if (Has(BeatLayer.Ghost) && rng.Next() < spec.GhostNotes * 0.25)
                hits.Add(new BeatHit(s, BeatLayer.Ghost, root + 24, 0.35, swing));
        }

        // Back-beat fill: every other loop, a soft pickup into the next downbeat so the groove
        // breathes instead of looping identically. A kick on the final "and" (kept on the beat-grid
        // so the pulse stays clean) plus an optional ghost tick on the last 16th.
        if (Has(BeatLayer.Pulse) && cycle % 2 == 1 && steps >= 4)
        {
            hits.Add(new BeatHit(steps - 2, BeatLayer.Pulse, root, 0.55, SwingAt(steps - 2)));
            if (Has(BeatLayer.Ghost))
                hits.Add(new BeatHit(steps - 1, BeatLayer.Ghost, root + 24, 0.30, SwingAt(steps - 1)));
        }

        // Bowl: makes "appearances" of ~2 measures (the bowl itself is a 10 s envelope: 1.5 s
        // ascend → 3.5 s sustain → ~5 s noticeable trailing fade-away), then silence between
        // appearances. The spacing varies with the spec's density:
        //   dense  (up feel)   → strike every loop          (~14 s spacing → mostly appearances)
        //   medium             → strike every other loop    (~29 s spacing → equal on/off)
        //   sparse (calm)      → strike every 4 loops       (~58 s spacing → long silences)
        // Bowl is well behind the bass — total summed peak ≈ ¼ bass — bass stars.
        if (Has(BeatLayer.Bowl))
        {
            // density → spacing in loops. Density 0.04 → 4, density 0.85 → 1. Clamped to [1, 4].
            int spacingLoops = Math.Max(1, (int)Math.Round(4.5 - 4.5 * spec.Density));
            if (cycle % spacingLoops == 0)
            {
                var chakraFreq = SignalsToBeat.ChakraBowlFreq(spec.Preset);
                int primaryMidi;
                if (chakraFreq.HasValue)
                    primaryMidi = SignalsToBeat.ChakraBowlMidi(spec.Preset);
                else
                {
                    int deg = rng.Next() < 0.30 ? 4 : 0;
                    primaryMidi = scale.DegreeToMidi(root, deg);
                }

                // Bar 0 downbeat appearance. Hit gain dropped 50 % from 0.10 → 0.05 — bass is the
                // star, the bowl is a soft consistent background presence.
                hits.Add(new BeatHit(0, BeatLayer.Bowl, primaryMidi, 0.05, 0));
            }
        }

        // Splash: a rare, soft, dark one-off for variety — an "appearance", not a layer that rides
        // the beat. One note at most per loop, on a beat, in the mid-low register with a slow attack.
        // High/bright sounds and sharp transients capture focus (auditory-salience research), so this
        // deliberately avoids both. Varies per loop via the cycle seed.
        if (Has(BeatLayer.Splash) && rng.Next() < 0.22)
        {
            int bar = (int)(rng.Next() * spec.LoopBars);
            int beat = new[] { 0, 4, 8, 12 }[(int)(rng.Next() * 4)];
            int step = bar * 16 + beat;
            int[] tones = { 0, 2, 4 };
            int deg = tones[(int)(rng.Next() * tones.Length)];
            hits.Add(new BeatHit(step, BeatLayer.Splash, scale.DegreeToMidi(root, deg), 0.30, SwingAt(step)));
        }

        // Melody: a one-bar motif laid out per bar as call-and-response. Even bars state the
        // motif; odd bars "answer" it by resolving to the tonic. The motif is seeded only from
        // the spec's *stable* identity (preset/scale/root) — NOT bpm/density/accents/cycle — so
        // neither tempo drift nor per-loop variation scrambles the tune; it stays recognizable.
        if (Has(BeatLayer.Melody))
        {
            // Seed ONLY from tonal identity (preset/scale/root) — NOT bpm/density/loopBars — so the
            // conductor's tempo drift can't scramble the tune. The melody stays recognizable all session.
            var motif = MotifFactory.Generate(
                Fnv.Hash($"motif|{spec.Preset}|{spec.Scale}|{spec.Root}"),
                scale.Intervals.Count);
            var answer = MotifFactory.WithResolvedEnding(motif);
            int melodyBase = root + 12; // the tune sits an octave above the root

            for (int bar = 0; bar < spec.LoopBars; bar++)
            {
                var phrase = (bar % 2 == 1) ? answer : motif;
                int barStart = bar * 16;
                foreach (var mn in phrase.Notes)
                {
                    // Buildup: notes fill in as intensity rises (sparse → coherent melody). No-op at 1.0.
                    if (intensity < 1.0 && rng.Next() >= intensity) continue;
                    int step = barStart + mn.Step;
                    double swing = (step % 2 == 1) ? spec.Swing : 0.0;
                    double gain = accents.Contains(step) ? Math.Min(1.0, mn.Gain + 0.1) : mn.Gain;
                    int midi = scale.DegreeToMidi(melodyBase, mn.Degree);
                    hits.Add(new BeatHit(step, BeatLayer.Melody, midi, gain, swing));
                }
            }
        }

        // Pad: a sustained root/3rd/5th chord at the top of each bar.
        if (Has(BeatLayer.Pad))
        {
            for (int bar = 0; bar < spec.LoopBars; bar++)
            {
                int s = bar * 16;
                foreach (int deg in new[] { 0, 2, 4 })
                    hits.Add(new BeatHit(s, BeatLayer.Pad, scale.DegreeToMidi(root, deg), 0.4, 0));
            }
        }

        return hits;
    }
}
