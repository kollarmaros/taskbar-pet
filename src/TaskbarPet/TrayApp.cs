namespace TaskbarPet;

/// <summary>
/// Windowless app: samples the CPU, renders frames and hands them to the selected
/// surface (tray icon, taskbar button or taskbar overlay). Owns the shared menu.
/// </summary>
sealed class TrayApp : ApplicationContext
{
    readonly CpuMonitor _cpu = new();
    readonly NumberRenderer _numberRenderer = new();
    readonly ContextMenuStrip _menu = new();
    readonly System.Windows.Forms.Timer _cpuTimer = new();
    readonly System.Windows.Forms.Timer _frameTimer = new();
    readonly CpuAverager _averager = new();
    AppSettings _settings = AppSettings.Load();
    AnimationRenderer _animationRenderer;
    IPetSurface _surface;
    float _cpuPercent;   // latest sample
    float _cpuAverage;   // average over AppSettings.AverageSeconds, drives the animation
    SettingsForm? _settingsForm;

    IIconRenderer Renderer => _settings.ShowNumber ? _numberRenderer : _animationRenderer;

    public TrayApp()
    {
        _animationRenderer = new AnimationRenderer(_settings);

        var settingsItem = new ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings());
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold); // default action (double-click)

        var numberItem = new ToolStripMenuItem("Show CPU number") { CheckOnClick = true };
        numberItem.Click += (_, _) =>
        {
            var changed = _settings.Clone();
            changed.Display = numberItem.Checked ? "number" : "animation";
            ApplySettings(changed);
        };

        var startupItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        startupItem.Click += (_, _) => StartupManager.IsEnabled = startupItem.Checked;

        _menu.Opening += (_, _) =>
        {
            numberItem.Checked = _settings.ShowNumber;
            startupItem.Checked = StartupManager.IsEnabled;
        };
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(numberItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _surface = CreateSurface();

        _cpuTimer.Tick += (_, _) => { SampleCpu(); UpdateTooltip(); };
        _frameTimer.Tick += (_, _) => UpdateFrame();

        ConfigureSampling();
        UpdateFrame();
        _cpuTimer.Start();
        _frameTimer.Start();
    }

    IPetSurface CreateSurface()
    {
        IPetSurface surface = _settings.ShowIn.ToLowerInvariant() switch
        {
            "taskbarbutton" => new TaskbarButtonSurface(_menu),
            "tray" => new TraySurface(_menu),
            _ => new OverlaySurface(_menu, _settings.OverlayOffset),
        };
        surface.SettingsRequested += ShowSettings;
        surface.ExitRequested += ExitThread;
        return surface;
    }

    void UpdateFrame()
    {
        var renderer = Renderer;
        float cpu = renderer == _numberRenderer ? _cpuPercent : _cpuAverage;
        Size size = _surface.FrameSize;
        if (size.Width > 0 && size.Height > 0)
        {
            using var bmp = renderer.Render(cpu, size);
            _surface.ShowFrame(bmp);
        }
        _frameTimer.Interval = Math.Max(20, renderer.FrameIntervalMs);
    }

    void SampleCpu()
    {
        _cpuPercent = _cpu.Sample();
        _cpuAverage = _averager.Add(_cpuPercent);
    }

    void ConfigureSampling()
    {
        _cpuTimer.Interval = _settings.SampleIntervalMs;
        _averager.WindowSize = _settings.AverageWindow;
        _averager.Weighted = _settings.WeightedAverage;
    }

    void UpdateTooltip()
    {
        string text = $"CPU: {_cpuPercent:0}%";
        if (!_settings.ShowNumber)
        {
            if (_settings.AverageSeconds > 0)
                text += $" (avg {_settings.AverageSeconds:0.#} s: {_cpuAverage:0}%)";
            if (_animationRenderer.CurrentAnimation is string anim)
                text += $" · {anim}";
        }
        _surface.SetTooltip(text);
    }

    void ShowSettings()
    {
        if (_settingsForm != null)
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new SettingsForm(_settings);
        _settingsForm.Applied += ApplySettings;
        _settingsForm.FormClosed += (_, _) => { _settingsForm.Dispose(); _settingsForm = null; };
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    void ApplySettings(AppSettings settings)
    {
        bool surfaceChanged = !settings.ShowIn.Equals(_settings.ShowIn, StringComparison.OrdinalIgnoreCase)
                              || settings.OverlayOffset != _settings.OverlayOffset;
        _settings = settings;
        _settings.Save();
        _animationRenderer.Dispose();
        _animationRenderer = new AnimationRenderer(_settings);
        if (surfaceChanged)
        {
            _surface.Dispose();
            _surface = CreateSurface();
        }
        ConfigureSampling();
        UpdateFrame();
        UpdateTooltip();
    }

    protected override void ExitThreadCore()
    {
        _cpuTimer.Stop();
        _frameTimer.Stop();
        _settingsForm?.Close();
        _surface.Dispose();
        _animationRenderer.Dispose();
        base.ExitThreadCore();
    }
}
