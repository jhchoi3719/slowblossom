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
}
