using RotationDating.Web.Models;
using RotationDating.Web.Services.LoveLibrary;

namespace RotationDating.Web.Tests;

public class LoveLibraryTests
{
    private static LoveSurveyInput Valid() => new()
    {
        Status = LoveSurveyOptions.Situationship,
        Mbti = "ENFP",
        Concern = "세 번 만났는데 상대가 먼저 연락을 잘 안 해요.",
        Agree = true
    };

    [Fact]
    public void Valid_answer_passes()
    {
        Assert.Null(LoveSurveyValidator.Validate(Valid()));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("mbti")]
    [InlineData("concern-short")]
    [InlineData("agree")]
    public void Missing_parts_are_reported(string expected)
    {
        var input = Valid();
        switch (expected)
        {
            case "status": input.Status = "결혼"; break;
            case "mbti": input.Mbti = "ABCD"; break;
            case "concern-short": input.Concern = "연락"; break;
            case "agree": input.Agree = false; break;
        }

        Assert.Equal(expected, LoveSurveyValidator.Validate(input));
    }

    [Fact]
    public void Unknown_mbti_is_allowed()
    {
        var input = Valid();
        input.Mbti = LoveSurveyOptions.UnknownMbti;
        input.PartnerMbti = LoveSurveyOptions.UnknownMbti;
        Assert.Null(LoveSurveyValidator.Validate(input));
    }

    [Fact]
    public void Dating_reader_sees_dating_book_first_and_other_types_last()
    {
        var books = new[]
        {
            new EBook { Id = 1, Title = "연애 시작 가이드", Audience = LoveSurveyOptions.BeforeDating },
            new EBook { Id = 2, Title = "연애 중 가이드", Audience = LoveSurveyOptions.Dating },
            new EBook { Id = 3, Title = "INTJ 연애", Audience = LoveSurveyOptions.Common, Mbti = "INTJ" },
            new EBook { Id = 4, Title = "ENFP 연애", Audience = LoveSurveyOptions.Common, Mbti = "ENFP" }
        };
        var response = new LoveSurveyResponse { Status = LoveSurveyOptions.Dating, Mbti = "ENFP" };

        var arranged = EBookRecommender.Arrange(books, response);

        Assert.Equal(4, arranged[0].Book.Id);
        Assert.True(arranged[0].Recommended);
        Assert.Equal(2, arranged[1].Book.Id);
        Assert.Equal(3, arranged[^1].Book.Id);
        Assert.False(arranged[^1].Recommended);
    }

    [Fact]
    public void Situationship_counts_as_before_dating()
    {
        var books = new[]
        {
            new EBook { Id = 1, Title = "연애 중 가이드", Audience = LoveSurveyOptions.Dating },
            new EBook { Id = 2, Title = "연애 시작 가이드", Audience = LoveSurveyOptions.BeforeDating }
        };
        var response = new LoveSurveyResponse { Status = LoveSurveyOptions.Situationship, Mbti = "모름" };

        Assert.Equal(2, EBookRecommender.Arrange(books, response)[0].Book.Id);
    }

    [Fact]
    public void Nothing_is_recommended_before_the_survey()
    {
        var books = new[] { new EBook { Id = 1, Title = "A", Audience = LoveSurveyOptions.BeforeDating } };
        Assert.False(EBookRecommender.Arrange(books, null)[0].Recommended);
    }
}
