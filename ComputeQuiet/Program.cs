using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace ComputeQuiet;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--self-check", StringComparer.OrdinalIgnoreCase))
        {
            Environment.Exit(SelfCheck.Run() ? 0 : 1);
            return;
        }

        var startMinimized = args.Any(a =>
            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-minimized", StringComparison.OrdinalIgnoreCase));

        if (!IsAdministrator())
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName
                        ?? throw new InvalidOperationException("Cannot locate ComputeQuiet.exe"),
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = string.Join(' ', args.Select(QuoteIfNeeded)),
                };
                Process.Start(start);
            }
            catch
            {
            System.Windows.MessageBox.Show(
                    "ComputeQuiet needs Administrator rights to pause services and suspend other processes.",
                    "ComputeQuiet",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run(new MainWindow(startMinimized));
    }

    static string QuoteIfNeeded(string value) =>
        value.Contains(' ') ? $"\"{value}\"" : value;

    static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
