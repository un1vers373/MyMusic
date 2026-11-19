using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMusic.DTOs;
using MyMusic.Services;

namespace MyMusic.Controllers;

[ApiController]
[Authorize]
[Route("api/playlists")]
public class PlaylistsController(PlaylistService playlists) : ControllerBase
{
    private Guid UserId => User.GetUserId();

    [HttpGet]
    public Task<List<PlaylistSummaryDto>> List(CancellationToken ct) => playlists.ListAsync(UserId, ct);

    [HttpGet("{id:guid}")]
    public Task<PlaylistDetailDto> Get(Guid id, CancellationToken ct) => playlists.GetAsync(UserId, id, ct);

    [HttpPost]
    public Task<PlaylistSummaryDto> Create(PlaylistRequest req, CancellationToken ct) =>
        playlists.CreateAsync(UserId, req, ct);

    [HttpPut("{id:guid}")]
    public Task<PlaylistSummaryDto> Update(Guid id, PlaylistRequest req, CancellationToken ct) =>
        playlists.UpdateAsync(UserId, id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await playlists.DeleteAsync(UserId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/tracks")]
    public async Task<IActionResult> AddTrack(Guid id, AddTrackToPlaylistRequest req, CancellationToken ct)
    {
        await playlists.AddTrackAsync(UserId, id, req.TrackId, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/tracks/{trackId:guid}")]
    public async Task<IActionResult> RemoveTrack(Guid id, Guid trackId, CancellationToken ct)
    {
        await playlists.RemoveTrackAsync(UserId, id, trackId, ct);
        return NoContent();
    }
}
