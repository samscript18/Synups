using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Synups.Desktop.Integrations;

public sealed class TrayController : IDisposable
{
    private readonly TrayIcon? tray;
    private readonly Action restore;
    public bool Restorable { get; }
    public string Status { get; }
    public TrayController(Action restore, Action quit, Action? diagnostics = null, Action? quickDiagnostic = null, Action? recentIncidents = null)
    {
        this.restore = restore;
        try
        {
            var app = Application.Current;
            if (app is null) { Status = "Tray unavailable: application not initialized."; return; }
            using var icon = AssetLoader.Open(new Uri("avares://Synups.Desktop/Assets/Synups.ico"));
            tray = new TrayIcon { Icon = new WindowIcon(icon), ToolTipText = "Synups", IsVisible = true };
            var show = new NativeMenuItem("Show Synups"); show.Click += (_, _) => restore();
            var exit = new NativeMenuItem("Exit Synups"); exit.Click += (_, _) => quit();
            var menu = new NativeMenu(); menu.Items.Add(show);
            void Add(string title,Action? action) { if(action is null) return; var item=new NativeMenuItem(title); item.Click+=(_,_)=>action(); menu.Items.Add(item); }
            Add("Open Diagnostics",diagnostics); Add("Quick Diagnostic…",quickDiagnostic); Add("Recent incidents",recentIncidents);
            menu.Items.Add(new NativeMenuItemSeparator()); menu.Items.Add(exit); tray.Menu = menu;
            tray.Clicked += Clicked;
            var icons = TrayIcon.GetIcons(app) ?? new TrayIcons(); icons.Add(tray); TrayIcon.SetIcons(app, icons);
            // Linux may export a menu without any host displaying it; never hide the window there.
            Restorable = (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) && tray.NativeMenuExporter is not null;
            Status = Restorable ? "Native tray menu initialized: Show Synups, Diagnostics, Quick Diagnostic preview, Recent incidents and Exit. Quick Diagnostic opens a preview before any network request." : "Tray restoration is not established here; minimizing keeps the taskbar/dock window available.";
        }
        catch (Exception ex) { Restorable = false; Status = $"Tray unavailable: {ex.Message}. Window will not be hidden."; }
    }
    private void Clicked(object? sender, EventArgs e) => restore();
    public void Dispose()
    {
        if (tray is null) return;
        tray.Clicked -= Clicked;
        if (Application.Current is { } app) TrayIcon.GetIcons(app)?.Remove(tray);
        tray.Dispose();
    }
}
