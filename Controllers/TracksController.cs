using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMusic.DTOs;
using MyMusic.Services;

namespace MyMusic.Controllers;

public class UploadTrackForm
{
    public IFormFile File { get; set; } = null!;
    public IFormFile? Cover { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
}

public class CoverForm
{
    public IFormFile Cover { get; set; } = null!;
}

[ApiController]
[Authorize]
[Route("api/tracks")]
public class TracksController(TrackService tracks, LibraryService library, IFileStorage storage) : ControllerBase
{
    private Guid UserId => User.GetUserId();

    [HttpGet]
    public Task<List<TrackDto>> List([FromQuery] string? q, CancellationToken ct) =>
        tracks.ListAsync(UserId, q, ct);

    [HttpPost]
    public async Task<ActionResult<TrackDto>> Upload([FromForm] UploadTrackForm form, CancellationToken ct) =>
        await tracks.UploadAsync(UserId, form.File, form.Cover, form.Title, form.Artist, form.Album, ct);

    [HttpPut("{id:guid}")]
    public Task<TrackDto> Update(Guid id, UpdateTrackRequest req, CancellationToken ct) =>
        tracks.UpdateAsync(UserId, id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tracks.DeleteAsync(UserId, id, ct);
        return NoContent();
    }

    // ----- Streaming (HTTP Range supported) -----

    [HttpGet("{id:guid}/stream")]
    [HttpHead("{id:guid}/stream")]
    public async Task<IActionResult> Stream(Guid id, CancellationToken ct)
    {
        var track = await tracks.GetOwnedAsync(UserId, id, ct);
        await RangeStreamer.WriteAsync(HttpContext, storage, track.StorageKey, track.ContentType, track.SizeBytes, ct);
        return new EmptyResult();
    }

    // ----- Covers -----

    [HttpGet("{id:guid}/cover")]
    public async Task<IActionResult> GetCover(Guid id, CancellationToken ct)
    {
        var track = await tracks.GetOwnedAsync(UserId, id, ct);
        if (track.CoverKey == null) throw new AppException(404, "This track has no cover.");
        var info = await storage.GetInfoAsync(track.CoverKey, ct)
                   ?? throw new AppException(404, "Cover is missing from storage.");
        await RangeStreamer.WriteAsync(HttpContext, storage, track.CoverKey,
            track.CoverContentType ?? "image/jpeg", info.Length, ct);
        return new EmptyResult();
    }

    [HttpPut("{id:guid}/cover")]
    public Task<TrackDto> SetCover(Guid id, [FromForm] CoverForm form, CancellationToken ct) =>
        tracks.SetCoverAsync(UserId, id, form.Cover, ct);

    [HttpDelete("{id:guid}/cover")]
    public Task<TrackDto> RemoveCover(Guid id, CancellationToken ct) =>
        tracks.RemoveCoverAsync(UserId, id, ct);

    // ----- Likes and play history -----

    [HttpPut("{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id, CancellationToken ct)
    {
        await library.SetLikeAsync(UserId, id, true, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/like")]
    public async Task<IActionResult> Unlike(Guid id, CancellationToken ct)
    {
        await library.SetLikeAsync(UserId, id, false, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/played")]
    public async Task<IActionResult> Played(Guid id, CancellationToken ct)
    {
        await library.RecordPlayAsync(UserId, id, ct);
        return NoContent();
    }
}
