using System.ComponentModel;

namespace TaskbarPet;

/// <summary>
/// Settings window with tabs: Display, CPU &amp; switching, Animations, General.
/// Every setting has an info icon that explains it on hover.
/// Edits a copy of the settings; <see cref="Applied"/> fires on OK/Apply.
/// </summary>
sealed class SettingsForm : Form
{
    readonly AppSettings _settings;
    readonly BindingList<RangeRule> _ranges;
    readonly ToolTip _help = new() { AutoPopDelay = 32000, InitialDelay = 150, ReshowDelay = 50, ShowAlways = true };
    Bitmap? _infoIcon;

    readonly RadioButton _inTray = new() { Text = "Tray icon", AutoSize = true };
    readonly RadioButton _inTaskbarButton = new() { Text = "Taskbar button", AutoSize = true };
    readonly RadioButton _inOverlay = new() { Text = "Taskbar overlay", AutoSize = true };
    readonly NumericUpDown _overlayOffset = new() { Minimum = 0, Maximum = 2000 };
    readonly RadioButton _showAnimation = new() { Text = "Animation", AutoSize = true };
    readonly RadioButton _showNumber = new() { Text = "CPU number", AutoSize = true };
    readonly NumericUpDown _samplesPerSecond = new() { Minimum = 1, Maximum = 20 };
    readonly NumericUpDown _averageSeconds = new() { Minimum = 0, Maximum = 60, DecimalPlaces = 1, Increment = 0.5m };
    readonly ComboBox _weighting = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Label _averageInfo = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    readonly NumericUpDown _minAnimationSeconds = new() { Minimum = 0, Maximum = 60, DecimalPlaces = 1, Increment = 0.5m };
    readonly CheckBox _stickinessEnabled = new() { Text = "On", AutoSize = true };
    readonly NumericUpDown _stickiness = new() { Minimum = 1, Maximum = 50 };
    readonly DataGridView _grid = new();
    readonly PictureBox _preview = new() { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    readonly Label _previewName = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    MemoryStream? _previewStream;
    bool _loading;

    /// <summary>Raised with the new settings when the user presses OK or Apply.</summary>
    public event Action<AppSettings>? Applied;

    public SettingsForm(AppSettings current)
    {
        _settings = current.Clone();
        _ranges = new BindingList<RangeRule>(_settings.Ranges);

        Text = "Taskbar Pet – Settings";
        Icon = Environment.ProcessPath is string exe ? Icon.ExtractAssociatedIcon(exe) : null;
        Font = SystemFonts.MessageBoxFont;
        // Sizes below are in 96-DPI units and scaled by S(); the font already follows the DPI.
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        Padding = new Padding(S(12));
        foreach (var n in new[] { _samplesPerSecond, _overlayOffset, _averageSeconds, _minAnimationSeconds, _stickiness })
            n.Width = S(70);
        _weighting.Width = S(240);
        _preview.Size = new Size(S(120), S(120));
        _averageInfo.MaximumSize = new Size(S(460), 0);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(S(12), S(4)) };
        tabs.TabPages.Add(Page("Display", BuildDisplayTab()));
        tabs.TabPages.Add(Page("CPU && switching", BuildCpuTab()));
        tabs.TabPages.Add(Page("Animations", BuildAnimationsTab(), fill: true));
        tabs.TabPages.Add(Page("General", BuildGeneralTab()));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(tabs);
        root.Controls.Add(BuildButtons());
        Controls.Add(root);

        // Fit on screen: at 150 % scaling a 1080p screen is only ~690 logical pixels high.
        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, S(1280), S(720));
        ClientSize = new Size(Math.Min(S(620), work.Width - S(40)), Math.Min(S(520), work.Height - S(60)));
        MinimumSize = new Size(Math.Min(S(560), work.Width), Math.Min(S(460), work.Height));

        LoadValues();
    }

    int S(int logical) => LogicalToDeviceUnits(logical);

    // ---------- tabs ----------

    Control BuildDisplayTab()
    {
        // Separate panels so the two radio groups work independently.
        var where = Flow(_inTray, _inTaskbarButton, _inOverlay);
        var what = Flow(_showAnimation, _showNumber);
        var offset = Flow(_overlayOffset, Hint("px left of the tray icons"));
        _inOverlay.CheckedChanged += (_, _) => _overlayOffset.Enabled = _inOverlay.Checked;

        var table = Grid();
        AddRow(table, "Show in:", where,
            "Where the pet appears.\n" +
            "• Tray icon: next to the clock. 16 px × display scaling (24 px at 150 %).\n" +
            "   Windows fixes this size. Right-click for the menu, double-click for settings.\n" +
            "• Taskbar button: its own button among the app buttons, 24 px × scaling\n" +
            "   (36 px at 150 %). Click the button for the menu. Also listed in Alt+Tab.\n" +
            "• Taskbar overlay: drawn on the taskbar just left of the tray icons,\n" +
            "   almost as tall as the taskbar (largest). Right-click for the menu,\n" +
            "   double-click for settings. Hidden while a fullscreen app is in front.");
        AddRow(table, "Overlay position:", offset,
            "Only for the taskbar overlay.\n" +
            "Gap between the pet and the tray icons, in pixels at 100 % scaling.\n" +
            "Increase it if the overlay covers something on the taskbar.");
        AddRow(table, "Show:", what,
            "• Animation: plays the GIF assigned to the current CPU range\n" +
            "   (set on the Animations tab).\n" +
            "• CPU number: shows the latest CPU reading as digits.");
        return table;
    }

    Control BuildCpuTab()
    {
        _weighting.Items.AddRange(["Recent samples count more", "All samples count equally"]);
        _samplesPerSecond.ValueChanged += (_, _) => UpdateAverageInfo();
        _averageSeconds.ValueChanged += (_, _) => UpdateAverageInfo();
        _weighting.SelectedIndexChanged += (_, _) => UpdateAverageInfo();
        _stickinessEnabled.CheckedChanged += (_, _) => _stickiness.Enabled = _stickinessEnabled.Checked;

        var measurement = Grid();
        AddSection(measurement, "Measurement");
        AddRow(measurement, "Samples per second:", _samplesPerSecond,
            "How often CPU usage is measured (1–20 times per second).\n" +
            "More samples give a smoother, more up-to-date average.\n" +
            "The cost is negligible either way.");
        AddRow(measurement, "Average over (seconds):", _averageSeconds,
            "The animation follows the CPU average of the last N seconds\n" +
            "instead of single readings, so short spikes are ignored.\n" +
            "Longer = calmer pet, but slower to react. 0 = follow every reading.\n" +
            "The CPU number display always shows the latest reading.");
        AddRow(measurement, "Weighting:", _weighting,
            "How readings inside the averaging window are combined.\n" +
            "• Recent samples count more: the newest reading has the highest weight,\n" +
            "   the oldest the lowest, so real changes show up sooner.\n" +
            "• All samples count equally: a plain average.");
        measurement.Controls.Add(_averageInfo, 0, measurement.RowCount++);
        measurement.SetColumnSpan(_averageInfo, 3);

        var switching = measurement;
        AddSection(switching, "Animation switching");
        AddRow(switching, "Minimum animation time (seconds):", _minAnimationSeconds,
            "Once an animation starts, it keeps playing at least this long,\n" +
            "even if the CPU moves to another range meanwhile.\n" +
            "This is the most effective setting against animations changing too often.\n" +
            "Big changes still show up right after this time. 0 = off.");
        AddRow(switching, "Range stickiness:", Flow(_stickinessEnabled, _stickiness, Hint("percentage points")),
            "Makes the current range \"sticky\": the CPU average has to move this many\n" +
            "points beyond the range's edges before the animation changes.\n" +
            "Example: in the 46–60 % range with stickiness 5, the pet moves up only\n" +
            "above 65 % and down only below 41 %.\n" +
            "Stops flipping back and forth when the CPU hovers around a range edge.");
        return measurement;
    }

    Control BuildAnimationsTab()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "From %", DataPropertyName = nameof(RangeRule.From), Width = S(70) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "To %", DataPropertyName = nameof(RangeRule.To), Width = S(70) });
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "Animation",
            DataPropertyName = nameof(RangeRule.Animation),
            DataSource = AnimationNames(),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FlatStyle = FlatStyle.Flat,
        });
        _grid.DataSource = _ranges;

        _grid.CellValidating += ValidateCell;
        _grid.DataError += (_, e) => e.ThrowException = false;
        // Commit combo box choices immediately so the preview follows them.
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.CurrentCell is DataGridViewComboBoxCell)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, _) => UpdatePreview();
        _grid.SelectionChanged += (_, _) => UpdatePreview();

        var add = new Button { Text = "Add range", AutoSize = true };
        add.Click += (_, _) => AddRange();
        var remove = new Button { Text = "Remove range", AutoSize = true };
        remove.Click += (_, _) => RemoveRange();

        var header = Flow(new Label { Text = "Animation for each CPU range", AutoSize = true, Margin = new Padding(S(3), S(4), S(3), S(3)) },
            InfoIcon("Which animation plays for each CPU range (whole percents, both ends included).\n" +
                     "Ranges are matched against the averaged CPU (CPU & switching tab).\n" +
                     "Ranges must not overlap. For CPU values in a gap, the CPU number is shown.\n" +
                     "The list offers the built-in animations plus any .gif in an optional\n" +
                     "\"animations\" folder next to TaskbarPet.exe (a GIF there with the same\n" +
                     "name as a built-in one replaces it).\n" +
                     "Select a row to see its animation in the preview."));

        var previewPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        previewPanel.Controls.AddRange([new Label { Text = "Preview", AutoSize = true }, _preview, _previewName]);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(header, 0, 0);
        table.Controls.Add(_grid, 0, 1);
        table.Controls.Add(previewPanel, 1, 1);
        table.Controls.Add(Flow(add, remove), 0, 2);
        return table;
    }

    Control BuildGeneralTab()
    {
        var table = Grid();
        AddRow(table, "", _startWithWindows,
            "Starts Taskbar Pet automatically when you sign in to Windows.\n" +
            "The same switch appears in Windows Settings → Apps → Startup\n" +
            "and in the pet's right-click menu.");
        string version = typeof(SettingsForm).Assembly.GetName().Version?.ToString(3) ?? "";
        AddRow(table, "Version:", Hint(version), "The installed version of Taskbar Pet.");
        AddRow(table, "Settings file:", Hint(AppSettings.FilePath),
            "Where these settings are stored. There is no need to edit it by hand.");
        return table;
    }

    FlowLayoutPanel BuildButtons()
    {
        var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(S(80), 0) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(S(80), 0), DialogResult = DialogResult.Cancel };
        var apply = new Button { Text = "Apply", AutoSize = true, MinimumSize = new Size(S(80), 0) };
        ok.Click += (_, _) => { if (TryApply()) Close(); };
        apply.Click += (_, _) => TryApply();
        CancelButton = cancel;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0, S(8), 0, 0) };
        flow.Controls.AddRange([apply, cancel, ok]);
        return flow;
    }

    // ---------- layout helpers ----------

    TabPage Page(string title, Control content, bool fill = false)
    {
        var page = new TabPage(title) { Padding = new Padding(S(10)), UseVisualStyleBackColor = true, AutoScroll = !fill };
        content.Dock = fill ? DockStyle.Fill : DockStyle.Top;
        page.Controls.Add(content);
        return page;
    }

    /// <summary>Three columns: label, control, info icon.</summary>
    TableLayoutPanel Grid() => new() { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Top };

    void AddRow(TableLayoutPanel table, string label, Control control, string help)
    {
        int row = table.RowCount++;
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(S(3), S(7), S(12), S(3)) };
        control.Anchor = AnchorStyles.Left;
        table.Controls.Add(l, 0, row);
        table.Controls.Add(control, 1, row);
        table.Controls.Add(InfoIcon(help), 2, row);
        if (label.Length > 0)
            _help.SetToolTip(l, help);
    }

    /// <summary>Bold heading row spanning the whole table.</summary>
    void AddSection(TableLayoutPanel table, string title)
    {
        var heading = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(S(3), table.RowCount == 0 ? S(3) : S(16), S(3), S(4)),
        };
        table.Controls.Add(heading, 0, table.RowCount++);
        table.SetColumnSpan(heading, 3);
    }

    PictureBox InfoIcon(string help)
    {
        _infoIcon ??= SystemIcons.GetStockIcon(StockIconId.Info, S(16)).ToBitmap();
        var icon = new PictureBox
        {
            Image = _infoIcon,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(S(16), S(16)),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(S(6), S(6), S(3), S(3)),
            Cursor = Cursors.Help,
        };
        _help.SetToolTip(icon, help);
        return icon;
    }

    FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        flow.Controls.AddRange(controls);
        return flow;
    }

    Label Hint(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(S(3), S(7), S(3), S(3)) };

    // ---------- values ----------

    void LoadValues()
    {
        _loading = true;
        string showIn = _settings.ShowIn.ToLowerInvariant();
        _inTaskbarButton.Checked = showIn == "taskbarbutton";
        _inOverlay.Checked = showIn == "overlay";
        _inTray.Checked = !_inTaskbarButton.Checked && !_inOverlay.Checked;
        _overlayOffset.Value = Math.Clamp(_settings.OverlayOffset, 0, 2000);
        _overlayOffset.Enabled = _inOverlay.Checked;
        _showNumber.Checked = _settings.ShowNumber;
        _showAnimation.Checked = !_settings.ShowNumber;
        _samplesPerSecond.Value = Math.Clamp(_settings.SamplesPerSecond, 1, 20);
        _averageSeconds.Value = Math.Clamp((decimal)_settings.AverageSeconds, 0, 60);
        _weighting.SelectedIndex = _settings.WeightedAverage ? 0 : 1;
        _minAnimationSeconds.Value = Math.Clamp((decimal)_settings.MinAnimationSeconds, 0, 60);
        _stickinessEnabled.Checked = _settings.StickinessEnabled;
        _stickiness.Value = Math.Clamp(_settings.Stickiness, 1, 50);
        _stickiness.Enabled = _settings.StickinessEnabled;
        _startWithWindows.Checked = StartupManager.IsEnabled;
        _loading = false;
        UpdateAverageInfo();
    }

    void ReadValues()
    {
        _settings.Display = _showNumber.Checked ? "number" : "animation";
        _settings.ShowIn = _inOverlay.Checked ? "overlay" : _inTaskbarButton.Checked ? "taskbarButton" : "tray";
        _settings.OverlayOffset = (int)_overlayOffset.Value;
        _settings.SamplesPerSecond = (int)_samplesPerSecond.Value;
        _settings.AverageSeconds = (double)_averageSeconds.Value;
        _settings.WeightedAverage = _weighting.SelectedIndex == 0;
        _settings.MinAnimationSeconds = (double)_minAnimationSeconds.Value;
        _settings.StickinessEnabled = _stickinessEnabled.Checked;
        _settings.Stickiness = (int)_stickiness.Value;
    }

    void UpdateAverageInfo()
    {
        if (_loading)
            return; // controls are being filled from _settings; don't read half-filled values back
        ReadValues();
        int n = _settings.AverageWindow;
        _averageInfo.Text = n == 1
            ? "The animation follows every single sample (no averaging)."
            : _settings.WeightedAverage
                ? $"Weighted average of the last {n} samples (newest counts {n}×, oldest 1×)."
                : $"Plain average of the last {n} samples.";
    }

    List<string> AnimationNames()
    {
        var names = AnimationLibrary.Names();
        // Keep names used in settings even if the file is gone, so the grid can show them.
        names.AddRange(_settings.Ranges.Select(r => r.Animation).Where(a => !string.IsNullOrEmpty(a)));
        return names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    void ValidateCell(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        _grid.Rows[e.RowIndex].ErrorText = "";
        if (e.ColumnIndex > 1)
            return;
        if (!int.TryParse(e.FormattedValue?.ToString(), out int v) || v < 0 || v > 100)
        {
            _grid.Rows[e.RowIndex].ErrorText = "Enter a whole number from 0 to 100.";
            e.Cancel = true;
        }
    }

    void AddRange()
    {
        int from = _ranges.Count == 0 ? 0 : Math.Min(100, _ranges.Max(r => r.To) + 1);
        string animation = (_grid.Columns[2] as DataGridViewComboBoxColumn)?.DataSource is List<string> { Count: > 0 } names ? names[0] : "";
        _ranges.Add(new RangeRule { From = from, To = 100, Animation = animation });
        _grid.CurrentCell = _grid.Rows[^1].Cells[0];
    }

    void RemoveRange()
    {
        if (_grid.CurrentRow?.DataBoundItem is RangeRule rule)
            _ranges.Remove(rule);
    }

    void UpdatePreview()
    {
        string? name = (_grid.CurrentRow?.DataBoundItem as RangeRule)?.Animation;

        var old = _preview.Image;
        var oldStream = _previewStream;
        _preview.Image = null;
        _previewStream = null;
        if (!string.IsNullOrEmpty(name) && AnimationLibrary.Load(name) is byte[] data)
        {
            // The stream must stay open while the GIF animates.
            _previewStream = new MemoryStream(data);
            _preview.Image = Image.FromStream(_previewStream);
            _previewName.Text = name;
        }
        else
        {
            _previewName.Text = string.IsNullOrEmpty(name) ? "" : $"{name}: animation not found";
        }
        old?.Dispose();
        oldStream?.Dispose();
    }

    bool TryApply()
    {
        if (!_grid.EndEdit())
            return false;
        ReadValues();

        var errors = _settings.RangeErrors();
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join("\n", errors), "Fix the CPU ranges", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        var gaps = _settings.RangeGaps();
        if (gaps.Count > 0 && MessageBox.Show(this,
                $"No animation is set for CPU {string.Join(", ", gaps)}. The CPU number will be shown there.\n\nSave anyway?",
                "Gaps in CPU ranges", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return false;

        _settings.Ranges.Sort((a, b) => a.From.CompareTo(b.From));
        _ranges.ResetBindings();
        StartupManager.IsEnabled = _startWithWindows.Checked;
        Applied?.Invoke(_settings.Clone());
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _preview.Image?.Dispose();
            _previewStream?.Dispose();
            _infoIcon?.Dispose();
            _help.Dispose();
        }
        base.Dispose(disposing);
    }
}
