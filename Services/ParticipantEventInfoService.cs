using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;
using System.Text.RegularExpressions;

namespace RotationDating.Web.Services;

public sealed class ParticipantEventInfo
{
    public string Title { get; init; } = "";
    public string VenueLabel { get; init; } = "";
    public string DateLabel { get; init; } = "";
}

public static class ParticipantEventInfoService
{
    private static readonly Regex AutoFixedTitleRegex = new(@"^\d+월 \d+일 로테이션 소개팅$");

    public static async Task<ParticipantEventInfo?> GetAsync(IDbContextFactory<AppDbContext> dbFactory, int eventId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var evt = await db.Events
            .AsNoTracking()
            .Include(e => e.CandidateDates)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt is null)
            return null;

        var venue = VenueHelper.FromEventKind(evt.Kind);
        var effectiveDate = EventDateHelper.GetEffectiveLoginDate(evt);

        return new ParticipantEventInfo
        {
            Title = GetParticipantDisplayTitle(evt),
            VenueLabel = VenueHelper.DisplayName(venue),
            DateLabel = FormatDateLabel(evt, effectiveDate)
        };
    }

    public static string GetParticipantDisplayTitle(Event evt)
    {
        if (evt.Kind == EventKind.FixedDate && IsAutoFixedTitle(evt.Title))
            return $"{evt.EventDate:M월 d일} 로테이션 소개팅";

        return evt.Title;
    }

    public static void SyncFixedDateTitle(Event evt, DateTime dateOnly)
    {
        if (evt.Kind != EventKind.FixedDate)
            return;

        if (IsAutoFixedTitle(evt.Title))
            evt.Title = $"{dateOnly:M월 d일} 로테이션 소개팅";
    }

    public static bool IsAutoFixedTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) && AutoFixedTitleRegex.IsMatch(title.Trim());

    private static string FormatDateLabel(Event evt, DateTime? effectiveDate)
    {
        if (effectiveDate.HasValue)
            return effectiveDate.Value.ToString("yyyy년 M월 d일 (ddd)");

        if (evt.Kind == EventKind.DatePoll)
            return $"후보일: {string.Join(", ", evt.CandidateDates.OrderBy(c => c.SortOrder).Select(c => c.ShortLabel))}";

        return evt.EventDate.ToString("yyyy년 M월 d일 (ddd)");
    }
}
