using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly KeepAwake _keepAwake;
    private readonly OverlayManager _overlay;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _autoOffMenu;
    private readonly Icon _icon;
    private readonly bool _ownsIcon;
    private bool _disposed;
    private Settings _settings = Settings.Load();

    // 트레이 "자동 종료" 빠른 시작 프리셋 (분). 타이머 없는 시작은 "덮개 시작"이 담당.
    private static readonly (string Label, int Minutes)[] AutoOffPresets =
    {
        ("30분", 30), ("1시간", 60), ("2시간", 120), ("4시간", 240)
    };

    public TrayAppContext()
    {
        _keepAwake = new KeepAwake(
            new Win32InputSender(),
            flags => NativeMethods.SetThreadExecutionState(flags));
        _overlay = new OverlayManager(OnDismissed);

        StartupRegistration.Apply(_settings.RunAtStartup, Application.ExecutablePath);

        var settingsItem = new ToolStripMenuItem("설정...", null, (_, _) => OpenSettings());
        _startItem = new ToolStripMenuItem("덮개 시작", null, (_, _) => StartCover());

        _autoOffMenu = new ToolStripMenuItem("자동 종료");
        foreach (var preset in AutoOffPresets)
        {
            int minutes = preset.Minutes;
            _autoOffMenu.DropDownItems.Add(
                new ToolStripMenuItem(preset.Label, null, (_, _) => StartCoverWith(minutes)));
        }

        var exitItem = new ToolStripMenuItem("종료", null, (_, _) => ExitApp());

        var menu = new ContextMenuStrip();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startItem);
        menu.Items.Add(_autoOffMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(iconPath)) { _icon = new Icon(iconPath); _ownsIcon = true; }
        else { _icon = SystemIcons.Application; _ownsIcon = false; }
        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = "MouseMover — 절전방지/화면가리기",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => StartCover();
    }

    // "덮개 시작" — 저장된 설정 그대로 사용(타이머 없음).
    private void StartCover() => StartCover(_settings);

    // 트레이 "자동 종료 ▸ N" — 원샷: 저장 설정 복제 + AutoOffMinutes만 덮어써 시작(저장 안 함).
    private void StartCoverWith(int autoOffMinutes)
    {
        var effective = _settings.Clone();
        effective.AutoOffMinutes = autoOffMinutes;
        StartCover(effective);
    }

    private void StartCover(Settings effective)
    {
        if (_overlay.IsActive) return;
        _keepAwake.JiggleSeconds = effective.JiggleSeconds;
        _keepAwake.Start();
        _overlay.Start(effective);
        _startItem.Enabled = false;
        _autoOffMenu.Enabled = false;
    }

    private void OnDismissed()
    {
        _keepAwake.Stop();
        _startItem.Enabled = true;
        _autoOffMenu.Enabled = true;
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _settings = form.Result;
            _settings.Save();
            StartupRegistration.Apply(_settings.RunAtStartup, Application.ExecutablePath);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _overlay.Stop();
            _keepAwake.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            if (_ownsIcon) _icon.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ExitApp()
    {
        Dispose();      // ApplicationContext.Dispose() -> Dispose(true), cleans up once via the guard
        ExitThread();
    }
}
