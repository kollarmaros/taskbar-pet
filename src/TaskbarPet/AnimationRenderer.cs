using System.Runtime.InteropServices;

namespace TaskbarPet;

/// <summary>
/// Plays the GIF assigned to the CPU range chosen by <see cref="RangeSelector"/>
/// (which applies the minimum animation time and range stickiness).
/// When the range changes, the new animation continues at the same frame number
/// instead of restarting.
/// </summary>
sealed class AnimationRenderer : IIconRenderer, IDisposable
{
    readonly AppSettings _settings;
    readonly Dictionary<string, GifAnimation?> _cache = new(StringComparer.OrdinalIgnoreCase);
    readonly NumberRenderer _fallback = new();
    readonly RangeSelector _selector;
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    Size _size;
    int _frame;

    public AnimationRenderer(AppSettings settings)
    {
        _settings = settings;
        _selector = new RangeSelector(settings.Ranges)
        {
            MinHoldSeconds = Math.Max(0, settings.MinAnimationSeconds),
            Stickiness = settings.StickinessEnabled ? Math.Max(0, settings.Stickiness) : 0,
        };
    }

    public int FrameIntervalMs { get; private set; } = 1000;

    /// <summary>Animation name used for the last frame, or null when falling back to the number.</summary>
    public string? CurrentAnimation { get; private set; }

    public Bitmap Render(float cpuPercent, Size size)
    {
        if (size != _size)
        {
            // Size changed (display scaling, surface switch, taskbar moved): rescale from the original GIFs.
            ClearCache();
            _size = size;
        }

        string? name = _selector.Update(cpuPercent, _clock.Elapsed.TotalSeconds)?.Animation;
        GifAnimation? anim = name == null ? null : Get(name);
        CurrentAnimation = anim == null ? null : name;
        if (anim == null)
        {
            FrameIntervalMs = _fallback.FrameIntervalMs;
            return _fallback.Render(cpuPercent, size);
        }

        int i = _frame % anim.Frames.Count;
        _frame = i + 1;
        FrameIntervalMs = anim.DelaysMs[i];
        return new Bitmap(anim.Frames[i]);
    }

    GifAnimation? Get(string name)
    {
        if (!_cache.TryGetValue(name, out var anim))
        {
            try
            {
                anim = AnimationLibrary.Load(name) is byte[] data ? GifAnimation.Load(data, _size) : null;
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or IOException)
            {
                anim = null; // unreadable GIF: show the number for this range instead
            }
            _cache[name] = anim;
        }
        return anim;
    }

    void ClearCache()
    {
        foreach (var a in _cache.Values)
            a?.Dispose();
        _cache.Clear();
    }

    public void Dispose() => ClearCache();
}
