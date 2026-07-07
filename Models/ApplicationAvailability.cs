namespace RotationDating.Web.Models;

public class ApplicationAvailability
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public DateTime AvailableDate { get; set; }

    public ParticipantApplication? Application { get; set; }
}
