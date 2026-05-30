namespace CodeKeys.Core.Audio;

/// <summary>
/// A block of mono PCM audio as 32-bit floats in [-1, 1], plus its sample rate.
/// Core generates these in RAM at pack-load; the app layer streams them through NAudio.
/// </summary>
public sealed class SampleBuffer
{
    public float[] Samples { get; }
    public int SampleRate { get; }

    public SampleBuffer(float[] samples, int sampleRate)
    {
        if (samples is null) throw new ArgumentNullException(nameof(samples));
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        Samples = samples;
        SampleRate = sampleRate;
    }

    public int Length => Samples.Length;
    public double DurationSeconds => Samples.Length / (double)SampleRate;

    /// <summary>Peak absolute amplitude — handy for tests and normalization.</summary>
    public float Peak()
    {
        float peak = 0f;
        foreach (var s in Samples)
        {
            float a = Math.Abs(s);
            if (a > peak) peak = a;
        }
        return peak;
    }

    /// <summary>Scale every sample so the loudest sits at <paramref name="target"/> (no-op for silence).</summary>
    public void NormalizeInPlace(float target = 0.9f)
    {
        float peak = Peak();
        if (peak <= 1e-6f) return;
        float gain = target / peak;
        for (int i = 0; i < Samples.Length; i++)
            Samples[i] *= gain;
    }

    public void ApplyGainInPlace(float gain)
    {
        for (int i = 0; i < Samples.Length; i++)
            Samples[i] *= gain;
    }
}
