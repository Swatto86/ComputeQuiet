using System.Drawing.Drawing2D;

namespace ComputeQuiet;

sealed class MainForm : Form
{
    readonly QuietEngine _engine = new();
    readonly AppSettings _settings;
    readonly NotifyIcon _tray;
    readonly Label _brand = new();
    readonly Label _tagline = new();
    readonly Button _toggle = new();
    readonly CheckBox _aggressive = new();
    readonly CheckBox _powerPlan = new();
    readonly CheckBox _startWithWindows = new();
    readonly CheckBox _minimizeToTray = new();
    readonly TextBox _keepAlive = new();
    readonly Label _keepAliveLabel = new();
    readonly Label _status = new();
    readonly ListBox _log = new();
    QuietState _state;
    bool _allowClose;

    public MainForm(bool startMinimized)
    {
        _settings = AppSettings.Load();
        _state = StateStore.Load();

        Text = "ComputeQuiet";
        Width = 540;
        Height = 720;
        MinimumSize = new Size(440, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 20, 22);
        ForeColor = Color.FromArgb(236, 232, 224);
        Font = new Font("Segoe UI", 10f);
        DoubleBuffered = true;
        Padding = new Padding(28);
        ShowInTaskbar = true;
        Icon = LoadAppIcon();

        _brand.Text = "ComputeQuiet";
        _brand.Font = new Font("Segoe UI Semibold", 28f, FontStyle.Bold);
        _brand.ForeColor = Color.FromArgb(236, 232, 224);
        _brand.AutoSize = true;
        _brand.Location = new Point(28, 28);

        _tagline.Text = "Park background work. Free the machine for play or local AI.";
        _tagline.Font = new Font("Segoe UI", 10.5f);
        _tagline.ForeColor = Color.FromArgb(160, 156, 148);
        _tagline.AutoSize = true;
        _tagline.MaximumSize = new Size(460, 0);
        _tagline.Location = new Point(32, 78);

        _toggle.Size = new Size(280, 72);
        _toggle.FlatStyle = FlatStyle.Flat;
        _toggle.FlatAppearance.BorderSize = 0;
        _toggle.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
        _toggle.Cursor = Cursors.Hand;
        _toggle.Click += (_, _) => ToggleQuiet();

        _aggressive.Text = "Aggressive — suspend all non-essential apps";
        _aggressive.AutoSize = true;
        _aggressive.ForeColor = Color.FromArgb(180, 176, 168);
        _aggressive.BackColor = Color.Transparent;
        _aggressive.Checked = _state.Aggressive;

        _powerPlan.Text = "Switch to High Performance power plan while quiet";
        _powerPlan.AutoSize = true;
        _powerPlan.ForeColor = Color.FromArgb(180, 176, 168);
        _powerPlan.BackColor = Color.Transparent;
        _powerPlan.Checked = _settings.SwitchPowerPlan;
        _powerPlan.CheckedChanged += (_, _) =>
        {
            _settings.SwitchPowerPlan = _powerPlan.Checked;
            _settings.Save();
        };

        _startWithWindows.Text = "Start with Windows (tray)";
        _startWithWindows.AutoSize = true;
        _startWithWindows.ForeColor = Color.FromArgb(180, 176, 168);
        _startWithWindows.BackColor = Color.Transparent;
        try
        {
            if (_settings.StartWithWindows != AutoStart.IsEnabled())
                AutoStart.SetEnabled(_settings.StartWithWindows);
        }
        catch { /* registry may be locked; checkbox still reflects intent */ }
        _startWithWindows.Checked = _settings.StartWithWindows;
        _startWithWindows.CheckedChanged += (_, _) =>
        {
            try
            {
                AutoStart.SetEnabled(_startWithWindows.Checked);
                _settings.StartWithWindows = _startWithWindows.Checked;
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ComputeQuiet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _startWithWindows.Checked = AutoStart.IsEnabled();
            }
        };

        _minimizeToTray.Text = "Close button minimizes to tray";
        _minimizeToTray.AutoSize = true;
        _minimizeToTray.ForeColor = Color.FromArgb(180, 176, 168);
        _minimizeToTray.BackColor = Color.Transparent;
        _minimizeToTray.Checked = _settings.MinimizeToTray;
        _minimizeToTray.CheckedChanged += (_, _) =>
        {
            _settings.MinimizeToTray = _minimizeToTray.Checked;
            _settings.Save();
        };

        _keepAliveLabel.Text = "Also keep running (comma-separated names)";
        _keepAliveLabel.AutoSize = true;
        _keepAliveLabel.ForeColor = Color.FromArgb(140, 136, 128);

        _keepAlive.BorderStyle = BorderStyle.FixedSingle;
        _keepAlive.BackColor = Color.FromArgb(28, 30, 32);
        _keepAlive.ForeColor = Color.FromArgb(220, 216, 208);
        _keepAlive.Font = new Font("Segoe UI", 10f);
        _keepAlive.PlaceholderText = "e.g. Discord, chrome";
        _keepAlive.Size = new Size(460, 28);

        _status.AutoSize = false;
        _status.Size = new Size(460, 40);
        _status.Font = new Font("Segoe UI", 10f);
        _status.ForeColor = Color.FromArgb(180, 176, 168);

        _log.BorderStyle = BorderStyle.None;
        _log.BackColor = Color.FromArgb(28, 30, 32);
        _log.ForeColor = Color.FromArgb(180, 176, 168);
        _log.Font = new Font("Consolas", 9f);
        _log.IntegralHeight = false;

        Controls.Add(_brand);
        Controls.Add(_tagline);
        Controls.Add(_toggle);
        Controls.Add(_aggressive);
        Controls.Add(_powerPlan);
        Controls.Add(_startWithWindows);
        Controls.Add(_minimizeToTray);
        Controls.Add(_keepAliveLabel);
        Controls.Add(_keepAlive);
        Controls.Add(_status);
        Controls.Add(_log);

        _tray = BuildTray();
        Resize += (_, _) => LayoutControls();
        FormClosing += OnFormClosing;
        LayoutControls();
        RefreshUi();

        if (_state.IsQuiet && _state.Log.Count > 0)
            foreach (var line in _state.Log)
                _log.Items.Add(line);

        if (startMinimized)
        {
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            Visible = false;
            _tray.Visible = true;
            _tray.ShowBalloonTip(2500, "ComputeQuiet", "Running in the tray.", ToolTipIcon.Info);
        }
    }

    NotifyIcon BuildTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show ComputeQuiet", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        var quietItem = new ToolStripMenuItem("Go Quiet", null, (_, _) =>
        {
            if (!_state.IsQuiet) ToggleQuiet();
        });
        var restoreItem = new ToolStripMenuItem("Restore", null, (_, _) =>
        {
            if (_state.IsQuiet) ToggleQuiet();
        });
        menu.Items.Add(quietItem);
        menu.Items.Add(restoreItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _allowClose = true;
            Close();
        });
        menu.Opening += (_, _) =>
        {
            quietItem.Enabled = !_state.IsQuiet;
            restoreItem.Enabled = _state.IsQuiet;
        };

        var tray = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Application,
            Text = "ComputeQuiet",
            Visible = true,
            ContextMenuStrip = menu,
        };
        tray.DoubleClick += (_, _) => RestoreFromTray();
        return tray;
    }

    void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose || e.CloseReason != CloseReason.UserClosing || !_settings.MinimizeToTray)
        {
            _tray.Visible = false;
            _tray.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
        _tray.ShowBalloonTip(2000, "ComputeQuiet", "Still running in the tray.", ToolTipIcon.Info);
    }

    void LayoutControls()
    {
        var contentWidth = ClientSize.Width - 64;
        _tagline.MaximumSize = new Size(contentWidth, 0);
        _toggle.Location = new Point((ClientSize.Width - _toggle.Width) / 2, 124);

        var y = 214;
        _aggressive.Location = new Point(32, y); y += 28;
        _powerPlan.Location = new Point(32, y); y += 28;
        _startWithWindows.Location = new Point(32, y); y += 28;
        _minimizeToTray.Location = new Point(32, y); y += 34;
        _keepAliveLabel.Location = new Point(32, y); y += 24;
        _keepAlive.Location = new Point(32, y);
        _keepAlive.Width = contentWidth;
        y += 40;
        _status.Width = contentWidth;
        _status.Location = new Point(32, y);
        y += 44;
        _log.Location = new Point(28, y);
        _log.Size = new Size(ClientSize.Width - 56, Math.Max(100, ClientSize.Height - y - 28));
    }

    void ToggleQuiet()
    {
        _toggle.Enabled = false;
        SetOptionEnabled(false);
        UseWaitCursor = true;
        try
        {
            if (_state.IsQuiet)
                _state = _engine.Disable();
            else
            {
                var options = new QuietOptions { Aggressive = _aggressive.Checked };
                options.ExtraKeepAlive.Add("ComputeQuiet");
                foreach (var part in _keepAlive.Text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var name = ProcessPolicy.Normalize(part);
                    if (name.Length > 0)
                        options.ExtraKeepAlive.Add(name);
                }
                _state = _engine.Enable(options);
            }

            _log.Items.Clear();
            foreach (var line in _engine.Log)
                _log.Items.Add(line);
            if (_log.Items.Count > 0)
                _log.TopIndex = _log.Items.Count - 1;
            RefreshUi();
            _tray.Text = _state.IsQuiet ? "ComputeQuiet — QUIET" : "ComputeQuiet";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ComputeQuiet", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _toggle.Enabled = true;
            SetOptionEnabled(!_state.IsQuiet);
        }
    }

    void SetOptionEnabled(bool enabled)
    {
        _aggressive.Enabled = enabled;
        _keepAlive.Enabled = enabled;
        // Settings stay editable anytime except aggressive/keepalive during quiet.
        _powerPlan.Enabled = enabled;
    }

    void RefreshUi()
    {
        if (_state.IsQuiet)
        {
            _toggle.Text = "RESTORE";
            _toggle.BackColor = Color.FromArgb(212, 168, 75);
            _toggle.ForeColor = Color.FromArgb(24, 22, 18);
            _status.Text = _state.QuietedAt is { } at
                ? $"Quiet since {at:t} — {_state.SuspendedPids.Count} suspended, {_state.StoppedServices.Count} services stopped."
                : "Quiet mode is on.";
            SetOptionEnabled(false);
        }
        else
        {
            _toggle.Text = "GO QUIET";
            _toggle.BackColor = Color.FromArgb(62, 140, 110);
            _toggle.ForeColor = Color.FromArgb(236, 232, 224);
            _status.Text = "System is normal. Press GO QUIET before a game or local AI run.";
            SetOptionEnabled(true);
        }
    }

    static Icon LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "ComputeQuiet.ico");
        if (File.Exists(path))
            return new Icon(path);
        path = Path.Combine(AppContext.BaseDirectory, "ComputeQuiet.ico");
        if (File.Exists(path))
            return new Icon(path);
        return SystemIcons.Application;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(
            ClientRectangle,
            Color.FromArgb(18, 20, 22),
            Color.FromArgb(28, 36, 34),
            90f);
        e.Graphics.FillRectangle(brush, ClientRectangle);

        using var wash = new SolidBrush(Color.FromArgb(30, 62, 140, 110));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(wash, ClientSize.Width - 280, -80, 360, 280);
    }
}
