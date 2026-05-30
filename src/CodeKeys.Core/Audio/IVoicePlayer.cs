namespace CodeKeys.Core.Audio;

/// <summary>
/// Anything that can play a pre-baked voice buffer. Lets the keystroke routing logic
/// (and its tests) stay independent of NAudio / the real audio device.
/// </summary>
public interface IVoicePlayer
{
    void Play(SampleBuffer buffer);
}
