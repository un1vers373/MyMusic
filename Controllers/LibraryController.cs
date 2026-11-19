using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMusic.DTOs;
using MyMusic.Services;

namespace MyMusic.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class LibraryController(LibraryService library) : ControllerBase
{
    private Guid UserId => User.GetUserId();

    [HttpGet("home")]
    public Task<HomeDto> Home(CancellationToken ct) => library.HomeAsync(UserId, ct);

    [HttpGet("search")]
    public Task<SearchResultDto> Search([FromQuery] string? q, CancellationToken ct) =>
        library.SearchAsync(UserId, q, ct);

    [HttpGet("likes")]
    public Task<List<TrackDto>> Likes(CancellationToken ct) => library.LikedAsync(UserId, ct);
}
