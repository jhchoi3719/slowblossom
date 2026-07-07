namespace RotationDating.Web.Models;

public class EventCandidateDate
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public DateTime EventDate { get; set; }
    public int SortOrder { get; set; }

    public Event? Event { get; set; }

    public string ShortLabel => EventDate.ToString("M/d (ddd)");
    public string LoginSuffix => EventDate.ToString("yyMMdd");
}
