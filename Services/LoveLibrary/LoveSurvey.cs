using RotationDating.Web.Models;

namespace RotationDating.Web.Services.LoveLibrary;

public static class LoveSurveyOptions
{
    public const string BeforeDating = "연애 전";
    public const string Situationship = "썸 타는 중";
    public const string Dating = "연애 중";
    public const string Common = "공통";
    public const string UnknownMbti = "모름";

    public const int ConcernMinLength = 10;
    public const int ConcernMaxLength = 1000;

    public static readonly string[] Statuses = [BeforeDating, Situationship, Dating];

    // 전자책 대상: 썸은 "연애 전" 책을 함께 본다
    public static readonly string[] Audiences = [BeforeDating, Dating, Common];

    public static readonly string[] MbtiTypes =
    [
        "INTJ", "INTP", "ENTJ", "ENTP",
        "INFJ", "INFP", "ENFJ", "ENFP",
        "ISTJ", "ISFJ", "ESTJ", "ESFJ",
        "ISTP", "ISFP", "ESTP", "ESFP"
    ];

    public static readonly string[] Genders = ["여", "남"];

    public static readonly string[] AgeRanges = ["20대 초반", "20대 후반", "30대 초반", "30대 후반", "40대 이상"];

    public static string StatusPlaceholder(string? status) => status switch
    {
        Dating => "예) 연애 1년 차인데 연락 문제로 자주 다퉈요. 서운한 걸 어떻게 말해야 할지 모르겠어요.",
        Situationship => "예) 3번 만났는데 상대가 먼저 연락을 잘 안 해요. 저한테 관심이 있는 걸까요?",
        _ => "예) 소개팅에서 첫 만남은 좋은데 두 번째 만남으로 잘 이어지지 않아요."
    };
}

public sealed class LoveSurveyInput
{
    public string Status { get; set; } = "";
    public string Mbti { get; set; } = "";
    public string? PartnerMbti { get; set; }
    public string? Gender { get; set; }
    public string? AgeRange { get; set; }
    public string Concern { get; set; } = "";
    public bool Agree { get; set; }
    public string? Website { get; set; }

    public static LoveSurveyInput FromForm(IFormCollection form) => new()
    {
        Status = form["status"].ToString().Trim(),
        Mbti = form["mbti"].ToString().Trim().ToUpperInvariant(),
        PartnerMbti = Blank(form["partnerMbti"].ToString().Trim().ToUpperInvariant()),
        Gender = Blank(form["gender"].ToString().Trim()),
        AgeRange = Blank(form["ageRange"].ToString().Trim()),
        Concern = form["concern"].ToString().Replace("\r\n", "\n").Trim(),
        Agree = form["agree"].ToString() is "on" or "true",
        Website = Blank(form["website"].ToString())
    };

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public static class LoveSurveyValidator
{
    // 문제가 없으면 null, 있으면 화면에 보여줄 오류 코드
    public static string? Validate(LoveSurveyInput input)
    {
        if (!LoveSurveyOptions.Statuses.Contains(input.Status))
            return "status";
        if (!IsMbtiOrUnknown(input.Mbti))
            return "mbti";
        if (input.PartnerMbti is not null && !IsMbtiOrUnknown(input.PartnerMbti))
            return "mbti";
        if (input.Gender is not null && !LoveSurveyOptions.Genders.Contains(input.Gender))
            return "gender";
        if (input.AgeRange is not null && !LoveSurveyOptions.AgeRanges.Contains(input.AgeRange))
            return "age";
        if (input.Concern.Length < LoveSurveyOptions.ConcernMinLength)
            return "concern-short";
        if (input.Concern.Length > LoveSurveyOptions.ConcernMaxLength)
            return "concern-long";
        if (!input.Agree)
            return "agree";
        return null;
    }

    public static string Message(string? code) => code switch
    {
        "status" => "지금 연애 상황을 골라 주세요.",
        "mbti" => "MBTI를 골라 주세요. 모르면 '모름'을 눌러 주세요.",
        "gender" or "age" => "성별·나이대를 다시 골라 주세요.",
        "concern-short" => $"고민을 {LoveSurveyOptions.ConcernMinLength}자 이상 적어 주세요. 한두 문장이면 충분해요.",
        "concern-long" => $"고민은 {LoveSurveyOptions.ConcernMaxLength}자까지 적을 수 있어요.",
        "agree" => "답변 활용 동의에 체크해 주세요.",
        "rate" => "잠시 후 다시 시도해 주세요.",
        "book" => "설문을 마치면 전자책을 열 수 있어요.",
        _ => "입력 내용을 다시 확인해 주세요."
    };

    private static bool IsMbtiOrUnknown(string value) =>
        value == LoveSurveyOptions.UnknownMbti || LoveSurveyOptions.MbtiTypes.Contains(value);
}

public static class EBookRecommender
{
    // 설문 답에 맞는 책이 앞에 오도록 정렬하고, 잘 맞는 책에는 추천 표시를 한다.
    public static List<(EBook Book, bool Recommended)> Arrange(IEnumerable<EBook> books, LoveSurveyResponse? response)
    {
        return books
            .Select(b => (Book: b, Score: Score(b, response)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Book.SortOrder)
            .ThenBy(x => x.Book.Id)
            .Select(x => (x.Book, response is not null && x.Score >= 2))
            .ToList();
    }

    private static int Score(EBook book, LoveSurveyResponse? response)
    {
        if (response is null)
            return 0;

        var score = 0;
        var wanted = response.Status == LoveSurveyOptions.Dating ? LoveSurveyOptions.Dating : LoveSurveyOptions.BeforeDating;
        if (book.Audience == wanted)
            score += 2;
        else if (book.Audience == LoveSurveyOptions.Common)
            score += 1;

        if (!string.IsNullOrEmpty(book.Mbti))
        {
            if (book.Mbti == response.Mbti || book.Mbti == response.PartnerMbti)
                score += 3;
            else
                score -= 3;
        }
        else if (book.Title.Contains("MBTI", StringComparison.OrdinalIgnoreCase) && response.Mbti != LoveSurveyOptions.UnknownMbti)
        {
            score += 1;
        }

        return score;
    }
}
