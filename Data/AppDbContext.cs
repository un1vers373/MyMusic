using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyMusic.Models;

namespace MyMusic.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<LikedTrack> LikedTracks => Set<LikedTrack>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();
    public DbSet<PlayHistory> PlayHistory => Set<PlayHistory>();

    // Auth cookie keys live in the database, so users stay signed in after a container restart.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.NormalizedUsername).IsUnique();
            e.Property(x => x.Username).HasMaxLength(32);
            e.Property(x => x.NormalizedUsername).HasMaxLength(32);
            e.Property(x => x.PasswordHash).HasMaxLength(512);
        });

        b.Entity<Track>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Artist).HasMaxLength(200);
            e.Property(x => x.Album).HasMaxLength(200);
            e.Property(x => x.StorageKey).HasMaxLength(300);
            e.Property(x => x.CoverKey).HasMaxLength(300);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.CoverContentType).HasMaxLength(100);
            e.Property(x => x.OriginalFileName).HasMaxLength(300);
        });

        b.Entity<LikedTrack>(e =>
        {
            e.HasKey(x => new { x.UserId, x.TrackId });
            e.HasOne(x => x.Track).WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Playlist>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasMany(x => x.PlaylistTracks).WithOne(x => x.Playlist)
                .HasForeignKey(x => x.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PlaylistTrack>(e =>
        {
            e.HasKey(x => new { x.PlaylistId, x.TrackId });
            e.HasOne(x => x.Track).WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PlayHistory>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.PlayedAt });
            e.HasOne<Track>().WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
