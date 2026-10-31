using RotationDating.Web.Models;
using RotationDating.Web.Services;
using RotationDating.Web.Services.ApplicationForm;

namespace RotationDating.Web.Tests;

public class ReviewFixTests
{
    [Fact]
    public void Returning_input_drops_new_applicant_answers()
    {
        var input = new ApplicationFormInput
        {
            IsReturning = true,
            Name = "홍길동",
            Gender = "남",
            Phone = "01012345678",
            Residence = "대구",
            KakaoId = "hong",
            Interests = ["여행"],
            Drinking = true,
            AgreeSelection = true,
            AgreePrivacy = true,
            HasPhoto = true
        };

        input.KeepOnlyReturningFields();

        Assert.Equal("홍길동", input.Name);
        Assert.Equal("남", input.Gender);
        Assert.Equal("01012345678", input.Phone);
        Assert.True(input.AgreePrivacy);
        Assert.Equal("", input.Residence);
        Assert.Equal("", input.KakaoId);
        Assert.Empty(input.Interests);
        Assert.Null(input.Drinking);
        Assert.False(input.AgreeSelection);
        Assert.False(input.HasPhoto);
    }

    [Fact]
    public void Phones_with_history_ignore_the_current_event()
    {
        var apps = new[]
        {
            new ParticipantApplication { EventId = 1, Phone = "010-1111-2222" },
            new ParticipantApplication { EventId = 2, Phone = "01011112222" },
            new ParticipantApplication { EventId = 2, Phone = "01033334444" }
        };

        var known = MemberDirectory.PhonesWithHistory(apps, excludingEventId: 2);

        Assert.Contains("01011112222", known);
        Assert.DoesNotContain("01033334444", known);
    }

    [Fact]
    public void Saving_the_default_notice_keeps_following_the_default()
    {
        var reformatted = ApplicationNoticeFormatter.ToEditableText(ApplicationNoticeDefaults.DefaultNotice);

        Assert.Null(ApplicationNoticeDefaults.ToStored(reformatted));
        Assert.Null(ApplicationNoticeDefaults.ToStored("  "));
        Assert.Equal("일시: 11월", ApplicationNoticeDefaults.ToStored(" 일시: 11월 "));
    }

    [Fact]
    public void Under_19_birth_date_is_rejected()
    {
        var thisYear = DateTime.Today.Year;
        Assert.NotNull(ApplicationFormValidator.NormalizeBirthDate("19950219"));
        Assert.Null(ApplicationFormValidator.NormalizeBirthDate($"{thisYear - 5}0101"));
    }
}
