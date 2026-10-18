using System.Reflection;

namespace TaskbarPet;

/// <summary>
/// Where animation GIFs come from: the ones built into the exe, plus an optional
/// <c>animations</c> folder next to it. A GIF in the folder with the same name as a
/// built-in one replaces it; other GIFs in the folder are added to the list.
/// </summary>
static class AnimationLibrary
{
    const string ResourcePrefix = "animations/";

    /// <summary>Optional folder for extra or replacement GIFs (doesn't have to exist).</summary>
    public static string Folder { get; } = Path.Combine(AppContext.BaseDirectory, "animations");

    static readonly Assembly Assembly = typeof(AnimationLibrary).Assembly;

    static IEnumerable<string> BuiltInNames() =>
        Assembly.GetManifestResourceNames()
            .Where(r => r.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)
                        && r.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            .Select(r => Path.GetFileNameWithoutExtension(r[ResourcePrefix.Length..]));

    static IEnumerable<string> FolderNames() =>
        Directory.Exists(Folder)
            ? Directory.GetFiles(Folder, "*.gif").Select(f => Path.GetFileNameWithoutExtension(f)!)
            : [];

    /// <summary>All available animation names, sorted.</summary>
    public static List<string> Names() =>
        BuiltInNames().Concat(FolderNames())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The GIF bytes for an animation, or null if it doesn't exist.</summary>
    public static byte[]? Load(string name)
    {
        string file = Path.Combine(Folder, name + ".gif");
        if (File.Exists(file))
            return File.ReadAllBytes(file);

        using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + name + ".gif");
        if (stream == null)
            return null;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
