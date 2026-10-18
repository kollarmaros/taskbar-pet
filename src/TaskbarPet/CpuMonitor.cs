using System.Runtime.InteropServices;

namespace TaskbarPet;

/// <summary>
/// Measures total CPU usage across all cores using GetSystemTimes.
/// Each call to <see cref="Sample"/> returns usage since the previous call.
/// </summary>
sealed class CpuMonitor
{
    [StructLayout(LayoutKind.Sequential)]
    struct FILETIME
    {
        public uint Low;
        public uint High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    // Readings over a very short gap are mostly noise (e.g. two timer ticks bunched up
    // after the app was starved on a fully loaded CPU); keep the previous value instead.
    const double MinGapMs = 50;

    readonly System.Diagnostics.Stopwatch _sinceLast = System.Diagnostics.Stopwatch.StartNew();
    ulong _prevIdle, _prevTotal;
    float _last;

    public CpuMonitor() => Read(out _prevIdle, out _prevTotal);

    /// <returns>CPU usage in percent, 0–100.</returns>
    public float Sample()
    {
        if (_sinceLast.Elapsed.TotalMilliseconds < MinGapMs)
            return _last;

        Read(out ulong idle, out ulong total);
        ulong idleDelta = idle - _prevIdle;
        ulong totalDelta = total - _prevTotal;
        if (totalDelta == 0)
            return _last; // counters not updated yet; measure over a longer gap next time

        _prevIdle = idle;
        _prevTotal = total;
        _sinceLast.Restart();
        _last = Math.Clamp(100f * (totalDelta - idleDelta) / totalDelta, 0, 100);
        return _last;
    }

    static void Read(out ulong idle, out ulong total)
    {
        GetSystemTimes(out var i, out var k, out var u);
        idle = i.Value;
        // Kernel time already includes idle time.
        total = k.Value + u.Value;
    }
}
