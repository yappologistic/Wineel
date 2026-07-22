using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace Wineel;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _pause;
    private readonly Forms.ToolStripMenuItem _replacement;
    private readonly Forms.ToolStripMenuItem _startup;
    public event Action? OpenSettings;
    public event Action? TryRequested;
    public event Action? PauseRequested;
    public event Action? ReplacementRequested;
    public event Action? StartupRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Settings", null, (_, _) => OpenSettings?.Invoke());
        menu.Items.Add("Try Wineel", null, (_, _) => TryRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        _pause = new Forms.ToolStripMenuItem("Pause", null, (_, _) => PauseRequested?.Invoke());
        _replacement = new Forms.ToolStripMenuItem("Replace Alt+Tab", null, (_, _) => ReplacementRequested?.Invoke());
        _startup = new Forms.ToolStripMenuItem("Start with Windows", null, (_, _) => StartupRequested?.Invoke());
        menu.Items.Add(_pause); menu.Items.Add(_replacement); menu.Items.Add(_startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("About Wineel", null, (_, _) => Forms.MessageBox.Show("Wineel 1.0\nA fast radial application switcher for Windows.\n\nNo telemetry. No network communication.", "About Wineel", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information));
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());
        _icon = new Forms.NotifyIcon
        {
            Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unavailable.")),
            Text = "Wineel",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenSettings?.Invoke();
    }

    public void Apply(AppSettings settings)
    {
        _pause.Text = settings.IsPaused ? "Resume" : "Pause";
        _replacement.Checked = settings.ReplaceAltTab;
        _startup.Checked = settings.StartWithWindows;
        _icon.Text = settings.IsPaused ? "Wineel — Paused" : "Wineel";
    }

    public void Notify(string message)
    {
        _icon.BalloonTipTitle = "Wineel";
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(5000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
