namespace MouseMover;

public sealed class KeepAwake : IDisposable
{
    private readonly IInputSender _sender;
    private readonly Action<uint> _setExecutionState;
    private readonly System.Windows.Forms.Timer _timer;

    public bool IsRunning { get; private set; }

    public int JiggleSeconds
    {
        get => _timer.Interval / 1000;
        set => _timer.Interval = Math.Max(1, value) * 1000;
    }

    public KeepAwake(IInputSender sender, Action<uint> setExecutionState, int jiggleSeconds = 45)
    {
        _sender = sender;
        _setExecutionState = setExecutionState;
        _timer = new System.Windows.Forms.Timer { Interval = jiggleSeconds * 1000 };
        _timer.Tick += (_, _) => _sender.Jiggle();
    }

    public void Start()
    {
        if (IsRunning) return;
        _setExecutionState(
            NativeMethods.ES_CONTINUOUS |
            NativeMethods.ES_SYSTEM_REQUIRED |
            NativeMethods.ES_DISPLAY_REQUIRED);
        _timer.Start();
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _timer.Stop();
        _setExecutionState(NativeMethods.ES_CONTINUOUS);
        IsRunning = false;
    }

    // 테스트용: 타이머 콜백과 동일하게 한 번 지글
    public void TickForTest() => _sender.Jiggle();

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
