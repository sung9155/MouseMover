using System.Drawing;
using MouseMover;
using Xunit;

public class ActiveIconFramesTests
{
    [Fact]
    public void Produces_four_pulse_frames_from_base_icon()
    {
        using var frames = new ActiveIconFrames(SystemIcons.Application);
        Assert.Equal(4, frames.Frames.Count);
        Assert.All(frames.Frames, f => Assert.NotNull(f));
    }

    [Fact]
    public void Dispose_clears_frames()
    {
        var frames = new ActiveIconFrames(SystemIcons.Application);
        frames.Dispose();
        Assert.Empty(frames.Frames);
    }
}
