using RotationDating.Web.Services.ApplicationForm;
using static RotationDating.Web.Services.ApplicationForm.NoticeBlockKind;

namespace RotationDating.Web.Tests;

public class ApplicationNoticeFormatterTests
{
    private static List<(NoticeBlockKind, string)> Parse(string text) =>
        ApplicationNoticeFormatter.Parse(text).Select(b => (b.Kind, b.Text)).ToList();

    [Fact]
    public void Line_based_text_becomes_headings_bullets_notes_and_paragraphs()
    {
        var blocks = Parse("[참가 대상]\n\n• 미혼남녀\n• 대구 직장인\n\n※ 권장 연령입니다.\n신청 가능합니다.");

        Assert.Equal(
        [
            (Heading, "참가 대상"),
            (Bullet, "미혼남녀"),
            (Bullet, "대구 직장인"),
            (Note, "※ 권장 연령입니다."),
            (Paragraph, "신청 가능합니다.")
        ], blocks);
    }

    [Fact]
    public void Flattened_text_breaks_at_markers_and_sentence_ends()
    {
        var blocks = Parse("안녕하세요 🌸 소개팅입니다. [진행 방식] • 5:5 진행 • 15분 대화 ※ 조정될 수 있습니다. 감사합니다 :)");

        Assert.Equal(
        [
            (Paragraph, "안녕하세요 🌸"),
            (Paragraph, "소개팅입니다."),
            (Heading, "진행 방식"),
            (Bullet, "5:5 진행"),
            (Bullet, "15분 대화"),
            (Note, "※ 조정될 수 있습니다."),
            (Paragraph, "감사합니다 :)")
        ], blocks);
    }

    [Fact]
    public void Flattened_default_notice_is_laid_out_like_the_original()
    {
        var flat = string.Join(' ', ApplicationNoticeDefaults.DefaultNotice
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        Assert.Equal(Parse(ApplicationNoticeDefaults.DefaultNotice), Parse(flat));
    }

    [Fact]
    public void ToEditableText_restores_line_breaks_for_the_admin_box()
    {
        var text = ApplicationNoticeFormatter.ToEditableText("안녕하세요 🌸 [참가비] • 남성 : 5만원 • 여성 : 4만원 ※ 음료 포함입니다.");

        Assert.Equal("안녕하세요 🌸\n\n[참가비]\n\n• 남성 : 5만원\n• 여성 : 4만원\n\n※ 음료 포함입니다.", text);
        Assert.Equal(Parse("안녕하세요 🌸 [참가비] • 남성 : 5만원 • 여성 : 4만원 ※ 음료 포함입니다."), Parse(text));
    }

    [Fact]
    public void Default_notice_has_its_sections()
    {
        var blocks = Parse(ApplicationNoticeDefaults.DefaultNotice);

        Assert.Contains((Heading, "참가비"), blocks);
        Assert.Contains((Bullet, "남성 : 85,000 -> 할인가 55,000"), blocks);
        Assert.Contains((Bullet, "혼인관계증명서"), blocks);
        Assert.Contains(blocks, b => b.Item1 == Paragraph && b.Item2.StartsWith("제출해주신 자료는"));
    }
}
