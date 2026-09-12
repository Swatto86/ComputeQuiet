using System;
using System.IO;
using System.Windows.Forms;

public class Probe : Form {
    readonly byte[] memory = new byte[16 * 1024 * 1024];
    static string Log { get { return Environment.GetEnvironmentVariable("GAMEQUIET_PROBE_LOG"); } }
    public Probe() {
        Text = "GameQuiet disposable workload"; Width = 420; Height = 160;
        Controls.Add(new Label { Text = "Disposable verification app. Safe to close.", Dock = DockStyle.Fill });
        for (int i=0;i<memory.Length;i+=4096) memory[i]=1;
        Shown += delegate { File.AppendAllText(Log, "started:" + System.Diagnostics.Process.GetCurrentProcess().Id + Environment.NewLine); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (File.Exists(Log + ".refuse")) { e.Cancel = true; File.AppendAllText(Log, "refused:" + System.Diagnostics.Process.GetCurrentProcess().Id + Environment.NewLine); }
            else File.AppendAllText(Log, "closed:" + System.Diagnostics.Process.GetCurrentProcess().Id + Environment.NewLine);
        };
    }
    [STAThread] public static void Main() { Application.Run(new Probe()); }
}
