using Microsoft.EntityFrameworkCore;
using MyMusic.Data;
using MyMusic.DTOs;
using MyMusic.Models;

namespace MyMusic.Services;

public static class TrackQueries
{
    public static async Task<HashSet<Guid>> LikedIdsAsync(this AppDbContext db, Guid userId, CancellationToken ct) =>
        (await db.LikedTracks.AsNoTracking().Where(l => l.UserId == userId).Select(l => l.TrackId).ToListAsync(ct))
        .ToHashSet();

    public static async Task<List<TrackDto>> ToDtosAsync(
        this IQueryable<Track> query, AppDbContext db, Guid userId, CancellationToken ct)
    {
        var tracks = await query.AsNoTracking().ToListAsync(ct);
        var liked = await db.LikedIdsAsync(userId, ct);
        return tracks.Select(t => TrackDto.From(t, liked.Contains(t.Id))).ToList();
    }

    public static bool Matches(Track t, string q) =>
        t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
        t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase) ||
        t.Album.Contains(q, StringComparison.OrdinalIgnoreCase);
}
