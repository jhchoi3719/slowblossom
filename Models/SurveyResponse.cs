namespace RotationDating.Web.Models;

public class SurveyResponse
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public string PhoneNumber { get; set; } = "";
    public string NormalizedPhone { get; set; } = "";
    public bool MarketingConsent { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Survey? Survey { get; set; }
    public ICollection<SurveyAnswer> Answers { get; set; } = [];
}

public class SurveyAnswer
{
    public int Id { get; set; }
    public int ResponseId { get; set; }
    public int QuestionId { get; set; }
    public int? OptionId { get; set; }
    public string? TextAnswer { get; set; }

    public SurveyResponse? Response { get; set; }
    public SurveyQuestion? Question { get; set; }
    public SurveyOption? Option { get; set; }
}
