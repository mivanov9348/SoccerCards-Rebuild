using System.Net;
using System.Net.Sockets;
using DugoutDeck.Api;
using Photino.NET;

namespace DugoutDeck.Desktop;

/// <summary>
/// Desktop version of Dugout Deck: runs the game server inside the app on a private local port
/// and shows the game in a native window (WebView2 on Windows). No browser, no console.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Saves live in %LOCALAPPDATA%\DugoutDeck (one database per Windows user).
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DugoutDeck");
        Directory.CreateDirectory(dataDir);

        try
        {
            var url = $"http://127.0.0.1:{FreePort()}";
            var app = GameServer.Build(args, new GameServerOptions
            {
                ContentRoot = AppContext.BaseDirectory,
                ClientPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                ConnectionString = $"Data Source={Path.Combine(dataDir, "dugout-deck.db")}",
                Urls = url
            });
            GameServer.InitializeDatabaseAsync(app).GetAwaiter().GetResult();
            app.StartAsync().GetAwaiter().GetResult();

            var window = new PhotinoWindow()
                .SetTitle("Dugout Deck")
                .SetUseOsDefaultSize(false)
                .SetSize(1366, 860)
                .Center()
                .SetResizable(true)
                .SetContextMenuEnabled(false)
                .SetDevToolsEnabled(false);
            var icon = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(icon)) window.SetIconFile(icon);

            window.Load(url);
            window.WaitForClose();

            app.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(dataDir, "error.log"), $"{DateTime.Now:u}\n{ex}");
            throw;
        }
    }

    /// <summary>Asks the OS for a free local port, so the app never clashes with other programs.</summary>
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
