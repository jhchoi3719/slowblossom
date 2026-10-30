using RotationDating.Web.Models;
using RotationDating.Web.Services.ApplicationForm;

namespace RotationDating.Web.Tests;

public class ApplicationFormValidatorTests
{
    private static readonly DateTime Sat1 = new(2026, 11, 7);
    private static readonly DateTime Sat2 = new(2026, 11, 14);

    private static Event FixedEvent() => new() { Id = 1, Kind = EventKind.FixedDate, EventDate = Sat1 };

    private static Event PollEvent() => new()
    {
        Id = 2,
        Kind = EventKind.DatePoll,
        CandidateDates = [new EventCandidateDate { EventDate = Sat1 }, new EventCandidateDate { EventDate = Sat2 }]
    };

    private static ApplicationFormInput Valid() => new()
    {
        Name = "홍길동",
        BirthDate = "19950219",
        Gender = "남",
        Residence = "대구시 수성구 범어동",
        Phone = "010-1234-5678",
        KakaoId = "hong",
        Education = "경북대학교 경영학과",
        Workplace = "대구 수성구 범어동, 대구은행, 인사과, 매니저",
        PreferredAgeRange = "28-32",
        Interests = ["여행", "운동"],
        ReferralSource = "인스타",
        Motivation = "좋은 인연을 만나고 싶어요",
        Drinking = true,
        Smoking = false,
        SelfIntro = "안녕하세요",
        HasPhoto = true,
        HasEmploymentProof = true,
        HasMarriageCert = true,
        AgreeSelection = true,
        AgreeMarriage = true,
        AgreePhoto = true,
        AgreeRefund = true,
        AgreePrivacy = true
    };

    [Fact]
    public void Valid_input_passes()
    {
        Assert.Empty(ApplicationFormValidator.Validate(Valid(), FixedEvent()));
    }

    [Fact]
    public void Missing_name_fails()
    {
        var input = Valid();
        input.Name = "  ";
        Assert.Contains("이름을 입력해 주세요.", ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Theory]
    [InlineData("19950219", true)]
    [InlineData("1995.02.19", true)]
    [InlineData("950219", false)]
    [InlineData("19951399", false)]
    public void BirthDate_must_be_8_digits(string birthDate, bool ok)
    {
        var input = Valid();
        input.BirthDate = birthDate;
        var errors = ApplicationFormValidator.Validate(input, FixedEvent());
        Assert.Equal(!ok, errors.Contains("생년월일 8자리를 입력해 주세요."));
    }

    [Fact]
    public void NormalizeBirthDate_strips_separators()
    {
        Assert.Equal("19950219", ApplicationFormValidator.NormalizeBirthDate("1995-02-19"));
        Assert.Null(ApplicationFormValidator.NormalizeBirthDate("abc"));
    }

    [Fact]
    public void Four_interests_fail()
    {
        var input = Valid();
        input.Interests = ["여행", "운동", "맛집", "독서"];
        Assert.Contains("관심사는 3개까지 선택할 수 있어요.", ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Fact]
    public void No_interest_fails()
    {
        var input = Valid();
        input.Interests = [];
        Assert.NotEmpty(ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Fact]
    public void Unknown_age_range_fails()
    {
        var input = Valid();
        input.PreferredAgeRange = "40-50";
        Assert.NotEmpty(ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Any_unchecked_agreement_fails(int which)
    {
        var input = Valid();
        switch (which)
        {
            case 0: input.AgreeSelection = false; break;
            case 1: input.AgreeMarriage = false; break;
            case 2: input.AgreePhoto = false; break;
            case 3: input.AgreeRefund = false; break;
            case 4: input.AgreePrivacy = false; break;
        }
        Assert.NotEmpty(ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Fact]
    public void New_applicant_needs_three_photos()
    {
        var input = Valid();
        input.HasMarriageCert = false;
        Assert.NotEmpty(ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Fact]
    public void Returning_applicant_skips_photos()
    {
        var input = Valid();
        input.IsReturning = true;
        input.HasPhoto = input.HasEmploymentProof = input.HasMarriageCert = false;
        Assert.Empty(ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Fact]
    public void DatePoll_event_needs_one_available_date()
    {
        var input = Valid();
        Assert.Contains("참여 가능한 날짜를 하나 이상 선택해 주세요.", ApplicationFormValidator.Validate(input, PollEvent()));

        input.AvailableDates = [Sat2];
        Assert.Empty(ApplicationFormValidator.Validate(input, PollEvent()));

        input.AvailableDates = [new DateTime(2026, 12, 25)];
        Assert.NotEmpty(ApplicationFormValidator.Validate(input, PollEvent()));
    }

    [Fact]
    public void FixedDate_event_ignores_available_dates()
    {
        var input = Valid();
        input.AvailableDates = [];
        Assert.Empty(ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Theory]
    [InlineData("01012345678", true)]
    [InlineData("010-1234-5678", true)]
    [InlineData("12345", false)]
    public void Phone_must_be_10_or_11_digits(string phone, bool ok)
    {
        var input = Valid();
        input.Phone = phone;
        Assert.Equal(ok, ApplicationFormValidator.Validate(input, FixedEvent()).Count == 0);
    }

    [Theory]
    [InlineData("Name", 51)]
    [InlineData("KakaoId", 51)]
    [InlineData("Residence", 101)]
    [InlineData("Education", 101)]
    [InlineData("Workplace", 201)]
    [InlineData("Motivation", 2001)]
    [InlineData("SelfIntro", 2001)]
    public void Overlong_text_fails(string field, int length)
    {
        var input = Valid();
        typeof(ApplicationFormInput).GetProperty(field)!.SetValue(input, new string('가', length));
        Assert.Contains(ApplicationFormValidator.Validate(input, FixedEvent()), e => e.Contains("너무 길어요"));
    }

    [Fact]
    public void Text_at_the_limit_passes()
    {
        var input = Valid();
        input.SelfIntro = new string('가', 2000);
        input.Workplace = new string('가', 200);
        Assert.Empty(ApplicationFormValidator.Validate(input, FixedEvent()));
    }

    [Fact]
    public void Missing_drinking_answer_fails()
    {
        var input = Valid();
        input.Drinking = null;
        Assert.NotEmpty(ApplicationFormValidator.Validate(input, FixedEvent()));
    }
}
