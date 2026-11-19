using MyMusic.Models;

namespace MyMusic.DTOs;

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);
public record UserDto(Guid Id, string Username);
public record AppConfigDto(bool RegistrationOpen, int MaxAudioMb, int MaxCoverMb);

public record TrackDto(
    Guid Id, string Title, string Artist, string Album,
    int Duration, bool HasCover, bool IsLiked, DateTime CreatedAt)
{
    public static TrackDto From(Track t, bool liked) =>
        new(t.Id, t.Title, t.Artist, t.Album, t.DurationSeconds, t.CoverKey != null, liked, t.CreatedAt);
}

public record UpdateTrackRequest(string? Title, string? Artist, string? Album);

public record PlaylistRequest(string Name, string? Description);
public record AddTrackToPlaylistRequest(Guid TrackId);

public record PlaylistSummaryDto(
    Guid Id, string Name, string? Description, int TrackCount, int TotalSeconds, List<Guid> CoverTrackIds);
public record PlaylistDetailDto(PlaylistSummaryDto Playlist, List<TrackDto> Tracks);

public record ArtistDto(string Name, int TrackCount, Guid? CoverTrackId);
public record AlbumDto(string Name, string Artist, int TrackCount, Guid? CoverTrackId);

public record SearchResultDto(
    List<TrackDto> Tracks, List<ArtistDto> Artists, List<AlbumDto> Albums, List<PlaylistSummaryDto> Playlists);

public record HomeDto(
    List<TrackDto> RecentlyPlayed, List<TrackDto> RecentlyAdded, List<TrackDto> Recommended,
    List<PlaylistSummaryDto> Playlists, int TrackCount);
