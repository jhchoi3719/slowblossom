using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public static class EventDateHelper
{
    public static DateTime? GetEffectiveLoginDate(Event evt) =>
        evt.Kind == EventKind.DatePoll ? evt.FinalizedDate?.Date : evt.EventDate.Date;

    public static bool IsPollAwaitingFinalization(Event evt) =>
        evt.Kind == EventKind.DatePoll && evt.FinalizedDate is null;

    public static bool IsAvailableOnLoginDate(ParticipantApplication app, DateTime loginDate)
    {
        if (app.Event?.Kind != EventKind.DatePoll)
            return true;

        return app.Availabilities.Any(a => a.AvailableDate.Date == loginDate.Date);
    }

    public static bool CanConfirmOnPoll(ParticipantApplication app)
    {
        if (app.Event?.Kind != EventKind.DatePoll)
            return true;

        var finalized = app.Event.FinalizedDate?.Date;
        if (finalized is null)
            return false;

        return app.Availabilities.Any(a => a.AvailableDate.Date == finalized.Value);
    }

    public static bool ShowsUnavailableOnFinalizedDate(Event evt, ParticipantApplication app)
    {
        if (evt.Kind != EventKind.DatePoll)
            return false;
        if (IsPollAwaitingFinalization(evt))
            return false;
        if (app.IsConfirmed)
            return false;

        var finalized = evt.FinalizedDate?.Date;
        if (finalized is null)
            return false;

        return !app.Availabilities.Any(a => a.AvailableDate.Date == finalized.Value);
    }

    public static List<DateTime> ParseAvailableDates(string? text, IEnumerable<DateTime>? allowedDates = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var allowed = allowedDates?.Select(d => d.Date).ToHashSet();
        var dates = new List<DateTime>();

        foreach (var token in text.Split([',', '/', '|', ' ', '，', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseDateToken(token, out var date))
                continue;

            if (allowed is not null && !allowed.Contains(date.Date))
                continue;

            if (dates.All(d => d.Date != date.Date))
                dates.Add(date.Date);
        }

        return dates.OrderBy(d => d).ToList();
    }

    public static bool TryParseDateToken(string token, out DateTime date)
    {
        date = default;

        if (token.Length == 6 && token.All(char.IsDigit)
            && int.TryParse(token[..2], out var yy)
            && int.TryParse(token.AsSpan(2, 2), out var mm)
            && int.TryParse(token.AsSpan(4, 2), out var dd))
        {
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

        return DateTime.TryParse(token, out date);
    }

    public static List<DateTime> ParsePollAvailableDates(string? text, IEnumerable<DateTime>? candidateDates = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var candidates = candidateDates?.Select(d => d.Date).Distinct().OrderBy(d => d).ToList() ?? [];
        var dates = new List<DateTime>();

        foreach (var token in text.Split([',', '/', '|', ' ', '，', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseDateToken(token, out var parsedDate))
            {
                if (candidates.Count == 0 || candidates.Contains(parsedDate.Date))
                    AddDateIfNew(dates, parsedDate.Date);
                continue;
            }

            if (int.TryParse(token, out var day) && day is >= 1 and <= 31)
            {
                foreach (var candidate in candidates.Where(d => d.Day == day))
                    AddDateIfNew(dates, candidate);
            }
        }

        return dates.OrderBy(d => d).ToList();
    }

    private static void AddDateIfNew(List<DateTime> dates, DateTime date)
    {
        if (dates.All(d => d.Date != date.Date))
            dates.Add(date.Date);
    }

    public static string FormatAvailableDates(IEnumerable<DateTime> dates) =>
        string.Join(", ", dates.OrderBy(d => d).Select(d => d.ToString("M/d (ddd)")));
}
