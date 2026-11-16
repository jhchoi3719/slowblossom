namespace RotationDating.Web.Models;

// 전자책을 열기 전에 받는 짧은 연애 상황 설문. 연애상담 AI 챗봇 학습·콘텐츠 기획용으로 모은다.
public class LoveSurveyResponse
{
    public int Id { get; set; }

    // 연애 전 / 썸 타는 중 / 연애 중
    public string Status { get; set; } = "";

    // 본인 MBTI (모르면 "모름")
    public string Mbti { get; set; } = "";

    // 상대(애인·썸·관심 있는 사람) MBTI, 선택
    public string? PartnerMbti { get; set; }

    public string? Gender { get; set; }

    public string? AgeRange { get; set; }

    // 주관식 고민
    public string Concern { get; set; } = "";

    // 전자책을 다시 열 때 확인하는 값 (쿠키에 저장)
    public string Token { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
