using FootballCards.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FootballCards.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CardDefinition> Cards => Set<CardDefinition>();
    public DbSet<Career> Careers => Set<Career>();
    public DbSet<CareerSeason> CareerSeasons => Set<CareerSeason>();
    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<CareerMessage> CareerMessages => Set<CareerMessage>();
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<League>().HasIndex(l => l.Name).IsUnique();
        b.Entity<Team>().HasIndex(t => t.Name).IsUnique();
        b.Entity<Team>().Ignore(t => t.Overall);
        b.Entity<Team>().HasOne(t => t.League).WithMany(l => l.Teams)
            .HasForeignKey(t => t.LeagueId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Competition>().HasIndex(c => c.Code).IsUnique();
        b.Entity<CardDefinition>().HasIndex(c => c.Code).IsUnique();

        b.Entity<Career>().HasOne(c => c.Team).WithMany().HasForeignKey(c => c.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Career>().HasMany(c => c.Seasons).WithOne(s => s.Career!)
            .HasForeignKey(s => s.CareerId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Career>().HasMany(c => c.Messages).WithOne()
            .HasForeignKey(m => m.CareerId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<CareerSeason>().HasMany(s => s.Fixtures).WithOne(f => f.CareerSeason!)
            .HasForeignKey(f => f.CareerSeasonId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Fixture>().HasOne(f => f.HomeTeam).WithMany().HasForeignKey(f => f.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Fixture>().HasOne(f => f.AwayTeam).WithMany().HasForeignKey(f => f.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Fixture>().HasOne(f => f.Competition).WithMany().HasForeignKey(f => f.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Fixture>().HasIndex(f => new { f.CareerSeasonId, f.Week });
    }
}
