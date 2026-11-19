using Microsoft.EntityFrameworkCore;
using MyMusic.Data;
using MyMusic.DTOs;
using MyMusic.Models;

namespace MyMusic.Services;

/// <summary>Home page data, search, likes and play history.</summary>
public class LibraryService(AppDbContext db, PlaylistService playlists)
{
    // ---------- Likes ----------

    public async Task<List<TrackDto>> LikedAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.LikedTracks.AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.LikedAt)
            .Include(l => l.Track)
            .ToListAsync(ct);
        return rows.Select(l => TrackDto.From(l.Track, true)).ToList();
    }

    public async Task SetLikeAsync(Guid userId, Guid trackId, bool liked, CancellationToken ct)
    {
        if (!await db.Tracks.AnyAsync(t => t.Id == trackId && t.UserId == userId, ct))
            throw new AppException(404, "Track not found.");

        var existing = await db.LikedTracks.FindAsync(new object[] { userId, trackId }, ct);
        if (liked && existing == null)
            db.LikedTracks.Add(new LikedTrack { UserId = userId, TrackId = trackId });
        else if (!liked && existing != null)
            db.LikedTracks.Remove(existing);
        else
            return;

        await db.SaveChangesAsync(ct);
    }

    // ---------- Play history ----------

    public async Task RecordPlayAsync(Guid userId, Guid trackId, CancellationToken ct)
    {
        if (!await db.Tracks.AnyAsync(t => t.Id == trackId && t.UserId == userId, ct))
            throw new AppException(404, "Track not found.");

        var last = await db.PlayHistory.Where(h => h.UserId == userId && h.TrackId == trackId)
            .OrderByDescending(h => h.PlayedAt).Select(h => (DateTime?)h.PlayedAt).FirstOrDefaultAsync(ct);
        if (last != null && DateTime.UtcNow - last.Value < TimeSpan.FromSeconds(20)) return;

        db.PlayHistory.Add(new PlayHistory { UserId = userId, TrackId = trackId });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Home ----------

    public async Task<HomeDto> HomeAsync(Guid userId, CancellationToken ct)
    {
        var all = await db.Tracks.AsNoTracking().Where(t => t.UserId == userId).ToListAsync(ct);
        var liked = await db.LikedIdsAsync(userId, ct);
        TrackDto Dto(Track t) => TrackDto.From(t, liked.Contains(t.Id));
        var byId = all.ToDictionary(t => t.Id);

        var recentIds = await db.PlayHistory.AsNoTracking()
            .Where(h => h.UserId == userId)
            .GroupBy(h => h.TrackId)
            .Select(g => new { Id = g.Key, Last = g.Max(x => x.PlayedAt) })
            .OrderByDescending(x => x.Last)
            .Take(12)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var plays = await db.PlayHistory.AsNoTracking()
            .Where(h => h.UserId == userId)
            .GroupBy(h => h.TrackId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var recent = recentIds.Where(byId.ContainsKey).Select(id => Dto(byId[id])).ToList();
        var added = all.OrderByDescending(t => t.CreatedAt).Take(12).Select(Dto).ToList();

        // Recommendations: artists you like or play often, favouring songs you haven't played yet.
        var artistScore = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in all)
        {
            var s = (liked.Contains(t.Id) ? 3.0 : 0) + Math.Min(plays.GetValueOrDefault(t.Id), 10) * 0.5;
            artistScore[t.Artist] = artistScore.GetValueOrDefault(t.Artist) + s;
        }
        var skip = recentIds.Take(6).ToHashSet();
        var recommended = all
            .Where(t => !skip.Contains(t.Id))
            .Select(t => new
            {
                Track = t,
                Score = artistScore.GetValueOrDefault(t.Artist)
                        + (plays.ContainsKey(t.Id) ? 0 : 2)
                        + Random.Shared.NextDouble() * 2,
            })
            .OrderByDescending(x => x.Score)
            .Take(12)
            .Select(x => Dto(x.Track))
            .ToList();

        return new HomeDto(recent, added, recommended, await playlists.ListAsync(userId, ct), all.Count);
    }

    // ---------- Search ----------

    public async Task<SearchResultDto> SearchAsync(Guid userId, string? q, CancellationToken ct)
    {
        q = (q ?? "").Trim();
        if (q.Length > 100) q = q[..100];
        if (q.Length == 0) return new SearchResultDto([], [], [], []);

        var all = await db.Tracks.AsNoTracking().Where(t => t.UserId == userId).ToListAsync(ct);
        var liked = await db.LikedIdsAsync(userId, ct);

        var matched = all.Where(t => TrackQueries.Matches(t, q))
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();

        var artists = all
            .Where(t => t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase))
            .GroupBy(t => t.Artist, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ArtistDto(g.First().Artist, g.Count(), g.FirstOrDefault(t => t.CoverKey != null)?.Id))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Take(12).ToList();

        var albums = all
            .Where(t => t.Album.Length > 0 &&
                        (t.Album.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                         t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(t => (t.Artist.ToLowerInvariant(), t.Album.ToLowerInvariant()))
            .Select(g => new AlbumDto(g.First().Album, g.First().Artist, g.Count(),
                g.FirstOrDefault(t => t.CoverKey != null)?.Id))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Take(12).ToList();

        var pls = (await playlists.ListAsync(userId, ct))
            .Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        return new SearchResultDto(
            matched.Take(50).Select(t => TrackDto.From(t, liked.Contains(t.Id))).ToList(),
            artists, albums, pls);
    }
}
