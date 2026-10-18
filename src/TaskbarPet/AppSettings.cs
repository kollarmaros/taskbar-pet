using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskbarPet;

/// <summary>One CPU range (inclusive, whole percent) and the animation shown in it.</summary>
sealed class RangeRule
{
    public int From { get; set; }
    public int To { get; set; }
    /// <summary>File name without extension in the <c>animations</c> folder, e.g. "1_sleep".</summary>
    public string Animation { get; set; } = "";
}

/// <summary>User settings stored as JSON in %AppData%\TaskbarPet\settings.json.</summary>
sealed class AppSettings
{
    /// <summary>"animation" or "number".</summary>
    public string Display { get; set; } = "animation";

    /// <summary>Where the pet is shown: "tray", "taskbarButton" or "overlay".</summary>
    public string ShowIn { get; set; } = "overlay";

    /// <summary>Overlay only: gap between the pet and the notification area, in logical pixels.</summary>
    public int OverlayOffset { get; set; } = 8;

    /// <summary>How many CPU samples are taken per second (1–20).</summary>
    public int SamplesPerSecond { get; set; } = 5;

    /// <summary>The animation is picked from the CPU average over this many seconds,
    /// so short spikes don't switch it. 0 = use the latest sample.</summary>
    public double AverageSeconds { get; set; } = 2;

    /// <summary>true: newer samples count more in the average; false: all count equally.</summary>
    public bool WeightedAverage { get; set; } = true;

    /// <summary>Once an animation starts it is kept at least this many seconds. 0 = off.</summary>
    public double MinAnimationSeconds { get; set; } = 3;

    /// <summary>When on, the current range is only left once the average is more than
    /// <see cref="Stickiness"/> percentage points beyond its edges.</summary>
    public bool StickinessEnabled { get; set; } = true;

    /// <summary>Range stickiness in percentage points.</summary>
    public int Stickiness { get; set; } = 5;

    public List<RangeRule> Ranges { get; set; } =
    [
        new() { From = 0,  To = 15,  Animation = "1_sleep" },
        new() { From = 16, To = 30,  Animation = "2_wake" },
        new() { From = 31, To = 45,  Animation = "3_curious" },
        new() { From = 46, To = 60,  Animation = "4_rummage" },
        new() { From = 61, To = 75,  Animation = "5_run" },
        new() { From = 76, To = 90,  Animation = "6_frantic" },
        new() { From = 91, To = 100, Animation = "7_meltdown" },
    ];

    [JsonIgnore]
    public bool ShowNumber => Display.Equals("number", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public int SampleIntervalMs => 1000 / Math.Clamp(SamplesPerSecond, 1, 20);

    /// <summary>Number of samples covered by <see cref="AverageSeconds"/> (at least 1).</summary>
    [JsonIgnore]
    public int AverageWindow => Math.Max(1, (int)Math.Round(AverageSeconds * Math.Clamp(SamplesPerSecond, 1, 20)));

    public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this, Json), Json)!;

    /// <summary>Problems that make the ranges unusable (empty list = OK).</summary>
    public List<string> RangeErrors()
    {
        var errors = new List<string>();
        foreach (var r in Ranges)
        {
            if (r.From < 0 || r.To > 100 || r.From > r.To)
                errors.Add($"Range {r.From}–{r.To} % is invalid: values must be 0–100 and 'from' must not be above 'to'.");
            if (string.IsNullOrWhiteSpace(r.Animation))
                errors.Add($"Range {r.From}–{r.To} % has no animation.");
        }
        var sorted = Ranges.OrderBy(r => r.From).ToList();
        for (int i = 1; i < sorted.Count; i++)
            if (sorted[i].From <= sorted[i - 1].To)
                errors.Add($"Ranges {sorted[i - 1].From}–{sorted[i - 1].To} % and {sorted[i].From}–{sorted[i].To} % overlap.");
        return errors;
    }

    /// <summary>CPU values (whole percents) not covered by any range, as readable spans.</summary>
    public List<string> RangeGaps()
    {
        var gaps = new List<string>();
        int? start = null;
        for (int cpu = 0; cpu <= 101; cpu++)
        {
            bool covered = cpu <= 100 && Ranges.Any(r => cpu >= r.From && cpu <= r.To);
            if (!covered && cpu <= 100 && start == null)
                start = cpu;
            else if ((covered || cpu == 101) && start != null)
            {
                gaps.Add(start == cpu - 1 ? $"{start} %" : $"{start}–{cpu - 1} %");
                start = null;
            }
        }
        return gaps;
    }

    public static string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskbarPet", "settings.json");

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Loads settings, creating the file with defaults if it doesn't exist.
    /// A broken file is left untouched and defaults are used instead.</summary>
    public static AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = new AppSettings();
            defaults.Save();
            return defaults;
        }
        try
        {
            string text = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(text, Json) ?? new AppSettings();
            // Write back settings added in newer versions, so they show up in the file.
            if (HasMissingKeys(text))
                settings.Save();
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    static bool HasMissingKeys(string json)
    {
        using var file = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        using var defaults = JsonDocument.Parse(JsonSerializer.Serialize(new AppSettings(), Json));
        var present = file.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return defaults.RootElement.EnumerateObject().Any(p => !present.Contains(p.Name));
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }
}
