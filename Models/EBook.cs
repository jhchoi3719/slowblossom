namespace RotationDating.Web.Models;

// 홈페이지 "연애 서재"에 올리는 PDF 전자책
public class EBook
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string? Subtitle { get; set; }

    // 연애 전 / 연애 중 / 공통
    public string Audience { get; set; } = "공통";

    // 특정 MBTI 전용이면 그 유형 (예: ENFP), 모든 유형용이면 비움
    public string? Mbti { get; set; }

    // {DATA_DIR}/private/ebooks 안의 파일 이름
    public string FileName { get; set; } = "";

    public int SortOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
