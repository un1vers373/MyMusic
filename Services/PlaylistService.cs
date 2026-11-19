using Microsoft.EntityFrameworkCore;
using MyMusic.Data;
using MyMusic.DTOs;
using MyMusic.Models;

namespace MyMusic.Services;

public class PlaylistService(AppDbContext db)
{
    public async Task<List<PlaylistSummaryDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        var playlists = await db.Playlists.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
        return await BuildSummariesAsync(playlists, ct);
    }

    public async Task<PlaylistDetailDto> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var playlist = await FindOwnedAsync(userId, id, ct, track: false);
        var summary = (await BuildSummariesAsync([playlist], ct))[0];

        var entries = await db.PlaylistTracks.AsNoTracking()
            .Where(pt => pt.PlaylistId == id)
            .OrderBy(pt => pt.Position)
            .Include(pt => pt.Track)
            .ToListAsync(ct);
        var liked = await db.LikedIdsAsync(userId, ct);
        var tracks = entries.Select(pt => TrackDto.From(pt.Track, liked.Contains(pt.TrackId))).ToList();
        return new PlaylistDetailDto(summary, tracks);
    }

    public async Task<PlaylistSummaryDto> CreateAsync(Guid userId, PlaylistRequest req, CancellationToken ct)
    {
        var (name, description) = Validate(req);
        var playlist = new Playlist { UserId = userId, Name = name, Description = description };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
        return (await BuildSummariesAsync([playlist], ct))[0];
    }

    public async Task<PlaylistSummaryDto> UpdateAsync(Guid userId, Guid id, PlaylistRequest req, CancellationToken ct)
    {
        var (name, description) = Validate(req);
        var playlist = await FindOwnedAsync(userId, id, ct);
        playlist.Name = name;
        playlist.Description = description;
        playlist.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await BuildSummariesAsync([playlist], ct))[0];
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var playlist = await FindOwnedAsync(userId, id, ct);
        db.Playlists.Remove(playlist);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddTrackAsync(Guid userId, Guid playlistId, Guid trackId, CancellationToken ct)
    {
        var playlist = await FindOwnedAsync(userId, playlistId, ct);

        if (!await db.Tracks.AnyAsync(t => t.Id == trackId && t.UserId == userId, ct))
            throw new AppException(404, "Track not found.");

        if (await db.PlaylistTracks.AnyAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId, ct))
            throw new AppException(409, "This song is already in the playlist.");

        var last = await db.PlaylistTracks.Where(pt => pt.PlaylistId == playlistId)
            .MaxAsync(pt => (int?)pt.Position, ct);

        db.PlaylistTracks.Add(new PlaylistTrack
        {
            PlaylistId = playlistId,
            TrackId = trackId,
            Position = (last ?? -1) + 1,
        });
        playlist.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveTrackAsync(Guid userId, Guid playlistId, Guid trackId, CancellationToken ct)
    {
        var playlist = await FindOwnedAsync(userId, playlistId, ct);
        var entry = await db.PlaylistTracks
            .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId, ct)
            ?? throw new AppException(404, "This song is not in the playlist.");
        db.PlaylistTracks.Remove(entry);
        playlist.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Playlist> FindOwnedAsync(Guid userId, Guid id, CancellationToken ct, bool track = true)
    {
        var query = track ? db.Playlists : db.Playlists.AsNoTracking();
        return await query.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct)
               ?? throw new AppException(404, "Playlist not found.");
    }

    private static (string Name, string? Description) Validate(PlaylistRequest req)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 100) throw new AppException(400, "Playlist name must be 1–100 characters.");
        var description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        if (description is { Length: > 300 }) throw new AppException(400, "Description is limited to 300 characters.");
        return (name, description);
    }

    private async Task<List<PlaylistSummaryDto>> BuildSummariesAsync(List<Playlist> playlists, CancellationToken ct)
    {
        if (playlists.Count == 0) return [];
        var ids = playlists.Select(p => p.Id).ToList();

        var rows = await db.PlaylistTracks.AsNoTracking()
            .Where(pt => ids.Contains(pt.PlaylistId))
            .OrderBy(pt => pt.Position)
            .Select(pt => new
            {
                pt.PlaylistId,
                pt.TrackId,
                Seconds = pt.Track.DurationSeconds,
                HasCover = pt.Track.CoverKey != null,
            })
            .ToListAsync(ct);

        var byPlaylist = rows.GroupBy(r => r.PlaylistId).ToDictionary(g => g.Key, g => g.ToList());

        return playlists.Select(p =>
        {
            byPlaylist.TryGetValue(p.Id, out var items);
            var count = items?.Count ?? 0;
            var seconds = items?.Sum(i => i.Seconds) ?? 0;
            var covers = items?.Where(i => i.HasCover).Select(i => i.TrackId).Take(4).ToList() ?? new List<Guid>();
            return new PlaylistSummaryDto(p.Id, p.Name, p.Description, count, seconds, covers);
        }).ToList();
    }
}
