using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public static class ParticipantAuthService
{
    public const int PasswordLength = 4;

    public static bool IsValidPasswordFormat(string? password) =>
        password?.Length == PasswordLength && password.All(char.IsDigit);

    public static bool HasPassword(ParticipantApplication application) =>
        IsValidPasswordFormat(application.Password);
    public static string FormatLoginId(string name, DateTime eventDate)
    {
        var trimmedName = name.Trim();
        return $"{trimmedName}{eventDate:yyMMdd}";
    }

    public static bool TryParseLoginId(string input, out string name, out DateTime eventDate)
    {
        name = "";
        eventDate = default;

        var trimmed = input.Trim();
        if (trimmed.Length < 7)
            return false;

        var dateSuffix = trimmed[^6..];
        if (!dateSuffix.All(char.IsDigit))
            return false;

        if (!TryParseYyMmDd(dateSuffix, out eventDate))
            return false;

        name = trimmed[..^6].Trim();
        return !string.IsNullOrWhiteSpace(name);
    }

    public static async Task<ParticipantApplication?> FindConfirmedParticipantAsync(
        AppDbContext db,
        string loginId)
    {
        if (!TryParseLoginId(loginId, out var name, out var eventDate))
            return null;

        var applications = await db.Applications
            .Include(a => a.Event)
            .Include(a => a.Availabilities)
            .Where(a => a.IsConfirmed)
            .ToListAsync();

        return applications.FirstOrDefault(a =>
        {
            if (!string.Equals(a.Name.Trim(), name, StringComparison.Ordinal))
                return false;

            var loginDate = a.Event is null ? null : EventDateHelper.GetEffectiveLoginDate(a.Event);
            if (loginDate?.Date != eventDate.Date)
                return false;

            return EventDateHelper.IsAvailableOnLoginDate(a, eventDate.Date);
        });
    }

    private static bool TryParseYyMmDd(string yymmdd, out DateTime date)
    {
        date = default;

        if (yymmdd.Length != 6)
            return false;

        if (!int.TryParse(yymmdd[..2], out var yy)
            || !int.TryParse(yymmdd.AsSpan(2, 2), out var mm)
            || !int.TryParse(yymmdd.AsSpan(4, 2), out var dd))
            return false;

        try
        {
            date = new DateTime(2000 + yy, mm, dd);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static async Task SignInParticipantAsync(HttpContext context, ParticipantApplication application)
    {
        var venue = VenueHelper.FromEventKind(application.Event!.Kind);
        var effectiveDate = EventDateHelper.GetEffectiveLoginDate(application.Event);

        var participantClaims = new[]
        {
            new Claim(ClaimTypes.Name, application.Name.Trim()),
            new Claim(ClaimTypes.Role, AuthRoles.Participant),
            new Claim(AuthClaims.EventId, application.EventId.ToString()),
            new Claim(AuthClaims.EventTitle, application.Event.Title),
            new Claim(AuthClaims.ApplicationId, application.Id.ToString()),
            new Claim(AuthClaims.Gender, application.Gender ?? ""),
            new Claim(AuthClaims.VenueLabel, VenueHelper.DisplayName(venue)),
            new Claim(AuthClaims.EventDateLabel, effectiveDate?.ToString("yyyy년 M월 d일 (ddd)") ?? "")
        };
        var participantIdentity = new ClaimsIdentity(participantClaims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(participantIdentity));
    }
}
