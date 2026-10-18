namespace TaskbarPet;

/// <summary>
/// Running average over the last <see cref="WindowSize"/> CPU samples.
/// Weighted mode gives the newest sample weight N, the one before N-1, … the oldest 1,
/// so the result follows recent changes faster while still ignoring short spikes.
/// </summary>
sealed class CpuAverager
{
    readonly Queue<float> _samples = new();

    public int WindowSize { get; set; } = 1;
    public bool Weighted { get; set; } = true;

    public float Add(float sample)
    {
        _samples.Enqueue(sample);
        while (_samples.Count > Math.Max(1, WindowSize))
            _samples.Dequeue();
        return Value;
    }

    public float Value
    {
        get
        {
            if (_samples.Count == 0)
                return 0;
            if (!Weighted)
                return _samples.Average();

            float sum = 0, weights = 0;
            int w = 1;
            foreach (float s in _samples)   // oldest first
            {
                sum += s * w;
                weights += w;
                w++;
            }
            return sum / weights;
        }
    }
}
