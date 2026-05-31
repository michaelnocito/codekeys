using CodeKeys.Core.Input;

namespace CodeKeys.Core.Beat;

/// <summary>
/// Builds <see cref="Signals"/> from a rolling window of recent keystrokes — the live capture
/// (module 1). It records only a timestamp, a <see cref="KeyKind"/>, and (for letters) an
/// upper/lower flag. It NEVER stores the characters typed, so text can't be reconstructed:
/// the README's "never stores keystrokes" promise holds. <see cref="Snapshot"/> always emits
/// an empty <c>Text</c>, so the beat seeds from the mood, not from what you wrote.
/// </summary>
public sealed class SignalsCollector
{
    private readonly long _windowMs;
    private readonly object _gate = new();
    private readonly Queue<Entry> _events = new();

    private readonly record struct Entry(long T, KeyKind Kind, bool Upper);

    // A long window (30s) on purpose: it averages over far more typing, so the arousal estimate
    // is stable and not hyper-responsive — we want to read a settled trend, not every burst.
    public SignalsCollector(long windowMs = 30000) => _windowMs = windowMs;

    /// <summary>Record one key-down. Pure modifiers and unknown keys are ignored.</summary>
    public void Record(long timestampMs, KeyKind kind, bool isUpper = false)
    {
        if (kind is KeyKind.Modifier or KeyKind.Other) return;
        lock (_gate)
        {
            _events.Enqueue(new Entry(timestampMs, kind, isUpper));
            while (_events.Count > 0 && timestampMs - _events.Peek().T > _windowMs)
                _events.Dequeue();
        }
    }

    /// <summary>Snapshot the current window as <see cref="Signals"/>. Text is always empty (privacy).</summary>
    public Signals Snapshot()
    {
        Entry[] arr;
        lock (_gate) arr = _events.ToArray();

        if (arr.Length == 0)
            return new Signals { Text = "" };

        int letters = 0, upper = 0, punct = 0, backspaces = 0, chars = 0;
        double gapSum = 0;
        int gapCount = 0;
        var gaps = new double[arr.Length - 1];

        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i].Kind)
            {
                case KeyKind.Letter: letters++; chars++; if (arr[i].Upper) upper++; break;
                case KeyKind.Digit: chars++; break;
                case KeyKind.Space: chars++; break;
                case KeyKind.Punctuation: chars++; punct++; break;
                case KeyKind.Backspace: backspaces++; break;
            }
            if (i > 0)
            {
                double g = arr[i].T - arr[i - 1].T;
                gaps[gapCount++] = g;
                gapSum += g;
            }
        }

        double avgGap = gapCount > 0 ? gapSum / gapCount : 0;
        double variance = 0;
        if (gapCount > 0)
        {
            double sq = 0;
            for (int i = 0; i < gapCount; i++) { double d = gaps[i] - avgGap; sq += d * d; }
            variance = Math.Sqrt(sq / gapCount); // std-dev in ms (the brain normalizes it)
        }

        return new Signals
        {
            Text = "",
            DurationMs = arr[^1].T - arr[0].T,
            CharCount = chars,
            Backspaces = backspaces,
            AvgGapMs = avgGap,
            GapVariance = variance,
            CapsRatio = letters > 0 ? (double)upper / letters : 0,
            PunctCount = punct,
        };
    }
}
