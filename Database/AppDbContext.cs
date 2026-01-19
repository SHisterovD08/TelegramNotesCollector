using Microsoft.EntityFrameworkCore;
using TelegramNotesCollector.Models;

namespace TelegramNotesCollector.Database
{
    public class AppDbContext : DbContext
    {
        public DbSet<Note> Notes { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Note>()
                .HasIndex(n => new { n.UserId, n.SourceId })
                .IsUnique();
                
            modelBuilder.Entity<Note>()
                .HasIndex(n => n.UserId);
                
            modelBuilder.Entity<Note>()
                .HasIndex(n => n.Platform);
                
            modelBuilder.Entity<Note>()
                .HasIndex(n => n.CreatedAt);
                
            modelBuilder.Entity<Note>()
                .Property(n => n.Tags)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
                
            modelBuilder.Entity<UserSettings>()
                .HasKey(u => u.UserId);
                
            modelBuilder.Entity<UserSettings>()
                .Property(u => u.Keywords)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
                
            modelBuilder.Entity<UserSettings>()
                .Property(u => u.BlockedSources)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
                
            modelBuilder.Entity<Subscription>()
                .HasIndex(s => new { s.UserId, s.Platform, s.SourceIdentifier })
                .IsUnique();
                
            modelBuilder.Entity<Subscription>()
                .Property(s => s.Filters)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
        }
    }
}
