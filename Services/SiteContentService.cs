using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public sealed class SiteContentService(IDbContextFactory<AppDbContext> dbFactory)
{
    private SiteContentSnapshot? _cached;

    public async Task<SiteContentSnapshot> GetAsync()
    {
        if (_cached is not null)
            return _cached;

        await using var db = await dbFactory.CreateDbContextAsync();
        _cached = new SiteContentSnapshot
        {
            Settings = await db.SiteSettings.AsNoTracking()
                .ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.Ordinal),
            Sections = await db.SiteSections.AsNoTracking()
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .ToListAsync(),
            FeatureCards = await db.SiteFeatureCards.AsNoTracking()
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .ToListAsync(),
            GalleryItems = await db.SiteGalleryItems.AsNoTracking()
                .OrderBy(g => g.SortOrder).ThenBy(g => g.Id)
                .ToListAsync(),
            FaqItems = await db.SiteFaqItems.AsNoTracking()
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                .ToListAsync(),
            Posts = await db.SitePosts.AsNoTracking()
                .Where(p => p.IsPublished)
                .OrderBy(p => p.SortOrder).ThenByDescending(p => p.CreatedAt)
                .ToListAsync(),
            AboutPeople = await LoadAboutPeopleAsync(db)
        };
        return _cached;
    }

    private static async Task<List<SiteAboutPerson>> LoadAboutPeopleAsync(AppDbContext db)
    {
        try
        {
            return await db.SiteAboutPeople.AsNoTracking()
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .ToListAsync();
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<SitePost>> GetAllPostsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SitePosts.AsNoTracking()
            .OrderBy(p => p.SortOrder).ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task EnsureSeededAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.SiteSettings.AnyAsync())
            return;

        db.SiteSettings.AddRange(SiteContentSeed.Settings.Select(pair => new SiteSetting
        {
            Key = pair.Key,
            Value = pair.Value
        }));
        db.SiteSections.AddRange(SiteContentSeed.Sections);
        db.SiteFeatureCards.AddRange(SiteContentSeed.FeatureCards);
        db.SiteGalleryItems.AddRange(SiteContentSeed.GalleryItems);
        db.SiteFaqItems.AddRange(SiteContentSeed.FaqItems);
        await db.SaveChangesAsync();
    }

    public static async Task MoveAsync<T>(AppDbContext db, int id, string? direction)
        where T : class, ISiteSortable
    {
        var items = (await db.Set<T>().ToListAsync())
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToList();
        var index = items.FindIndex(i => i.Id == id);
        if (index < 0)
            return;

        var delta = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
        var swapIndex = index + delta;
        if (swapIndex < 0 || swapIndex >= items.Count)
            return;

        (items[index].SortOrder, items[swapIndex].SortOrder) = (items[swapIndex].SortOrder, items[index].SortOrder);
        await db.SaveChangesAsync();
    }
}
