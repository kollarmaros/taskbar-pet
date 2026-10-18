using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TaskbarPet;

/// <summary>
/// An animated GIF scaled down to the pet's size. Frames are fitted into that rectangle
/// (bottom-centred) and its empty area is filled with the GIF's own background colour.
/// </summary>
sealed class GifAnimation : IDisposable
{
    const int PropertyTagFrameDelay = 0x5100;

    public Size Size { get; }
    public IReadOnlyList<Bitmap> Frames { get; }
    public IReadOnlyList<int> DelaysMs { get; }

    GifAnimation(Size size, List<Bitmap> frames, List<int> delays)
    {
        Size = size;
        Frames = frames;
        DelaysMs = delays;
    }

    /// <param name="gifData">The GIF file's bytes (see <see cref="AnimationLibrary.Load"/>).</param>
    public static GifAnimation Load(byte[] gifData, Size size)
    {
        using var stream = new MemoryStream(gifData);
        using var gif = Image.FromStream(stream);

        int count = gif.GetFrameCount(FrameDimension.Time);
        int[] delays = ReadDelays(gif, count);
        var frames = new List<Bitmap>(count);

        float scale = Math.Min((float)size.Width / gif.Width, (float)size.Height / gif.Height);
        int w = Math.Max(1, (int)Math.Round(gif.Width * scale));
        int h = Math.Max(1, (int)Math.Round(gif.Height * scale));
        var dest = new Rectangle((size.Width - w) / 2, size.Height - h, w, h);

        for (int i = 0; i < count; i++)
        {
            gif.SelectActiveFrame(FrameDimension.Time, i);
            using var full = new Bitmap(gif);
            Color background = full.GetPixel(0, 0);

            var frame = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(frame))
            {
                g.Clear(background);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                // Clamp edge sampling so the border doesn't blend with transparent black.
                using var attrs = new ImageAttributes();
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(full, dest, 0, 0, full.Width, full.Height, GraphicsUnit.Pixel, attrs);
            }
            frames.Add(frame);
        }
        return new GifAnimation(size, frames, delays.ToList());
    }

    static int[] ReadDelays(Image gif, int count)
    {
        var delays = Enumerable.Repeat(100, count).ToArray();
        if (!gif.PropertyIdList.Contains(PropertyTagFrameDelay))
            return delays;

        // One 32-bit value per frame, in hundredths of a second.
        byte[] raw = gif.GetPropertyItem(PropertyTagFrameDelay)!.Value!;
        for (int i = 0; i < count && i * 4 + 3 < raw.Length; i++)
        {
            int cs = BitConverter.ToInt32(raw, i * 4);
            // Browsers treat very small delays as "use a default"; do the same.
            delays[i] = cs < 2 ? 100 : cs * 10;
        }
        return delays;
    }

    public void Dispose()
    {
        foreach (var f in Frames)
            f.Dispose();
    }
}
