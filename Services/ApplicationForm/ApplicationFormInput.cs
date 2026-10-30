namespace RotationDating.Web.Services.ApplicationForm;

public sealed class ApplicationFormInput
{
    public string Name { get; set; } = "";
    public string BirthDate { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Residence { get; set; } = "";
    public string Phone { get; set; } = "";
    public string KakaoId { get; set; } = "";
    public string Education { get; set; } = "";
    public string Workplace { get; set; } = "";
    public string PreferredAgeRange { get; set; } = "";
    public List<string> Interests { get; set; } = [];
    public string ReferralSource { get; set; } = "";
    public string Motivation { get; set; } = "";
    public bool? Drinking { get; set; }
    public bool? Smoking { get; set; }
    public string SelfIntro { get; set; } = "";
    public bool IsReturning { get; set; }
    public List<DateTime> AvailableDates { get; set; } = [];

    public bool AgreeSelection { get; set; }
    public bool AgreeMarriage { get; set; }
    public bool AgreePhoto { get; set; }
    public bool AgreeRefund { get; set; }
    public bool AgreePrivacy { get; set; }

    public bool HasPhoto { get; set; }
    public bool HasEmploymentProof { get; set; }
    public bool HasMarriageCert { get; set; }
}
