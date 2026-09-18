using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace ComputeQuiet;

public partial class MainWindow : Window
{
    readonly QuietEngine _engine = new();
    readonly AppSettings _settings;
    readonly WinForms.NotifyIcon _tray;
    QuietState _state;
    bool _allowClose;
    bool _suppressSettingEvents;

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _state = StateStore.Load();

        try
        {
            Icon = LoadWindowIcon();
        }
        catch
        {
            // Icon optional at design-time.
        }

        _suppressSettingEvents = true;
        AggressiveCheck.IsChecked = _state.Aggressive;
        PowerPlanCheck.IsChecked = _settings.SwitchPowerPlan;
        try
        {
            if (_settings.StartWithWindows != AutoStart.IsEnabled())
                AutoStart.SetEnabled(_settings.StartWithWindows);
        }
        catch { /* registry may be locked */ }
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTray;
        _suppressSettingEvents = false;

        KeepAliveBox.GotFocus += (_, _) =>
        {
            if (KeepAliveBox.Text.Length == 0 && KeepAliveBox.Tag is string hint)
                KeepAliveBox.ToolTip = hint;
        };

        _tray = BuildTray();
        RefreshUi();

        if (_state.IsQuiet && _state.Log.Count > 0)
        {
            foreach (var line in _state.Log)
                LogList.Items.Add(line);
            LogList.ScrollIntoView(LogList.Items[^1]!);
        }

        if (startMinimized)
        {
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
            Hide();
            _tray.Visible = true;
            _tray.ShowBalloonTip(2500, "ComputeQuiet", "Running in the tray.", WinForms.ToolTipIcon.Info);
        }
    }

    WinForms.NotifyIcon BuildTray()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Show ComputeQuiet", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        var quietItem = new WinForms.ToolStripMenuItem("Go Quiet", null, (_, _) =>
        {
            if (!_state.IsQuiet) ToggleQuiet();
        });
        var restoreItem = new WinForms.ToolStripMenuItem("Restore", null, (_, _) =>
        {
            if (_state.IsQuiet) ToggleQuiet();
        });
        menu.Items.Add(quietItem);
        menu.Items.Add(restoreItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
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

        var tray = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
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
        WindowState = WindowState.Normal;
        Activate();
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_settings.MinimizeToTray)
        {
            _tray.Visible = false;
            _tray.Dispose();
            System.Windows.Application.Current.Shutdown();
            return;
        }

        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
        _tray.ShowBalloonTip(2000, "ComputeQuiet", "Still running in the tray.", WinForms.ToolTipIcon.Info);
    }

    void OnToggleClick(object sender, RoutedEventArgs e) => ToggleQuiet();

    void OnPowerPlanChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingEvents) return;
        _settings.SwitchPowerPlan = PowerPlanCheck.IsChecked == true;
        _settings.Save();
    }

    void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingEvents) return;
        try
        {
            var enabled = StartWithWindowsCheck.IsChecked == true;
            AutoStart.SetEnabled(enabled);
            _settings.StartWithWindows = enabled;
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ComputeQuiet", MessageBoxButton.OK, MessageBoxImage.Warning);
            _suppressSettingEvents = true;
            StartWithWindowsCheck.IsChecked = AutoStart.IsEnabled();
            _suppressSettingEvents = false;
        }
    }

    void OnMinimizeToTrayChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingEvents) return;
        _settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
        _settings.Save();
    }

    void ToggleQuiet()
    {
        ToggleButton.IsEnabled = false;
        SetOptionEnabled(false);
        Cursor = System.Windows.Input.Cursors.Wait;
        try
        {
            if (_state.IsQuiet)
                _state = _engine.Disable();
            else
            {
                var options = new QuietOptions { Aggressive = AggressiveCheck.IsChecked == true };
                options.ExtraKeepAlive.Add("ComputeQuiet");
                foreach (var part in KeepAliveBox.Text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var name = ProcessPolicy.Normalize(part);
                    if (name.Length > 0)
                        options.ExtraKeepAlive.Add(name);
                }
                _state = _engine.Enable(options);
            }

            LogList.Items.Clear();
            foreach (var line in _engine.Log)
                LogList.Items.Add(line);
            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]!);
            RefreshUi();
            _tray.Text = _state.IsQuiet ? "ComputeQuiet — QUIET" : "ComputeQuiet";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ComputeQuiet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Cursor = System.Windows.Input.Cursors.Arrow;
            ToggleButton.IsEnabled = true;
            SetOptionEnabled(!_state.IsQuiet);
        }
    }

    void SetOptionEnabled(bool enabled)
    {
        AggressiveCheck.IsEnabled = enabled;
        KeepAliveBox.IsEnabled = enabled;
        PowerPlanCheck.IsEnabled = enabled;
    }

    void RefreshUi()
    {
        if (_state.IsQuiet)
        {
            ToggleButton.Content = "RESTORE";
            ToggleButton.Background = (System.Windows.Media.Brush)FindResource("RestoreGoldBrush");
            ToggleButton.Foreground = (System.Windows.Media.Brush)FindResource("RestoreFgBrush");
            StatusText.Text = _state.QuietedAt is { } at
                ? $"Quiet since {at:t} — {_state.SuspendedPids.Count} suspended, {_state.StoppedServices.Count} services stopped."
                : "Quiet mode is on.";
            SetOptionEnabled(false);
        }
        else
        {
            ToggleButton.Content = "GO QUIET";
            ToggleButton.Background = (System.Windows.Media.Brush)FindResource("QuietGreenBrush");
            ToggleButton.Foreground = (System.Windows.Media.Brush)FindResource("FgBrush");
            StatusText.Text = "System is normal. Press GO QUIET before a game or local AI run.";
            SetOptionEnabled(true);
        }
    }

    static ImageSource? LoadWindowIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "ComputeQuiet.ico");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "ComputeQuiet.ico");
        if (!File.Exists(path))
            return null;

        return System.Windows.Media.Imaging.BitmapFrame.Create(
            new Uri(path),
            System.Windows.Media.Imaging.BitmapCreateOptions.None,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
    }

    static Icon LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "ComputeQuiet.ico");
        if (File.Exists(path))
            return new Icon(path);
        path = Path.Combine(AppContext.BaseDirectory, "ComputeQuiet.ico");
        if (File.Exists(path))
            return new Icon(path);
        return SystemIcons.Application;
    }
}
