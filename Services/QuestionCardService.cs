using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public class QuestionCardService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<QuestionCard>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.QuestionCards
            .OrderBy(q => q.SortOrder)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.QuestionCards.CountAsync();
    }

    public async Task<(string Text, int Count)> GetRandomAsync(int? seed = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var questions = await db.QuestionCards
            .OrderBy(q => q.SortOrder)
            .Select(q => q.Text)
            .ToListAsync();

        if (questions.Count == 0)
            return ("등록된 질문이 없습니다.", 0);

        var index = seed.HasValue
            ? Math.Abs(seed.Value) % questions.Count
            : Random.Shared.Next(questions.Count);

        return (questions[index], questions.Count);
    }
}
