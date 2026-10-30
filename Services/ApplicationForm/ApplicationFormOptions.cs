namespace RotationDating.Web.Services.ApplicationForm;

public static class ApplicationFormOptions
{
    public const int MaxInterests = 3;

    public static readonly string[] AgeRanges = ["22-27", "28-32", "33 이상"];

    public static readonly string[] Interests =
        ["여행", "운동", "맛집", "전시/공연", "독서", "게임", "재테크", "반려동물", "음악"];

    public static readonly string[] ReferralSources = ["인스타", "블로그", "지인"];
}
