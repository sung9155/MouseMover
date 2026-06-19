using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly KeepAwake _keepAwake;
    private readonly OverlayManager _overlay;
    private readonly ToolStripMenuItem _startItem;
    private readonly Icon _icon;
    private readonly bool _ownsIcon;
    private bool _disposed;
    private Settings _settings = Settings.Load();

    public TrayAppContext()
    {
        _keepAwake = new KeepAwake(
            new Win32InputSender(),
            flags => NativeMethods.SetThreadExecutionState(flags));
        _overlay = new OverlayManager(OnDismissed);

        StartupRegistration.Apply(_settings.RunAtStartup, Application.ExecutablePath);

        var settingsItem = new ToolStripMenuItem("설정...", null, (_, _) => OpenSettings());
        _startItem = new ToolStripMenuItem("덮개 시작", null, (_, _) => StartCover());
        var exitItem = new ToolStripMenuItem("종료", null, (_, _) => ExitApp());

        var menu = new ContextMenuStrip();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startItem);
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

    private void StartCover()
    {
        if (_overlay.IsActive) return;
        _keepAwake.JiggleSeconds = _settings.JiggleSeconds;
        _keepAwake.Start();
        _overlay.Start(_settings);
        _startItem.Enabled = false;
    }

    private void OnDismissed()
    {
        _keepAwake.Stop();
        _startItem.Enabled = true;
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
