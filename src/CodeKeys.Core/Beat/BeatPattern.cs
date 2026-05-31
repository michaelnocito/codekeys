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
                    // Quarter-note anchor. In buildup (intensity<1) the bar's first kick always
                    // plays, but the other quarters fill in as intensity rises — a sparse heartbeat
                    // that grows into a steady pulse. At intensity 1.0 this is a no-op (always plays).
                    if (intensity >= 1.0 || s % 16 == 0 || rng.Next() < intensity)
                        hits.Add(new BeatHit(s, BeatLayer.Pulse, root, accentGain, swing));
                }
                else if (s % 2 == 0 && rng.Next() < spec.Density * 0.30)
                    hits.Add(new BeatHit(s, BeatLayer.Pulse, root, 0.5, swing));
            }

            // Bass: low, warm root-anchored notes — the body of the "blanket". Mostly the root with
            // an occasional fifth; an extra note appears when it's fuller (warmer). Low register, no
            // high tones. Density (= the conductor's warmth) drives how full it gets.
            if (Has(BeatLayer.Bass))
            {
                if (s % 8 == 0)
                {
                    int deg = rng.Next() < 0.25 ? 4 : 0; // mostly root, sometimes the fifth
                    int midi = scale.DegreeToMidi(root, deg);
                    hits.Add(new BeatHit(s, BeatLayer.Bass, midi, accents.Contains(s) ? 0.55 : 0.45, swing));
                }
                else if (s % 4 == 0 && rng.Next() < spec.Density * 0.5)
                    hits.Add(new BeatHit(s, BeatLayer.Bass, root, 0.4, swing));
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
