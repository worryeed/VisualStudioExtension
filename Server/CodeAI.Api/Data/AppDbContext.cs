using CodeAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeAI.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<QueryHistory> QueryHistories => Set<QueryHistory>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<QueryHistory>()
          .HasKey(q => q.Id);

        mb.Entity<QueryHistory>()
          .HasOne(q => q.AppUser)
          .WithMany()
          .HasForeignKey(q => q.AppUserId);

        mb.Entity<RefreshToken>()
          .HasIndex(r => r.Token)
          .IsUnique();
    }
}