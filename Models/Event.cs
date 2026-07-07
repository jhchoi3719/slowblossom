namespace RotationDating.Web.Models;

public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public EventKind Kind { get; set; } = EventKind.FixedDate;
    public DateTime? FinalizedDate { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Participant> Participants { get; set; } = [];
    public ICollection<ParticipantApplication> Applications { get; set; } = [];
    public ICollection<EventCandidateDate> CandidateDates { get; set; } = [];

    public string TabLabel => Kind switch
    {
        EventKind.DatePoll when FinalizedDate.HasValue => $"{FinalizedDate.Value:M월 d일} 확정",
        EventKind.DatePoll => Title,
        _ => EventDate.ToString("M월 d일 (ddd)")
    };

    public string FormattedDate => Kind switch
    {
        EventKind.DatePoll when FinalizedDate.HasValue =>
            $"{FinalizedDate.Value:yyyy년 M월 d일 (ddd)} 행사 확정",
        EventKind.DatePoll =>
            $"가능일: {string.Join(", ", CandidateDates.OrderBy(c => c.SortOrder).Select(c => c.ShortLabel))}",
        _ => EventDate.ToString("yyyy년 M월 d일 (ddd)")
    };
}
