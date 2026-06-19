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
    private bool _disposed;

    public TrayAppContext()
    {
        _keepAwake = new KeepAwake(
            new Win32InputSender(),
            flags => NativeMethods.SetThreadExecutionState(flags));
        _overlay = new OverlayManager(OnDismissed);

        _startItem = new ToolStripMenuItem("덮개 시작", null, (_, _) => StartCover());
        var exitItem = new ToolStripMenuItem("종료", null, (_, _) => ExitApp());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_startItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = LoadIcon();
        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = "MouseMover — 절전방지/화면가리기",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => StartCover();
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "app.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    private void StartCover()
    {
        if (_overlay.IsActive) return;
        _keepAwake.Start();
        _overlay.Start();
        _startItem.Enabled = false;
    }

    private void OnDismissed()
    {
        _keepAwake.Stop();
        _startItem.Enabled = true;
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
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ExitApp()
    {
        Dispose();      // ApplicationContext.Dispose() -> Dispose(true), cleans up once via the guard
        ExitThread();
    }
}
