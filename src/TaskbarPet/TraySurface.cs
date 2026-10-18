namespace TaskbarPet;

/// <summary>Pet as a notification-area (tray) icon. Windows fixes its size: 16 px × display scaling.</summary>
sealed class TraySurface : IPetSurface
{
    readonly NotifyIcon _tray;
    Icon? _currentIcon;

    public event Action? SettingsRequested;
    public event Action? ExitRequested { add { } remove { } }

    public TraySurface(ContextMenuStrip menu)
    {
        _tray = new NotifyIcon { ContextMenuStrip = menu, Text = "Taskbar Pet" };
        _tray.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) SettingsRequested?.Invoke(); };
    }

    public Size FrameSize => SystemInformation.SmallIconSize;

    public void ShowFrame(Bitmap frame)
    {
        var icon = Icon.FromHandle(frame.GetHicon());
        _tray.Icon = icon;
        _tray.Visible = true;

        // Icon.FromHandle doesn't own the handle, so free the previous one ourselves.
        if (_currentIcon != null)
        {
            Native.DestroyIcon(_currentIcon.Handle);
            _currentIcon.Dispose();
        }
        _currentIcon = icon;
    }

    public void SetTooltip(string text) =>
        // NotifyIcon tooltips are limited to 127 characters.
        _tray.Text = text.Length > 127 ? text[..127] : text;

    public void Dispose()
    {
        _tray.Visible = false;
        _tray.Dispose();
        if (_currentIcon != null)
        {
            Native.DestroyIcon(_currentIcon.Handle);
            _currentIcon.Dispose();
        }
    }
}
