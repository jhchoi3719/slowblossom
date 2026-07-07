namespace RotationDating.Web.Models;

public class Participant
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public int? Age { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? Memo { get; set; }
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Applied;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Event? Event { get; set; }

    public string GenderLabel => Gender == Gender.Male ? "남" : "여";
    public string StatusLabel => Status switch
    {
        ParticipantStatus.Applied => "신청",
        ParticipantStatus.Confirmed => "확정",
        ParticipantStatus.Cancelled => "취소",
        _ => ""
    };
}
