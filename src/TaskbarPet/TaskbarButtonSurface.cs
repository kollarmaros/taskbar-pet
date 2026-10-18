namespace TaskbarPet;

/// <summary>
/// Pet as the icon of its own taskbar button (24 px × display scaling, larger than the tray).
/// The window behind it stays minimized; clicking the button opens the pet's menu instead of
/// restoring it. The button's Windows menu ("Close window") exits the app.
/// </summary>
sealed class TaskbarButtonSurface : Form, IPetSurface
{
    readonly ContextMenuStrip _menu;
    IntPtr _currentIcon;
    bool _closingFromApp;

    public event Action? SettingsRequested { add { } remove { } }
    public event Action? ExitRequested;

    public TaskbarButtonSurface(ContextMenuStrip menu)
    {
        _menu = menu;
        Text = "Taskbar Pet";
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
        WindowState = FormWindowState.Minimized;
        Show();
    }

    public Size FrameSize => new(LogicalToDeviceUnits(24), LogicalToDeviceUnits(24));

    public void ShowFrame(Bitmap frame)
    {
        IntPtr icon = frame.GetHicon();
        Native.SendMessage(Handle, Native.WM_SETICON, Native.ICON_BIG, icon);
        Native.SendMessage(Handle, Native.WM_SETICON, Native.ICON_SMALL, icon);
        if (_currentIcon != IntPtr.Zero)
            Native.DestroyIcon(_currentIcon);
        _currentIcon = icon;
    }

    public void SetTooltip(string text) => Text = text;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_SYSCOMMAND)
        {
            int cmd = (int)m.WParam & 0xFFF0;
            if (cmd is Native.SC_RESTORE or Native.SC_MAXIMIZE)
            {
                // Taskbar button clicked: show the pet's menu and stay minimized.
                _menu.Show(Cursor.Position);
                return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_closingFromApp && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            ExitRequested?.Invoke();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closingFromApp = true;
            if (_currentIcon != IntPtr.Zero)
                Native.DestroyIcon(_currentIcon);
            _currentIcon = IntPtr.Zero;
        }
        base.Dispose(disposing);
    }
}
