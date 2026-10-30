using RotationDating.Web.Models;
using RotationDating.Web.Services.ApplicationForm;

namespace RotationDating.Web.Tests;

public class ApplicationNotifierTests
{
    private static readonly Event Evt = new() { Kind = EventKind.FixedDate, EventDate = new DateTime(2026, 11, 7) };

    private static ParticipantApplication App(bool returning) => new()
    {
        Name = "홍길동",
        Gender = "남",
        BirthDate = "19950219",
        Phone = "01012345678",
        KakaoId = "hong95",
        IsReturning = returning
    };

    [Fact]
    public void BuildMessage_contains_name_event_and_contact()
    {
        var message = ApplicationNotifier.BuildMessage(Evt, App(returning: false));

        Assert.Contains("새 신청서", message);
        Assert.Contains(Evt.TabLabel, message);
        Assert.Contains("홍길동 · 남 · 95년생", message);
        Assert.Contains("01012345678 / 카톡 hong95", message);
        Assert.DoesNotContain("기존 참가자", message);
    }

    [Fact]
    public void BuildMessage_marks_returning_applicant()
    {
        Assert.Contains("기존 참가자", ApplicationNotifier.BuildMessage(Evt, App(returning: true)));
    }
}
