using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyMusic.Data;
using MyMusic.DTOs;
using MyMusic.Models;

namespace MyMusic.Services;

public class AuthService(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config)
{
    private static readonly Regex NameRx = new("^[A-Za-z0-9_.-]{3,32}$", RegexOptions.Compiled);
    private static readonly User Dummy = new() { Username = "dummy" };
    private readonly string _dummyHash = hasher.HashPassword(Dummy, "dummy-password");

    /// <summary>When registration is disabled, the very first account can still be created.</summary>
    public async Task<bool> RegistrationOpenAsync(CancellationToken ct)
    {
        if (config.GetValue("Auth:AllowRegistration", true)) return true;
        return !await db.Users.AnyAsync(ct);
    }

    public async Task<User> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        if (!await RegistrationOpenAsync(ct))
            throw new AppException(403, "Registration is closed on this server.");

        var name = (req.Username ?? "").Trim();
        if (!NameRx.IsMatch(name))
            throw new AppException(400, "Username must be 3–32 characters: letters, digits, dot, dash or underscore.");

        var password = req.Password ?? "";
        if (password.Length < 8 || password.Length > 128)
            throw new AppException(400, "Password must be 8–128 characters.");

        var normalized = name.ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedUsername == normalized, ct))
            throw new AppException(409, "That username is taken.");

        var user = new User { Username = name, NormalizedUsername = normalized };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var normalized = (req.Username ?? "").Trim().ToLowerInvariant();
        var password = req.Password ?? "";
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, ct);

        if (user == null)
        {
            hasher.VerifyHashedPassword(Dummy, _dummyHash, password); // keep timing similar
            throw new AppException(401, "Wrong username or password.");
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            throw new AppException(401, "Wrong username or password.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync(ct);
        }
        return user;
    }

    public Task<User?> FindAsync(Guid id, CancellationToken ct) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
}
