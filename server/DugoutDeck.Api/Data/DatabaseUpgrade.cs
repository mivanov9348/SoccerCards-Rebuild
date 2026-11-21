using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DugoutDeck.Api.Data;

/// <summary>
/// Brings any save file up to date with EF Core migrations (Data/Migrations), so saves survive game updates.
/// Save files made before migrations existed (version 6 and older) already have the tables of the first migration:
/// that migration is marked as applied and only the newer ones run. Before migrating an existing save, a backup copy
/// "dugout-deck.db.backup-&lt;date&gt;" is written next to it.
/// </summary>
public static class DatabaseUpgrade
{
    public static async Task RunAsync(AppDbContext db, ILogger logger)
    {
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        var file = new SqliteConnectionStringBuilder(conn.ConnectionString).DataSource;
        var existed = File.Exists(file) && new FileInfo(file).Length > 0;

        await conn.OpenAsync();
        try
        {
            if (await HasTable(conn, "Teams") && !await HasTable(conn, HistoryRepository.DefaultTableName))
            {
                var first = db.Database.GetMigrations().First();
                await Exec(conn, $"CREATE TABLE \"{HistoryRepository.DefaultTableName}\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);");
                await Exec(conn, $"INSERT INTO \"{HistoryRepository.DefaultTableName}\" VALUES ('{first}', '10.0.15');");
                logger.LogInformation("Save file from an older version: marked {Migration} as applied.", first);
            }
        }
        finally
        {
            await conn.CloseAsync();
        }

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0) return;
        if (existed)
        {
            var backup = $"{file}.backup-{DateTime.Now:yyyyMMdd-HHmmss}";
            SqliteConnection.ClearAllPools();
            File.Copy(file, backup, overwrite: true);
            logger.LogInformation("Backup before updating the save file: {Backup}", backup);
        }
        await db.Database.MigrateAsync();
        logger.LogInformation("Save file updated: {Migrations}", string.Join(", ", pending));
    }

    private static async Task<bool> HasTable(SqliteConnection conn, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $n";
        cmd.Parameters.AddWithValue("$n", name);
        return (long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
    }

    private static async Task Exec(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
