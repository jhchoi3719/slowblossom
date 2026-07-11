using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public class SurveyService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<Survey>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Surveys
            .AsNoTracking()
            .Include(s => s.Questions)
            .Include(s => s.Responses)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<Survey?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Surveys
            .Include(s => s.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Survey?> GetBySlugAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Surveys
            .AsNoTracking()
            .Include(s => s.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);
    }

    public async Task<Survey?> GetParticipantSurveyAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Surveys
            .AsNoTracking()
            .Include(s => s.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(s => s.Slug == slug);
    }

    public async Task CompleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var survey = await db.Surveys.FindAsync(id);
        if (survey is null)
            return;

        survey.IsActive = false;
        await db.SaveChangesAsync();
    }

    public async Task<int> CreateAsync(string title)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var survey = new Survey
        {
            Title = title.Trim(),
            Slug = await GenerateUniqueSlugAsync(db),
            WelcomeTitle = "설문에 참여해 주세요",
            WelcomeContent = "아래 버튼을 눌러 설문을 시작해 주세요.",
            CreatedAt = DateTime.UtcNow
        };

        db.Surveys.Add(survey);
        await db.SaveChangesAsync();
        return survey.Id;
    }

    public async Task<int?> DuplicateAsync(int sourceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var source = await db.Surveys
            .AsNoTracking()
            .Include(s => s.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(s => s.Id == sourceId);

        if (source is null)
            return null;

        var copy = new Survey
        {
            Title = $"{source.Title} (복사)",
            Slug = await GenerateUniqueSlugAsync(db),
            WelcomeTitle = source.WelcomeTitle,
            WelcomeContent = source.WelcomeContent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Questions = source.Questions
                .OrderBy(q => q.SortOrder)
                .Select(q => new SurveyQuestion
                {
                    Title = q.Title,
                    Content = q.Content,
                    QuestionType = q.QuestionType,
                    AllowMultiple = q.AllowMultiple,
                    IsRequired = q.IsRequired,
                    SortOrder = q.SortOrder,
                    Options = q.Options
                        .OrderBy(o => o.SortOrder)
                        .Select(o => new SurveyOption
                        {
                            Text = o.Text,
                            AllowCustomInput = o.AllowCustomInput,
                            SortOrder = o.SortOrder
                        })
                        .ToList()
                })
                .ToList()
        };

        db.Surveys.Add(copy);
        await db.SaveChangesAsync();
        return copy.Id;
    }

    public async Task SaveWelcomeAsync(int id, string welcomeTitle, string welcomeContent, string? title = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var survey = await db.Surveys.FindAsync(id)
            ?? throw new InvalidOperationException("설문을 찾을 수 없습니다.");

        survey.WelcomeTitle = welcomeTitle.Trim();
        survey.WelcomeContent = welcomeContent.Trim();
        if (!string.IsNullOrWhiteSpace(title))
            survey.Title = title.Trim();

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var survey = await db.Surveys.FindAsync(id);
        if (survey is null)
            return;

        db.Surveys.Remove(survey);
        await db.SaveChangesAsync();
    }

    public async Task<int> SaveQuestionAsync(
        int surveyId,
        int? questionId,
        string title,
        string content,
        SurveyQuestionType questionType,
        bool allowMultiple,
        bool isRequired,
        IReadOnlyList<SurveyOptionInput> options)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        SurveyQuestion question;
        if (questionId.HasValue)
        {
            question = await db.SurveyQuestions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == questionId.Value && q.SurveyId == surveyId)
                ?? throw new InvalidOperationException("항목을 찾을 수 없습니다.");

            db.SurveyOptions.RemoveRange(question.Options);
        }
        else
        {
            var maxOrder = await db.SurveyQuestions
                .Where(q => q.SurveyId == surveyId)
                .Select(q => (int?)q.SortOrder)
                .MaxAsync() ?? 0;

            question = new SurveyQuestion
            {
                SurveyId = surveyId,
                SortOrder = maxOrder + 1
            };
            db.SurveyQuestions.Add(question);
        }

        question.Title = title.Trim();
        question.Content = content.Trim();
        question.QuestionType = questionType;
        question.AllowMultiple = questionType == SurveyQuestionType.Objective && allowMultiple;
        question.IsRequired = isRequired;

        if (questionType == SurveyQuestionType.Objective)
        {
            var cleaned = options
                .Select(o => new { Text = o.Text.Trim(), o.AllowCustomInput })
                .Where(o => !string.IsNullOrEmpty(o.Text))
                .ToList();

            if (cleaned.Count < 1)
                throw new InvalidOperationException("객관식 항목은 보기를 1개 이상 입력해 주세요.");

            question.Options = cleaned
                .Select((item, index) => new SurveyOption
                {
                    Text = item.Text,
                    AllowCustomInput = item.AllowCustomInput,
                    SortOrder = index + 1
                })
                .ToList();
        }
        else
        {
            question.Options = [];
        }

        await db.SaveChangesAsync();
        return question.Id;
    }

    public async Task DeleteQuestionAsync(int surveyId, int questionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var question = await db.SurveyQuestions
            .FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyId == surveyId);

        if (question is null)
            return;

        db.SurveyQuestions.Remove(question);
        await db.SaveChangesAsync();

        var remaining = await db.SurveyQuestions
            .Where(q => q.SurveyId == surveyId)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

        for (var i = 0; i < remaining.Count; i++)
            remaining[i].SortOrder = i + 1;

        await db.SaveChangesAsync();
    }

    public async Task<(bool Success, string? ErrorCode)> SubmitResponseAsync(string slug, SurveySubmitInput input)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var survey = await db.Surveys
            .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);

        if (survey is null)
            return (false, "notfound");

        if (!input.MarketingConsent)
            return (false, "consent");

        if (string.IsNullOrWhiteSpace(input.PhoneNumber))
            return (false, "phone");

        var normalizedPhone = NormalizePhone(input.PhoneNumber);
        if (normalizedPhone.Length is < 10 or > 11)
            return (false, "phone");

        if (await db.SurveyResponses.AnyAsync(r =>
                r.SurveyId == survey.Id && r.NormalizedPhone == normalizedPhone))
            return (false, "duplicate");

        foreach (var question in survey.Questions)
        {
            if (!question.IsRequired)
                continue;

            if (question.QuestionType == SurveyQuestionType.Objective)
            {
                if (!input.SelectedOptions.TryGetValue(question.Id, out var selected) || selected.Count == 0)
                    return (false, $"required_{question.Id}");

                if (HasMissingCustomText(question, selected, input))
                    return (false, $"custom_{question.Id}");
            }
            else if (!input.TextAnswers.TryGetValue(question.Id, out var text) || string.IsNullOrWhiteSpace(text))
            {
                return (false, $"required_{question.Id}");
            }
        }

        foreach (var question in survey.Questions.Where(q => q.QuestionType == SurveyQuestionType.Objective))
        {
            if (!input.SelectedOptions.TryGetValue(question.Id, out var selected))
                continue;

            if (HasMissingCustomText(question, selected, input))
                return (false, $"custom_{question.Id}");
        }

        var response = new SurveyResponse
        {
            SurveyId = survey.Id,
            PhoneNumber = normalizedPhone,
            NormalizedPhone = normalizedPhone,
            MarketingConsent = true,
            SubmittedAt = DateTime.UtcNow
        };

        foreach (var question in survey.Questions.OrderBy(q => q.SortOrder))
        {
            if (question.QuestionType == SurveyQuestionType.Objective)
            {
                if (!input.SelectedOptions.TryGetValue(question.Id, out var optionIds) || optionIds.Count == 0)
                    continue;

                var validOptionIds = question.Options.Select(o => o.Id).ToHashSet();
                var cleaned = optionIds.Where(validOptionIds.Contains).Distinct().ToList();

                if (!question.AllowMultiple && cleaned.Count > 1)
                    cleaned = [cleaned[0]];

                foreach (var optionId in cleaned)
                {
                    var option = question.Options.First(o => o.Id == optionId);
                    string? textAnswer = null;
                    if (option.AllowCustomInput)
                    {
                        input.CustomOptionTexts.TryGetValue($"{question.Id}_{optionId}", out var customText);
                        textAnswer = customText?.Trim();
                    }

                    response.Answers.Add(new SurveyAnswer
                    {
                        QuestionId = question.Id,
                        OptionId = optionId,
                        TextAnswer = textAnswer
                    });
                }
            }
            else if (input.TextAnswers.TryGetValue(question.Id, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                response.Answers.Add(new SurveyAnswer
                {
                    QuestionId = question.Id,
                    TextAnswer = text.Trim()
                });
            }
        }

        db.SurveyResponses.Add(response);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<SurveyResultsView?> GetResultsAsync(int surveyId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var survey = await db.Surveys
            .AsNoTracking()
            .Include(s => s.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(s => s.Id == surveyId);

        if (survey is null)
            return null;

        var responses = await db.SurveyResponses
            .AsNoTracking()
            .Where(r => r.SurveyId == surveyId)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Option)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();

        var totalResponses = responses.Count;
        var questionResults = new List<SurveyQuestionResult>();

        foreach (var question in survey.Questions.OrderBy(q => q.SortOrder))
        {
            var result = new SurveyQuestionResult { Question = question };

            if (question.QuestionType == SurveyQuestionType.Objective)
            {
                var optionCounts = question.Options.ToDictionary(o => o.Id, _ => 0);
                foreach (var answer in responses.SelectMany(r => r.Answers).Where(a => a.QuestionId == question.Id && a.OptionId.HasValue))
                {
                    if (optionCounts.ContainsKey(answer.OptionId!.Value))
                        optionCounts[answer.OptionId.Value]++;
                }

                result.OptionResults = question.Options
                    .OrderBy(o => o.SortOrder)
                    .Select(o => new SurveyOptionResult
                    {
                        Option = o,
                        Count = optionCounts.GetValueOrDefault(o.Id),
                        Percentage = totalResponses == 0 ? 0 : optionCounts.GetValueOrDefault(o.Id) * 100.0 / totalResponses
                    })
                    .ToList();
            }
            else
            {
                result.TextAnswers = responses
                    .SelectMany(r => r.Answers
                        .Where(a => a.QuestionId == question.Id && !string.IsNullOrWhiteSpace(a.TextAnswer))
                        .Select(a => new SurveyTextAnswerResult
                        {
                            Text = a.TextAnswer!,
                            SubmittedAt = r.SubmittedAt,
                            ResponseId = r.Id
                        }))
                    .OrderByDescending(a => a.SubmittedAt)
                    .ToList();
            }

            questionResults.Add(result);
        }

        var summaries = responses.Select(r => new SurveyResponseSummary
        {
            Id = r.Id,
            SubmittedAt = r.SubmittedAt,
            PhoneNumber = r.PhoneNumber,
            MarketingConsent = r.MarketingConsent,
            Answers = survey.Questions
                .OrderBy(q => q.SortOrder)
                .Select(q => new SurveyAnswerSummary
                {
                    QuestionTitle = q.Title,
                    AnswerText = FormatAnswerText(q, r.Answers.Where(a => a.QuestionId == q.Id))
                })
                .Where(a => !string.IsNullOrWhiteSpace(a.AnswerText) && a.AnswerText != "-")
                .Concat(
                [
                    new SurveyAnswerSummary
                    {
                        QuestionTitle = "개인정보·마케팅 수신 동의",
                        AnswerText = r.MarketingConsent ? "동의" : "미동의"
                    },
                    new SurveyAnswerSummary
                    {
                        QuestionTitle = "휴대폰 번호",
                        AnswerText = string.IsNullOrWhiteSpace(r.PhoneNumber) ? "-" : r.PhoneNumber
                    }
                ])
                .ToList()
        }).ToList();

        return new SurveyResultsView
        {
            Survey = survey,
            TotalResponses = totalResponses,
            Questions = questionResults,
            Responses = summaries
        };
    }

    private static string FormatAnswerText(SurveyQuestion question, IEnumerable<SurveyAnswer> answers)
    {
        var list = answers.ToList();
        if (list.Count == 0)
            return "-";

        if (question.QuestionType == SurveyQuestionType.Subjective)
            return list.FirstOrDefault()?.TextAnswer ?? "-";

        return string.Join(", ", list
            .Select(a =>
            {
                if (a.Option?.AllowCustomInput == true && !string.IsNullOrWhiteSpace(a.TextAnswer))
                    return $"{a.Option.Text}: {a.TextAnswer}";

                return a.Option?.Text;
            })
            .Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    private static bool HasMissingCustomText(
        SurveyQuestion question,
        IEnumerable<int> selectedOptionIds,
        SurveySubmitInput input)
    {
        foreach (var optionId in selectedOptionIds)
        {
            var option = question.Options.FirstOrDefault(o => o.Id == optionId);
            if (option is not { AllowCustomInput: true })
                continue;

            if (!input.CustomOptionTexts.TryGetValue($"{question.Id}_{optionId}", out var text)
                || string.IsNullOrWhiteSpace(text))
                return true;
        }

        return false;
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    private static async Task<string> GenerateUniqueSlugAsync(AppDbContext db)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        while (true)
        {
            var slug = new string(Enumerable.Range(0, 8)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());

            if (!await db.Surveys.AnyAsync(s => s.Slug == slug))
                return slug;
        }
    }
}
