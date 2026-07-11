namespace RotationDating.Web.Models;

public class SurveyResultsView
{
    public Survey Survey { get; set; } = null!;
    public int TotalResponses { get; set; }
    public List<SurveyQuestionResult> Questions { get; set; } = [];
    public List<SurveyResponseSummary> Responses { get; set; } = [];
}

public class SurveyQuestionResult
{
    public SurveyQuestion Question { get; set; } = null!;
    public List<SurveyOptionResult> OptionResults { get; set; } = [];
    public List<SurveyTextAnswerResult> TextAnswers { get; set; } = [];
}

public class SurveyOptionResult
{
    public SurveyOption Option { get; set; } = null!;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class SurveyTextAnswerResult
{
    public string Text { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public int ResponseId { get; set; }
}

public class SurveyResponseSummary
{
    public int Id { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string PhoneNumber { get; set; } = "";
    public bool MarketingConsent { get; set; }
    public List<SurveyAnswerSummary> Answers { get; set; } = [];
}

public class SurveyAnswerSummary
{
    public string QuestionTitle { get; set; } = "";
    public string AnswerText { get; set; } = "";
}

public class SurveySubmitInput
{
    public Dictionary<int, List<int>> SelectedOptions { get; set; } = [];
    public Dictionary<int, string> TextAnswers { get; set; } = [];
    public Dictionary<string, string> CustomOptionTexts { get; set; } = [];
    public bool MarketingConsent { get; set; }
    public string PhoneNumber { get; set; } = "";
}
