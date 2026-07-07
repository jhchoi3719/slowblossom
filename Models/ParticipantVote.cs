namespace RotationDating.Web.Models;

public class ParticipantVote
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int VoterApplicationId { get; set; }
    public int TargetApplicationId { get; set; }
    public VoteType VoteType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Event? Event { get; set; }
    public ParticipantApplication? Voter { get; set; }
    public ParticipantApplication? Target { get; set; }
}
