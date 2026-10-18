using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;

namespace TaskbarPet;

/// <summary>Draws the CPU percentage as digits that fill the icon.</summary>
sealed class NumberRenderer : IIconRenderer
{
    public int FrameIntervalMs => 1000;

    public Bitmap Render(float cpuPercent, Size size)
    {
        string text = ((int)Math.Round(cpuPercent)).ToString();
        var bmp = new Bitmap(size.Width, size.Height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        // Build the digits as a path, then scale it to fill the image so
        // "7", "42" and "100" are all as large as possible.
        using var path = new GraphicsPath();
        using var family = new FontFamily("Segoe UI");
        path.AddString(text, family, (int)FontStyle.Bold, 100, Point.Empty, StringFormat.GenericTypographic);
        RectangleF bounds = path.GetBounds();

        float scaleY = size.Height / bounds.Height;
        float scaleX = Math.Min(scaleY, size.Width / bounds.Width);
        using var m = new Matrix();
        m.Translate((size.Width - bounds.Width * scaleX) / 2, (size.Height - bounds.Height * scaleY) / 2);
        m.Scale(scaleX, scaleY);
        m.Translate(-bounds.X, -bounds.Y);
        path.Transform(m);

        using var brush = new SolidBrush(IsTaskbarLight() ? Color.Black : Color.White);
        g.FillPath(brush, path);
        return bmp;
    }

    static bool IsTaskbarLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
    }
}
