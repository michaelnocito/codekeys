using CodeKeys.Core.Audio;
using NAudio.Wave;

namespace CodeKeys.App.Audio;

/// <summary>
/// Streams a <see cref="SampleBuffer"/> as an endless seamless loop — the ambient bed.
/// Always returns a full buffer (never ends), so it stays in the mixer permanently;
/// the engine controls audibility with a separate volume stage.
/// </summary>
public sealed class LoopSampleProvider : ISampleProvider
{
    private readonly float[] _data;
    private int _pos;

    public WaveFormat WaveFormat { get; }

    public LoopSampleProvider(SampleBuffer buffer)
    {
        _data = buffer.Samples;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(buffer.SampleRate, 1);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_data.Length == 0)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        int written = 0;
        while (written < count)
        {
            int chunk = Math.Min(count - written, _data.Length - _pos);
            Array.Copy(_data, _pos, buffer, offset + written, chunk);
            written += chunk;
            _pos += chunk;
            if (_pos >= _data.Length) _pos = 0; // wrap — seam is already crossfaded
        }
        return count;
    }
}
