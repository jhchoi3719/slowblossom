namespace RotationDating.Web.Models;

public class ParticipantAiMatch
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public VoteType VoteType { get; set; }
    public int MaleApplicationId { get; set; }
    public int FemaleApplicationId { get; set; }
    public MatchSource MatchSource { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ParticipantApplication? Male { get; set; }
    public ParticipantApplication? Female { get; set; }
}
