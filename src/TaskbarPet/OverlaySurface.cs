namespace TaskbarPet;

/// <summary>
/// Pet drawn in a borderless always-on-top window placed on the taskbar, just left of the
/// notification area. Larger than an icon (sized from the taskbar height). Uses a layered
/// window, so transparent GIF pixels show the taskbar through.
/// Re-checks the taskbar position every second (also handles Explorer restarts) and hides
/// while a fullscreen app is in front.
/// </summary>
sealed class OverlaySurface : Form, IPetSurface
{
    const float WidthToHeight = 1f; // animation frames are square (tools/sheet_to_gif.py --cell)

    readonly ContextMenuStrip _menu;
    readonly int _offset;
    readonly ToolTip _toolTip = new();
    readonly System.Windows.Forms.Timer _placementTimer = new() { Interval = 1000 };
    Rectangle _bounds;
    bool _hiddenForFullscreen;
    string _tooltip = "";

    public event Action? SettingsRequested;
    public event Action? ExitRequested { add { } remove { } }

    /// <param name="offsetFromTray">Logical pixels between the pet and the notification area.</param>
    public OverlaySurface(ContextMenuStrip menu, int offsetFromTray)
    {
        _menu = menu;
        _offset = offsetFromTray;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;

        MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) _menu.Show(Cursor.Position); };
        MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) SettingsRequested?.Invoke(); };

        Show(); // a layered window stays invisible until the first frame is drawn
        _placementTimer.Tick += (_, _) => UpdatePlacement();
        UpdatePlacement();
        _placementTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style = Native.WS_POPUP;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public Size FrameSize => _bounds.Size;

    void UpdatePlacement()
    {
        bool fullscreen = Native.IsFullscreenAppActive();
        if (fullscreen != _hiddenForFullscreen)
        {
            _hiddenForFullscreen = fullscreen;
            Visible = !fullscreen;
        }

        IntPtr taskbar = Native.FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !Native.GetWindowRect(taskbar, out var tb))
            return; // Explorer restarting: keep the last position and try again next tick
        Rectangle bar = tb.ToRectangle();

        // Right edge = left edge of the notification area (tray icons + clock), if found.
        int right = bar.Right;
        IntPtr tray = Native.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && Native.GetWindowRect(tray, out var tr))
            right = tr.Left;

        int margin = LogicalToDeviceUnits(4);
        int h = Math.Max(16, bar.Height - 2 * margin);
        int w = (int)(h * WidthToHeight);
        int x = right - w - LogicalToDeviceUnits(_offset);
        int y = bar.Top + (bar.Height - h) / 2;
        _bounds = new Rectangle(x, y, w, h);
    }

    public void ShowFrame(Bitmap frame)
    {
        if (_hiddenForFullscreen || !IsHandleCreated)
            return;

        // The taskbar is topmost too and can cover us after it is clicked; re-assert.
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);

        // Layered windows only receive clicks on non-transparent pixels. An almost invisible
        // backdrop (alpha 1) makes the whole rectangle clickable, e.g. around number digits.
        using var canvas = new Bitmap(frame.Width, frame.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.FromArgb(1, 0, 0, 0));
            g.DrawImageUnscaled(frame, 0, 0);
        }

        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBitmap = canvas.GetHbitmap(Color.FromArgb(0));
        IntPtr old = Native.SelectObject(memDc, hBitmap);
        try
        {
            var size = new Native.SIZE { Width = canvas.Width, Height = canvas.Height };
            var src = new Native.POINT();
            var dst = new Native.POINT { X = _bounds.X, Y = _bounds.Y };
            var blend = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER,
                SourceConstantAlpha = 255,
                AlphaFormat = Native.AC_SRC_ALPHA,
            };
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
        }
        finally
        {
            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBitmap);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public void SetTooltip(string text)
    {
        if (text == _tooltip)
            return; // re-setting the same text makes a visible tooltip flicker
        _tooltip = text;
        _toolTip.SetToolTip(this, text);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _placementTimer.Dispose();
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
