using System.Diagnostics;
using System.Security.Principal;

namespace ComputeQuiet;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--self-check", StringComparer.OrdinalIgnoreCase))
        {
            Environment.Exit(SelfCheck.Run() ? 0 : 1);
            return;
        }

        ApplicationConfiguration.Initialize();

        var startMinimized = args.Any(a =>
            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-minimized", StringComparison.OrdinalIgnoreCase));

        if (!IsAdministrator())
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = string.Join(' ', args.Select(QuoteIfNeeded)),
                };
                Process.Start(start);
            }
            catch
            {
                MessageBox.Show(
                    "ComputeQuiet needs Administrator rights to pause services and suspend other processes.",
                    "ComputeQuiet",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            return;
        }

        Application.Run(new MainForm(startMinimized));
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
