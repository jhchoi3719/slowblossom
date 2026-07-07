using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public sealed class ParsedBulkApplication
{
    public ParticipantApplication Application { get; set; } = new();
    public List<DateTime> AvailableDates { get; set; } = [];
}

public static class BulkApplicationParser
{
    private static readonly string[] LineSeparators = ["\r\n", "\n", "\r"];

    public static List<ParsedBulkApplication> Parse(
        string? pasteData,
        int eventId,
        bool includeAvailability = false,
        IEnumerable<DateTime>? candidateDates = null)
    {
        if (string.IsNullOrWhiteSpace(pasteData))
            return [];

        var applications = new List<ParsedBulkApplication>();

        foreach (var line in pasteData.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            if (IsHeaderRow(trimmedLine))
                continue;

            var columns = trimmedLine.Split('\t');
            var name = GetColumn(columns, 0);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var parsed = new ParsedBulkApplication
            {
                Application = new ParticipantApplication
                {
                    EventId = eventId,
                    Name = name,
                    BirthDate = TrimOrNull(GetColumn(columns, 1)),
                    Gender = NormalizeGender(GetColumn(columns, 2)),
                    Phone = TrimOrNull(GetColumn(columns, 3)),
                    Residence = TrimOrNull(GetColumn(columns, 4)),
                    Workplace = TrimOrNull(GetColumn(columns, 5)),
                    AllowContact = false
                }
            };

            if (includeAvailability)
            {
                parsed.Application.PreferredAgeRange = TrimOrNull(GetColumn(columns, 7));
                parsed.Application.Interests = TrimOrNull(GetColumn(columns, 8));
                parsed.Application.Drinking = ParseOxText(GetColumn(columns, 9));
                parsed.Application.Smoking = ParseOxText(GetColumn(columns, 10));
                parsed.AvailableDates = EventDateHelper.ParsePollAvailableDates(
                    GetColumn(columns, 6),
                    candidateDates);
            }
            else
            {
                parsed.Application.PreferredAgeRange = TrimOrNull(GetColumn(columns, 6));
                parsed.Application.Interests = TrimOrNull(GetColumn(columns, 7));
                parsed.Application.Drinking = ParseOxText(GetColumn(columns, 8));
                parsed.Application.Smoking = ParseOxText(GetColumn(columns, 9));
            }

            applications.Add(parsed);
        }

        return applications;
    }

    private static bool IsHeaderRow(string line)
    {
        var firstCell = GetColumn(line.Split('\t'), 0);
        return firstCell.Contains("이름", StringComparison.Ordinal)
            && (line.Contains("생년", StringComparison.Ordinal)
                || line.Contains("성별", StringComparison.Ordinal)
                || line.Contains("연락", StringComparison.Ordinal));
    }

    private static string GetColumn(string[] columns, int index) =>
        index < columns.Length ? columns[index].Trim() : "";

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "남" or "남성" or "m" or "male" => "남",
            "여" or "여성" or "f" or "female" => "여",
            _ => value.Trim() is "남" or "여" ? value.Trim() : TrimOrNull(value)
        };
    }

    public static bool? ParseOxText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "o" or "true" or "y" or "yes" => true,
            "x" or "false" or "n" or "no" => false,
            _ => null
        };
    }
}
