using FootballCards.Api;

// Web server mode: `dotnet run` (API on http://localhost:5080).
// If the client has been built (client/dist), the server also serves the game itself.
var contentRoot = Directory.GetCurrentDirectory();
var app = GameServer.Build(args, new GameServerOptions
{
    ContentRoot = contentRoot,
    ClientPath = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "client", "dist"))
});
await GameServer.InitializeDatabaseAsync(app);
app.Run();
