using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public sealed class SiteAdminAuthService(IDbContextFactory<AppDbContext> dbFactory)
{
    public const string UserName = "admin";
    public const string DefaultPassword = "admin";
    public const int MinPasswordLength = 4;
    private const string PasswordHashKey = "site.admin.passwordHash";

    public async Task EnsureSeededAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var setting = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == PasswordHashKey);
        if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
            return;

        var hash = HashPassword(DefaultPassword);
        if (setting is null)
        {
            db.SiteSettings.Add(new SiteSetting { Key = PasswordHashKey, Value = hash });
        }
        else
        {
            setting.Value = hash;
        }

        await db.SaveChangesAsync();
    }

    public bool IsUserName(string? username) =>
        string.Equals(username?.Trim(), UserName, StringComparison.OrdinalIgnoreCase);

    public async Task<bool> VerifyAsync(string? username, string? password)
    {
        if (!IsUserName(username) || string.IsNullOrEmpty(password))
            return false;

        await using var db = await dbFactory.CreateDbContextAsync();
        var hash = await db.SiteSettings.AsNoTracking()
            .Where(s => s.Key == PasswordHashKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(hash))
            return string.Equals(password, DefaultPassword, StringComparison.Ordinal);

        return VerifyPassword(password, hash);
    }

    public async Task<string?> ChangePasswordAsync(string? currentPassword, string? newPassword, string? confirmPassword)
    {
        if (!await VerifyAsync(UserName, currentPassword))
            return "current";
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            return "short";
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            return "mismatch";

        await using var db = await dbFactory.CreateDbContextAsync();
        var setting = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == PasswordHashKey);
        var hash = HashPassword(newPassword);
        if (setting is null)
            db.SiteSettings.Add(new SiteSetting { Key = PasswordHashKey, Value = hash });
        else
            setting.Value = hash;

        await db.SaveChangesAsync();
        return null;
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.', 2);
        if (parts.Length != 2)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
