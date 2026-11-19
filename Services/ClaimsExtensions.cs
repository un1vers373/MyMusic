using System.Security.Claims;

namespace MyMusic.Services;

public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : throw new AppException(401, "Not signed in.");
    }
}
