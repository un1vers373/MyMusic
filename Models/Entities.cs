namespace MyMusic.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string NormalizedUsername { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Track
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public int DurationSeconds { get; set; }

    /// <summary>Key of the audio object inside the configured file storage (local folder or S3 bucket).</summary>
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "audio/mpeg";
    public long SizeBytes { get; set; }
    public string OriginalFileName { get; set; } = "";

    public string? CoverKey { get; set; }
    public string? CoverContentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LikedTrack
{
    public Guid UserId { get; set; }
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;
    public DateTime LikedAt { get; set; } = DateTime.UtcNow;
}

public class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<PlaylistTrack> PlaylistTracks { get; set; } = new();
}

public class PlaylistTrack
{
    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;
    public int Position { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class PlayHistory
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TrackId { get; set; }
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}
