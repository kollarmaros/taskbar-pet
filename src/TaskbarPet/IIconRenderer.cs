namespace TaskbarPet;

/// <summary>
/// Produces the pet's image: <see cref="NumberRenderer"/> draws the CPU number,
/// <see cref="AnimationRenderer"/> plays the GIF for the current CPU range.
/// Where the image goes is up to the <see cref="IPetSurface"/>.
/// </summary>
interface IIconRenderer
{
    /// <summary>Milliseconds until the next <see cref="Render"/> call. Read after each
    /// Render, since animations can give every frame its own duration.</summary>
    int FrameIntervalMs { get; }

    /// <param name="cpuPercent">Latest CPU usage, 0–100.</param>
    /// <param name="size">Image size in pixels, from <see cref="IPetSurface.FrameSize"/>.</param>
    Bitmap Render(float cpuPercent, Size size);
}
