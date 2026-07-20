namespace RotationDating.Web.Models;

public class ParticipantApplication
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Residence { get; set; }
    public string? Workplace { get; set; }
    public string? PreferredAgeRange { get; set; }
    public string? Interests { get; set; }
    public bool? Drinking { get; set; }
    public bool? Smoking { get; set; }
    public bool? AllowContact { get; set; }
    public bool IsConfirmed { get; set; }
    public bool HasArrived { get; set; }
    public string? Password { get; set; }
    public DateTime? ConsentAcceptedAt { get; set; }
    public string? Memo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Event? Event { get; set; }
    public ICollection<ApplicationAvailability> Availabilities { get; set; } = [];

    public static string OxLabel(bool? value) => value switch
    {
        true => "O",
        false => "X",
        _ => "-"
    };

    public static string? ToBirthYearLabel(string? birthDate)
    {
        if (string.IsNullOrWhiteSpace(birthDate))
            return null;

        var trimmed = birthDate.Trim();
        if (trimmed.EndsWith("년생", StringComparison.Ordinal))
            return trimmed;

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length >= 8
            && int.TryParse(digits[..4], out var fullYear)
            && fullYear is >= 1900 and <= 2099)
            return $"{fullYear % 100:00}년생";

        if (digits.Length >= 6
            && int.TryParse(digits[..2], out var yy))
        {
            var year = yy <= 30 ? 2000 + yy : 1900 + yy;
            return $"{year % 100:00}년생";
        }

        if (digits.Length == 4
            && int.TryParse(digits, out var yearOnly)
            && yearOnly is >= 1900 and <= 2099)
            return $"{yearOnly % 100:00}년생";

        return null;
    }
}
