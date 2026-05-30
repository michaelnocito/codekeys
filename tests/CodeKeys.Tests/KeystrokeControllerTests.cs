using CodeKeys.Core.Audio;
using CodeKeys.Core.Input;
using CodeKeys.Core.Music;
using Xunit;

namespace CodeKeys.Tests;

public class KeystrokeControllerTests
{
    private sealed class FakePlayer : IVoicePlayer
    {
        public int PlayCount { get; private set; }
        public SampleBuffer? Last { get; private set; }
        public void Play(SampleBuffer buffer) { PlayCount++; Last = buffer; }
    }

    private static (KeystrokeController c, FakePlayer p) Build()
    {
        var map = new SpatialKeyMap(Scale.MajorPentatonic, NoteUtil.ParseNoteName("C3"));
        var voices = KeyVoiceSet.BakeSynth(map, 44100, Waveform.WarmPad, Envelope.Pluck);
        var player = new FakePlayer();
        return (new KeystrokeController(map, voices, player), player);
    }

    [Fact]
    public void Fresh_KeyDown_Plays_Once()
    {
        var (c, p) = Build();
        Assert.True(c.OnKeyDown('A'));
        Assert.Equal(1, p.PlayCount);
    }

    [Fact]
    public void Held_Key_Does_Not_Machine_Gun()
    {
        var (c, p) = Build();
        c.OnKeyDown('A');
        c.OnKeyDown('A'); // OS auto-repeat
        c.OnKeyDown('A');
        Assert.Equal(1, p.PlayCount);
    }

    [Fact]
    public void Replay_After_KeyUp()
    {
        var (c, p) = Build();
        c.OnKeyDown('A');
        c.OnKeyUp('A');
        c.OnKeyDown('A');
        Assert.Equal(2, p.PlayCount);
    }

    [Fact]
    public void Disabled_Plays_Nothing()
    {
        var (c, p) = Build();
        c.Enabled = false;
        Assert.False(c.OnKeyDown('A'));
        Assert.Equal(0, p.PlayCount);
    }

    [Fact]
    public void Pure_Modifier_Is_Silent()
    {
        var (c, p) = Build();
        Assert.False(c.OnKeyDown(VirtualKey.ShiftL));
        Assert.Equal(0, p.PlayCount);
    }

    [Fact]
    public void Rhythm_Keys_Play()
    {
        var (c, p) = Build();
        Assert.True(c.OnKeyDown(VirtualKey.Space));
        Assert.True(c.OnKeyDown(VirtualKey.Enter));
        Assert.True(c.OnKeyDown(VirtualKey.Back));
        Assert.Equal(3, p.PlayCount);
    }

    [Fact]
    public void LastSoundedKey_Tracks_Audible_Keys_Only()
    {
        var (c, _) = Build();
        c.OnKeyDown('A');
        c.OnKeyDown(VirtualKey.ControlL); // silent — should not overwrite
        Assert.Equal('A', c.LastSoundedKey);
    }

    [Fact]
    public void ResetHeldKeys_Allows_Immediate_Replay()
    {
        var (c, p) = Build();
        c.OnKeyDown('A');
        c.ResetHeldKeys();
        c.OnKeyDown('A');
        Assert.Equal(2, p.PlayCount);
    }
}
