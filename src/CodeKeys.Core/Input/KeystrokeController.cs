using CodeKeys.Core.Audio;

namespace CodeKeys.Core.Input;

/// <summary>
/// Turns raw key-down / key-up events into played voices. This is the stateless-ish
/// heart of the keystroke layer, deliberately separated from the Win32 hook so it can
/// be unit-tested with a fake player.
///
/// Responsibilities:
///   • Suppress OS auto-repeat — a held key fires one sound, not a machine-gun.
///   • Respect the layer enable toggle.
///   • Resolve each key through the spatial map and play the matching baked buffer.
/// </summary>
public sealed class KeystrokeController
{
    private SpatialKeyMap _map;
    private KeyVoiceSet _voices;
    private readonly IVoicePlayer _player;
    private readonly HashSet<int> _down = new();
    private readonly object _gate = new();

    public bool Enabled { get; set; } = true;

    /// <summary>Last key that produced a sound, for on-screen diagnostics. 0 if none yet.</summary>
    public int LastSoundedKey { get; private set; }

    public KeystrokeController(SpatialKeyMap map, KeyVoiceSet voices, IVoicePlayer player)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _voices = voices ?? throw new ArgumentNullException(nameof(voices));
        _player = player ?? throw new ArgumentNullException(nameof(player));
    }

    /// <summary>
    /// Handle a key-down. Returns true if a sound was played. The low-level hook delivers
    /// repeated downs while a key is held; we only sound the first transition.
    /// </summary>
    public bool OnKeyDown(int virtualKey)
    {
        lock (_gate)
        {
            if (!_down.Add(virtualKey)) return false; // already held — ignore auto-repeat
            if (!Enabled) return false;

            var buffer = _voices.Resolve(_map.Resolve(virtualKey));
            if (buffer is null) return false;

            LastSoundedKey = virtualKey;
            _player.Play(buffer);
            return true;
        }
    }

    public void OnKeyUp(int virtualKey)
    {
        lock (_gate) _down.Remove(virtualKey);
    }

    /// <summary>Swap the active preset (map + baked voices) live. Clears held keys so none stick.</summary>
    public void SetVoices(SpatialKeyMap map, KeyVoiceSet voices)
    {
        lock (_gate)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _voices = voices ?? throw new ArgumentNullException(nameof(voices));
            _down.Clear();
        }
    }

    /// <summary>Forget all held keys (e.g. on focus loss / hook reinstall) so none get stuck.</summary>
    public void ResetHeldKeys()
    {
        lock (_gate) _down.Clear();
    }
}
