using MouseMover;
using Xunit;

public class KeepAwakeTests
{
    private sealed class FakeSender : IInputSender
    {
        public int JiggleCount;
        public void Jiggle() => JiggleCount++;
    }

    [Fact]
    public void Start_sets_execution_state_with_required_flags()
    {
        var sender = new FakeSender();
        uint captured = 0;
        var ka = new KeepAwake(sender, flags => captured = flags);

        ka.Start();

        Assert.True((captured & NativeMethods.ES_CONTINUOUS) != 0);
        Assert.True((captured & NativeMethods.ES_SYSTEM_REQUIRED) != 0);
        Assert.True((captured & NativeMethods.ES_DISPLAY_REQUIRED) != 0);
        Assert.True(ka.IsRunning);
    }

    [Fact]
    public void Stop_restores_continuous_only()
    {
        var sender = new FakeSender();
        uint captured = 0;
        var ka = new KeepAwake(sender, flags => captured = flags);
        ka.Start();

        ka.Stop();

        Assert.Equal(NativeMethods.ES_CONTINUOUS, captured);
        Assert.False(ka.IsRunning);
    }

    [Fact]
    public void Tick_jiggles_once()
    {
        var sender = new FakeSender();
        var ka = new KeepAwake(sender, _ => { });
        ka.Start();

        ka.TickForTest();

        Assert.Equal(1, sender.JiggleCount);
    }

    [Fact]
    public void Stop_is_idempotent()
    {
        var sender = new FakeSender();
        var ka = new KeepAwake(sender, _ => { });
        ka.Start();
        ka.Stop();
        ka.Stop(); // 두 번째 호출 예외 없어야 함
        Assert.False(ka.IsRunning);
    }

    [Fact]
    public void JiggleSeconds_get_set_round_trips()
    {
        var ka = new KeepAwake(new FakeSender(), _ => { });
        ka.JiggleSeconds = 30;
        Assert.Equal(30, ka.JiggleSeconds);
    }

    [Fact]
    public void JiggleSeconds_default_is_45()
    {
        var ka = new KeepAwake(new FakeSender(), _ => { });
        Assert.Equal(45, ka.JiggleSeconds);
    }
}
