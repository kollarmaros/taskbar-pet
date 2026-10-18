namespace TaskbarPet;

/// <summary>
/// Picks the CPU range (and so the animation) for the averaged CPU value, with two
/// brakes against flickering between animations:
/// <list type="bullet">
/// <item>minimum time: once a range is chosen it is kept for at least <see cref="MinHoldSeconds"/>;</item>
/// <item>stickiness: the current range is only left when the value is more than
/// <see cref="Stickiness"/> points beyond its edges.</item>
/// </list>
/// </summary>
sealed class RangeSelector
{
    readonly List<RangeRule> _ranges;
    RangeRule? _current;
    bool _hasCurrent;
    double _lastSwitch;

    public double MinHoldSeconds { get; init; }
    public double Stickiness { get; init; }

    public RangeSelector(IEnumerable<RangeRule> ranges) => _ranges = ranges.ToList();

    /// <param name="cpu">Averaged CPU %.</param>
    /// <param name="nowSeconds">Monotonic time in seconds.</param>
    /// <returns>The chosen range, or null when the value falls in a gap.</returns>
    public RangeRule? Update(float cpu, double nowSeconds)
    {
        int rounded = (int)Math.Round(cpu);
        var candidate = _ranges.FirstOrDefault(r => rounded >= r.From && rounded <= r.To);

        if (!_hasCurrent)
            return Switch(candidate, nowSeconds);
        if (candidate == _current)
            return _current;
        if (nowSeconds - _lastSwitch < MinHoldSeconds)
            return _current;
        if (_current != null && Stickiness > 0
            && cpu >= _current.From - 0.5 - Stickiness
            && cpu < _current.To + 0.5 + Stickiness)
            return _current;
        return Switch(candidate, nowSeconds);
    }

    RangeRule? Switch(RangeRule? range, double now)
    {
        _current = range;
        _hasCurrent = true;
        _lastSwitch = now;
        return range;
    }
}
