using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class OverlayManager
{
    private readonly Action _onDismiss;
    private readonly Action _lock;
    private readonly List<OverlayForm> _forms = new();
    private readonly System.Windows.Forms.Timer _elapsedTimer;
    private DateTime _startUtc;
    private Settings _settings = new();
    private DateTime _startLocal;

    public bool IsActive { get; private set; }

    public OverlayManager(Action onDismiss, Action lockWorkstation)
    {
        _onDismiss = onDismiss;
        _lock = lockWorkstation;
        _elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _elapsedTimer.Tick += (_, _) => RefreshElapsed();
    }

    public void Start(Settings settings)
    {
        if (IsActive) return;
        _startUtc = DateTime.UtcNow;
        _settings = settings;
        _startLocal = DateTime.Now;

        foreach (var screen in Screen.AllScreens)
        {
            var form = new OverlayForm(screen.Bounds, settings, _startLocal, () => DismissAll(false));
            _forms.Add(form);
            form.Show();
        }
        // 첫 폼에 포커스 → 키 입력 수신
        if (_forms.Count > 0) _forms[0].Activate();

        _elapsedTimer.Start();
        IsActive = true;
    }

    public void Stop()
    {
        if (!IsActive) return;
        _elapsedTimer.Stop();
        foreach (var form in _forms)
        {
            form.Close();
        }
        _forms.Clear();
        IsActive = false;
    }

    private void RefreshElapsed()
    {
        var elapsed = DateTime.UtcNow - _startUtc;
        foreach (var form in _forms) form.UpdateElapsed(elapsed);

        if (StopPolicy.ShouldAutoStop(_settings, _startLocal, DateTime.Now))
        {
            DismissAll(_settings.LockOnAutoStop);
        }
    }

    // lockAfter: 자동종료 + 설정 켜짐일 때만 true. 수동 해제(키/클릭)는 항상 false.
    private void DismissAll(bool lockAfter)
    {
        if (!IsActive) return;
        Stop();
        _onDismiss();
        if (lockAfter) _lock();   // 폼 닫힌 뒤 잠금 → 잠금 화면이 빈 바탕 위에 표시
    }
}
