using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Data;

/// <summary>One club in a name pack (only the fields that are given replace the game's own).</summary>
public class TeamNameOverride
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Short { get; set; }
    public string? City { get; set; }
    public string? Stadium { get; set; }
    public List<string>? Colors { get; set; }
}

public class CodeName
{
    public string? Code { get; set; }
    public string? Name { get; set; }
}

/// <summary>A name pack file (JSON). Made by players; the game itself only ships fictional names and a template.</summary>
public class NamePackFile
{
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public List<TeamNameOverride> Teams { get; set; } = new();
    public List<CodeName> Leagues { get; set; } = new();
    public List<CodeName> Competitions { get; set; } = new();
}

/// <summary>All active packs merged (later files win), by code.</summary>
public partial class NameOverlay
{
    public Dictionary<string, TeamNameOverride> Teams { get; } = new();
    public Dictionary<string, string> Leagues { get; } = new();
    public Dictionary<string, string> Competitions { get; } = new();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorRegex();
    public static bool IsColor(string? c) => c != null && ColorRegex().IsMatch(c);
}

/// <summary>
/// Name packs (1.3): JSON files in the "name-packs" folder next to the save file (%LOCALAPPDATA%\DugoutDeck\name-packs). Active packs end
/// in .json, switched-off packs in .json.off. They survive game updates and apply to every save, including running careers.
/// Teams changed in the in-game editor keep their editor names.
/// </summary>
public class NamePacks(string folder, string seedPath)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Folder { get; } = folder;

    public record PackInfo(string File, string Name, string? Author, int Teams, int Leagues, int Competitions, bool Enabled, string? Error);

    public List<PackInfo> List()
    {
        if (!Directory.Exists(Folder)) return new();
        return Directory.GetFiles(Folder).Where(f => f.EndsWith(".json") || f.EndsWith(".json.off")).OrderBy(f => f)
            .Select(path =>
            {
                var file = Path.GetFileName(path);
                var enabled = file.EndsWith(".json");
                try
                {
                    var p = JsonSerializer.Deserialize<NamePackFile>(File.ReadAllText(path), Json) ?? new NamePackFile();
                    return new PackInfo(file, p.Name ?? file, p.Author, p.Teams.Count, p.Leagues.Count, p.Competitions.Count, enabled, null);
                }
                catch (JsonException ex) { return new PackInfo(file, file, null, 0, 0, 0, enabled, "Not a valid name pack: " + ex.Message); }
            }).ToList();
    }

    public NameOverlay LoadOverlay()
    {
        var o = new NameOverlay();
        if (!Directory.Exists(Folder)) return o;
        foreach (var path in Directory.GetFiles(Folder, "*.json").OrderBy(f => f))
        {
            NamePackFile? p;
            try { p = JsonSerializer.Deserialize<NamePackFile>(File.ReadAllText(path), Json); }
            catch (JsonException) { continue; }
            if (p == null) continue;
            foreach (var t in p.Teams.Where(t => !string.IsNullOrWhiteSpace(t.Code))) o.Teams[t.Code!] = Clean(t);
            foreach (var l in p.Leagues.Where(l => !string.IsNullOrWhiteSpace(l.Code) && !string.IsNullOrWhiteSpace(l.Name))) o.Leagues[l.Code!] = Cut(l.Name!, 60);
            foreach (var c in p.Competitions.Where(c => !string.IsNullOrWhiteSpace(c.Code) && !string.IsNullOrWhiteSpace(c.Name))) o.Competitions[c.Code!] = Cut(c.Name!, 60);
        }
        return o;
    }

    private static string Cut(string s, int max) => s.Trim().Length > max ? s.Trim()[..max] : s.Trim();

    private static TeamNameOverride Clean(TeamNameOverride t) => new()
    {
        Code = t.Code, Name = t.Name == null ? null : Cut(t.Name, 40), Short = t.Short == null ? null : Cut(t.Short, 4).ToUpperInvariant(),
        City = t.City == null ? null : Cut(t.City, 40), Stadium = t.Stadium == null ? null : Cut(t.Stadium, 60), Colors = t.Colors
    };

    private static string SafeName(string name)
    {
        var s = Regex.Replace(Path.GetFileNameWithoutExtension(name), "[^A-Za-z0-9 _.-]", "").Trim();
        return string.IsNullOrEmpty(s) ? "name-pack" : s.Length > 50 ? s[..50] : s;
    }

    /// <summary>Checks and stores a pack (from the Settings screen). Returns the file name.</summary>
    public string Import(string fileName, string content)
    {
        NamePackFile? p;
        try { p = JsonSerializer.Deserialize<NamePackFile>(content, Json); }
        catch (JsonException ex) { throw new Game.GameRuleException("This is not a valid name pack: " + ex.Message); }
        if (p == null || p.Teams.Count + p.Leagues.Count + p.Competitions.Count == 0)
            throw new Game.GameRuleException("The file has no teams, leagues or competitions.");
        Directory.CreateDirectory(Folder);
        var file = SafeName(fileName) + ".json";
        File.WriteAllText(Path.Combine(Folder, file), JsonSerializer.Serialize(p, Json));
        File.Delete(Path.Combine(Folder, file + ".off"));
        return file;
    }

    private string PathOf(string file)
    {
        var name = Path.GetFileName(file);
        var path = Path.Combine(Folder, name);
        if (!File.Exists(path)) throw new Game.GameRuleException("Name pack not found.");
        return path;
    }

    public void SetEnabled(string file, bool enabled)
    {
        var path = PathOf(file);
        var on = path.EndsWith(".json");
        if (on == enabled) return;
        var target = enabled ? path[..^4] : path + ".off";
        if (File.Exists(target)) File.Delete(target);
        File.Move(path, target);
    }

    public void Delete(string file) => File.Delete(PathOf(file));

    public void DisableAll()
    {
        foreach (var p in List().Where(p => p.Enabled)) SetEnabled(p.File, false);
    }

    /// <summary>Writes the names currently in the game as a pack (a template to edit and share). Returns file name and content.</summary>
    public async Task<(string File, string Content)> ExportAsync(AppDbContext db)
    {
        var pack = new NamePackFile
        {
            Name = "My name pack", Author = "", Version = "1",
            Teams = (await db.Teams.Where(t => t.Code != null).OrderBy(t => t.Code).ToListAsync())
                .Select(t => new TeamNameOverride { Code = t.Code, Name = t.Name, Short = t.ShortName, City = t.City, Stadium = t.Stadium, Colors = [t.PrimaryColor, t.SecondaryColor] }).ToList(),
            Leagues = (await db.Leagues.Where(l => l.Code != null).OrderBy(l => l.Code).ToListAsync()).Select(l => new CodeName { Code = l.Code, Name = l.Name }).ToList(),
            Competitions = (await db.Competitions.Where(c => c.Type != Entities.CompetitionType.League).OrderBy(c => c.Code).ToListAsync())
                .Select(c => new CodeName { Code = c.Code, Name = c.Name }).ToList()
        };
        Directory.CreateDirectory(Folder);
        var file = $"export-{DateTime.Now:yyyyMMdd-HHmm}.json.off";
        var content = JsonSerializer.Serialize(pack, Json);
        await File.WriteAllTextAsync(Path.Combine(Folder, file), content);
        return (file, content);
    }

    /// <summary>Applies the active packs to the save now (the same sync as at start-up).</summary>
    public Task ApplyAsync(AppDbContext db, ILogger logger) => DbSeeder.SeedAsync(db, seedPath, logger, LoadOverlay());
}
