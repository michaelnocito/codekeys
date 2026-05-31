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
    /// <summary>Build the hit timeline for one loop of <paramref name="spec"/>.</summary>
    public static IReadOnlyList<BeatHit> Build(BeatSpec spec)
    {
        int steps = spec.LoopBars * 16;
        var scale = SignalsToBeat.ToScale(spec.Scale);
        int root = NoteUtil.ParseNoteName(spec.Root);
        int span = scale.DegreeSpan(2);

        // Seed from the spec so the same beat is always laid out the same way.
        var rng = new Prng(Fnv.Hash($"{spec.Preset}|{spec.Bpm}|{spec.Root}|{spec.LoopBars}|{spec.Density:F3}"));
        var accents = new HashSet<int>(spec.Accents);
        var hits = new List<BeatHit>();

        bool Has(BeatLayer l) => Array.IndexOf(spec.Layers, l) >= 0;

        for (int s = 0; s < steps; s++)
        {
            double swing = (s % 2 == 1) ? spec.Swing : 0.0;
            double accentGain = accents.Contains(s) ? 1.0 : 0.8;

            // Pulse: kick on every beat (every 4 steps); an off-beat appears when busy.
            if (Has(BeatLayer.Pulse))
            {
                if (s % 4 == 0)
                    hits.Add(new BeatHit(s, BeatLayer.Pulse, root, accentGain, swing));
                else if (s % 4 == 2 && spec.Density > 0.6)
                    hits.Add(new BeatHit(s, BeatLayer.Pulse, root, 0.6, swing));
            }

            // Marimba: density-driven scale notes on the 8th-note grid.
            if (Has(BeatLayer.Marimba) && s % 2 == 0 && rng.Next() < spec.Density * 0.6)
            {
                int degree = (int)(rng.Next() * span);
                int midi = scale.DegreeToMidi(root + 12, degree);
                hits.Add(new BeatHit(s, BeatLayer.Marimba, midi, accents.Contains(s) ? 0.9 : 0.7, swing));
            }

            // Arp: steady ascending scale run on the 8th-note grid.
            if (Has(BeatLayer.Arp) && s % 2 == 0)
            {
                int degree = (s / 2) % span;
                int midi = scale.DegreeToMidi(root + 12, degree);
                hits.Add(new BeatHit(s, BeatLayer.Arp, midi, 0.45, swing));
            }

            // Ghost: glitchy fills, probability scaled by the backspace-driven ghost amount.
            if (Has(BeatLayer.Ghost) && rng.Next() < spec.GhostNotes * 0.25)
                hits.Add(new BeatHit(s, BeatLayer.Ghost, root + 24, 0.35, swing));
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
