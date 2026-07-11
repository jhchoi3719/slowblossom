namespace RotationDating.Web.Models;

public class Survey
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string WelcomeTitle { get; set; } = "";
    public string WelcomeContent { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SurveyQuestion> Questions { get; set; } = [];
    public ICollection<SurveyResponse> Responses { get; set; } = [];
}

public class SurveyQuestion
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public SurveyQuestionType QuestionType { get; set; }
    public bool AllowMultiple { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }

    public Survey? Survey { get; set; }
    public ICollection<SurveyOption> Options { get; set; } = [];
}

public class SurveyOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = "";
    public bool AllowCustomInput { get; set; }
    public int SortOrder { get; set; }

    public SurveyQuestion? Question { get; set; }
}
