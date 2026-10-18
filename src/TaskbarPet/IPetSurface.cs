namespace TaskbarPet;

/// <summary>
/// Where the pet is displayed: tray icon, taskbar button or taskbar overlay.
/// The app renders a frame at <see cref="FrameSize"/> and hands it to <see cref="ShowFrame"/>.
/// </summary>
interface IPetSurface : IDisposable
{
    /// <summary>Size in pixels the frames should be rendered at. May change (e.g. taskbar moved).</summary>
    Size FrameSize { get; }

    /// <summary>Displays the frame. The surface copies what it needs; the caller disposes the bitmap.</summary>
    void ShowFrame(Bitmap frame);

    void SetTooltip(string text);

    /// <summary>The user asked for the settings window (e.g. double-click).</summary>
    event Action? SettingsRequested;

    /// <summary>The user closed the surface from Windows UI (taskbar button "Close window").</summary>
    event Action? ExitRequested;
}
