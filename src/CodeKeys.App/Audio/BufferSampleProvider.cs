using CodeKeys.Core.Audio;
using NAudio.Wave;

namespace CodeKeys.App.Audio;

/// <summary>
/// Plays a pre-baked <see cref="SampleBuffer"/> exactly once, then reports end-of-stream.
/// The mixer auto-removes it when finished, so a key press is just "add one of these."
/// </summary>
public sealed class BufferSampleProvider : ISampleProvider
{
    private readonly float[] _data;
    private int _pos;
    private readonly float _gain;

    public WaveFormat WaveFormat { get; }

    /// <summary>Monotonic id so the engine can cap polyphony by dropping the oldest voice.</summary>
    public long Id { get; }

    public bool Finished => _pos >= _data.Length;

    public BufferSampleProvider(SampleBuffer buffer, long id, float gain = 1f)
    {
        _data = buffer.Samples;
        _gain = gain;
        Id = id;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(buffer.SampleRate, 1);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int remaining = _data.Length - _pos;
        if (remaining <= 0) return 0; // signals the mixer to remove this voice

        int n = Math.Min(remaining, count);
        if (_gain == 1f)
            Array.Copy(_data, _pos, buffer, offset, n);
        else
            for (int i = 0; i < n; i++) buffer[offset + i] = _data[_pos + i] * _gain;

        _pos += n;
        return n;
    }
}
