using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Data;
using MyMusic.DTOs;
using MyMusic.Models;

namespace MyMusic.Services;

public class TrackService(
    AppDbContext db, IFileStorage storage, IOptions<UploadOptions> upload, ILogger<TrackService> log)
{
    public async Task<List<TrackDto>> ListAsync(Guid userId, string? q, CancellationToken ct)
    {
        var tracks = await db.Tracks.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var liked = await db.LikedIdsAsync(userId, ct);

        q = q?.Trim();
        IEnumerable<Track> result = tracks;
        if (!string.IsNullOrEmpty(q)) result = tracks.Where(t => TrackQueries.Matches(t, q));
        return result.Select(t => TrackDto.From(t, liked.Contains(t.Id))).ToList();
    }

    public async Task<Track> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct) =>
        await db.Tracks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
        ?? throw new AppException(404, "Track not found.");

    public async Task<TrackDto> GetDtoAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var list = await db.Tracks.Where(t => t.Id == id && t.UserId == userId).ToDtosAsync(db, userId, ct);
        return list.FirstOrDefault() ?? throw new AppException(404, "Track not found.");
    }

    public async Task<TrackDto> UploadAsync(
        Guid userId, IFormFile? file, IFormFile? cover, string? title, string? artist, string? album,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0) throw new AppException(400, "No file was uploaded.");

        var maxAudio = upload.Value.MaxAudioMb * 1024L * 1024L;
        if (file.Length > maxAudio)
            throw new AppException(413, $"File is larger than {upload.Value.MaxAudioMb} MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".mp3") throw new AppException(400, "Only MP3 files are supported.");

        await using var audio = file.OpenReadStream();
        if (!AudioInspector.LooksLikeMp3(audio))
            throw new AppException(400, "This file is not a valid MP3.");

        var meta = AudioInspector.TryRead(audio, file.FileName)
                   ?? throw new AppException(400, "This file could not be read as an MP3.");

        // Cover: an uploaded image wins over the one embedded in the tags.
        (byte[] Data, string ContentType, string Ext)? coverData = null;
        if (cover is { Length: > 0 })
        {
            coverData = await ReadCoverAsync(cover, ct);
        }
        else if (meta.Cover != null && meta.Cover.Length <= upload.Value.MaxCoverMb * 1024L * 1024L)
        {
            var sniff = AudioInspector.SniffImage(meta.Cover);
            if (sniff != null) coverData = (meta.Cover, sniff.Value.ContentType, sniff.Value.Ext);
        }

        var id = Guid.NewGuid();
        var track = new Track
        {
            Id = id,
            UserId = userId,
            Title = Clean(title, 300).NullIfEmpty() ?? Clean(meta.Title, 300).NullIfEmpty()
                    ?? Clean(Path.GetFileNameWithoutExtension(file.FileName), 300).NullIfEmpty() ?? "Untitled",
            Artist = Clean(artist, 200).NullIfEmpty() ?? Clean(meta.Artist, 200).NullIfEmpty() ?? "Unknown artist",
            Album = Clean(album, 200).NullIfEmpty() ?? Clean(meta.Album, 200),
            DurationSeconds = meta.DurationSeconds,
            StorageKey = $"audio/{userId:N}/{id:N}.mp3",
            ContentType = "audio/mpeg",
            SizeBytes = file.Length,
            OriginalFileName = Clean(file.FileName, 300),
        };

        var savedKeys = new List<string>();
        try
        {
            audio.Position = 0;
            await storage.SaveAsync(track.StorageKey, audio, track.ContentType, ct);
            savedKeys.Add(track.StorageKey);

            if (coverData is { } c)
            {
                track.CoverKey = $"covers/{userId:N}/{id:N}{c.Ext}";
                track.CoverContentType = c.ContentType;
                using var ms = new MemoryStream(c.Data);
                await storage.SaveAsync(track.CoverKey, ms, c.ContentType, ct);
                savedKeys.Add(track.CoverKey);
            }

            db.Tracks.Add(track);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var key in savedKeys) await TryDeleteAsync(key);
            throw;
        }

        return TrackDto.From(track, false);
    }

    public async Task<TrackDto> UpdateAsync(Guid userId, Guid id, UpdateTrackRequest req, CancellationToken ct)
    {
        var track = await GetOwnedAsync(userId, id, ct);

        if (req.Title != null)
        {
            var title = Clean(req.Title, 300);
            if (title.Length == 0) throw new AppException(400, "Title cannot be empty.");
            track.Title = title;
        }
        if (req.Artist != null)
        {
            var artist = Clean(req.Artist, 200);
            track.Artist = artist.Length == 0 ? "Unknown artist" : artist;
        }
        if (req.Album != null) track.Album = Clean(req.Album, 200);

        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(userId, id, ct);
    }

    public async Task<TrackDto> SetCoverAsync(Guid userId, Guid id, IFormFile? cover, CancellationToken ct)
    {
        if (cover == null || cover.Length == 0) throw new AppException(400, "No image was uploaded.");
        var track = await GetOwnedAsync(userId, id, ct);
        var (data, contentType, ext) = await ReadCoverAsync(cover, ct);

        var oldKey = track.CoverKey;
        var newKey = $"covers/{userId:N}/{id:N}-{Guid.NewGuid():N}{ext}";
        using (var ms = new MemoryStream(data))
            await storage.SaveAsync(newKey, ms, contentType, ct);

        track.CoverKey = newKey;
        track.CoverContentType = contentType;
        await db.SaveChangesAsync(ct);

        if (oldKey != null) await TryDeleteAsync(oldKey);
        return await GetDtoAsync(userId, id, ct);
    }

    public async Task<TrackDto> RemoveCoverAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var track = await GetOwnedAsync(userId, id, ct);
        var oldKey = track.CoverKey;
        track.CoverKey = null;
        track.CoverContentType = null;
        await db.SaveChangesAsync(ct);
        if (oldKey != null) await TryDeleteAsync(oldKey);
        return await GetDtoAsync(userId, id, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var track = await GetOwnedAsync(userId, id, ct);
        var keys = new[] { track.StorageKey, track.CoverKey };

        db.Tracks.Remove(track); // likes, playlist entries and history are removed by cascade
        await db.SaveChangesAsync(ct);

        foreach (var key in keys)
            if (key != null) await TryDeleteAsync(key);
    }

    private async Task<(byte[] Data, string ContentType, string Ext)> ReadCoverAsync(IFormFile file, CancellationToken ct)
    {
        var max = upload.Value.MaxCoverMb * 1024L * 1024L;
        if (file.Length > max) throw new AppException(413, $"Cover is larger than {upload.Value.MaxCoverMb} MB.");

        using var ms = new MemoryStream();
        await using (var s = file.OpenReadStream()) await s.CopyToAsync(ms, ct);
        var data = ms.ToArray();

        var sniff = AudioInspector.SniffImage(data)
                    ?? throw new AppException(400, "Cover must be a JPEG, PNG or WebP image.");
        return (data, sniff.ContentType, sniff.Ext);
    }

    private async Task TryDeleteAsync(string key)
    {
        try { await storage.DeleteAsync(key, CancellationToken.None); }
        catch (Exception ex) { log.LogWarning(ex, "Could not delete storage object {Key}", key); }
    }

    private static string Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var cleaned = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return cleaned.Length > max ? cleaned[..max] : cleaned;
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
