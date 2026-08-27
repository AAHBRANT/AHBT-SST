using System.Windows.Forms;

namespace AAHBRANT.SST.AgenteBiometria.Tray;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly WebApplication _app;

    public TrayApplicationContext(WebApplication app)
    {
        _app = app;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Sair", null, (_, _) => Sair());

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
            Text = "AAHBRANT — Agente Biometria",
        };
    }

    private void Sair()
    {
        _trayIcon.Visible = false;
        _ = _app.StopAsync();
        Application.Exit();
    }
}
