namespace MouseMover;

public sealed class KeepAwake : IDisposable
{
    private readonly IInputSender _sender;
    private readonly Action<uint> _setExecutionState;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Func<TimeSpan> _idleTime;

    public bool IsRunning { get; private set; }

    public int JiggleSeconds
    {
        get => _timer.Interval / 1000;
        set => _timer.Interval = Math.Max(1, value) * 1000;
    }

    public KeepAwake(IInputSender sender, Action<uint> setExecutionState, int jiggleSeconds = 45, Func<TimeSpan>? idleTime = null)
    {
        _sender = sender;
        _setExecutionState = setExecutionState;
        _idleTime = idleTime ?? Win32IdleTime.Get;
        _timer = new System.Windows.Forms.Timer { Interval = jiggleSeconds * 1000 };
        _timer.Tick += (_, _) => Tick();
    }

    // 유휴 시간이 지글 주기 이상일 때만 지글 (사용자가 활동 중이면 건너뜀)
    private void Tick()
    {
        if (_idleTime() >= TimeSpan.FromSeconds(JiggleSeconds))
            _sender.Jiggle();
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

    // 테스트용: 타이머 콜백과 동일한 유휴 게이트를 거쳐 한 번 처리
    public void TickForTest() => Tick();

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
