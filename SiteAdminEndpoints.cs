using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;
using RotationDating.Web.Services;

namespace RotationDating.Web.Services;

public static class SiteAdminEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/admin/site/login", LoginAsync).DisableAntiforgery();
        app.MapPost("/admin/site/logout", (Delegate)LogoutAsync).DisableAntiforgery();

        var admin = app.MapGroup("/admin/site")
            .RequireAuthorization(policy => policy.RequireRole(AuthRoles.SiteAdmin))
            .DisableAntiforgery();

        admin.MapPost("/password", ChangePasswordAsync);

        admin.MapPost("/basic/save", SaveBasicAsync);
        admin.MapPost("/about/save", SaveAboutAsync);

        admin.MapPost("/sections/save", SaveSectionAsync);
        admin.MapPost("/sections/add", AddSectionAsync);
        admin.MapPost("/sections/delete", DeleteEntityAsync<SiteSection>("/admin/site/sections"));
        admin.MapPost("/sections/move", MoveEntityAsync<SiteSection>("/admin/site/sections"));

        admin.MapPost("/cards/save", SaveCardAsync);
        admin.MapPost("/cards/add", AddCardAsync);
        admin.MapPost("/cards/delete", DeleteEntityAsync<SiteFeatureCard>("/admin/site/cards"));
        admin.MapPost("/cards/move", MoveEntityAsync<SiteFeatureCard>("/admin/site/cards"));

        admin.MapPost("/gallery/save", SaveGalleryAsync);
        admin.MapPost("/gallery/add", AddGalleryAsync);
        admin.MapPost("/gallery/delete", DeleteEntityAsync<SiteGalleryItem>("/admin/site/gallery"));
        admin.MapPost("/gallery/move", MoveEntityAsync<SiteGalleryItem>("/admin/site/gallery"));

        admin.MapPost("/faq/save", SaveFaqAsync);
        admin.MapPost("/faq/add", AddFaqAsync);
        admin.MapPost("/faq/delete", DeleteEntityAsync<SiteFaqItem>("/admin/site/faq"));
        admin.MapPost("/faq/move", MoveEntityAsync<SiteFaqItem>("/admin/site/faq"));

        admin.MapPost("/posts/save", SavePostAsync);
        admin.MapPost("/posts/add", AddPostAsync);
        admin.MapPost("/posts/delete", DeleteEntityAsync<SitePost>("/admin/site/posts"));
        admin.MapPost("/posts/move", MoveEntityAsync<SitePost>("/admin/site/posts"));
    }

    private static async Task<IResult> LoginAsync(
        [FromForm] string? username,
        [FromForm] string? password,
        HttpContext context,
        SiteAdminAuthService auth)
    {
        if (!await auth.VerifyAsync(username, password))
            return Results.Redirect("/admin/site/login?error=invalid");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, SiteAdminAuthService.UserName),
            new Claim(ClaimTypes.Role, AuthRoles.SiteAdmin)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        return Results.Redirect("/admin/site");
    }

    private static async Task<IResult> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/");
    }

    private static async Task<IResult> ChangePasswordAsync(
        [FromForm] string? currentPassword,
        [FromForm] string? newPassword,
        [FromForm] string? confirmPassword,
        SiteAdminAuthService auth)
    {
        var error = await auth.ChangePasswordAsync(currentPassword, newPassword, confirmPassword);
        if (error is not null)
            return Results.Redirect($"/admin/site/password?error={error}");
        return Results.Redirect("/admin/site/password?saved=true");
    }

    private static async Task<IResult> SaveBasicAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.SiteSettings.ToListAsync();

        var (heroImage, imageError) = await uploads.ResolveImageAsync(
            form.Files.GetFile("heroImage"),
            form["heroImageUrl"],
            GetSetting(settings, SiteContentKeys.HeroImage));

        Upsert(settings, db, SiteContentKeys.HeroTitle, Clip(form["heroTitle"], 200));
        Upsert(settings, db, SiteContentKeys.HeroDesc, Clip(form["heroDesc"], 1000));
        Upsert(settings, db, SiteContentKeys.HeroImage, heroImage);
        Upsert(settings, db, SiteContentKeys.IntroNotice, Clip(form["introNotice"], 2000));
        Upsert(settings, db, SiteContentKeys.IntroNoticeEn, Clip(form["introNoticeEn"], 2000));
        Upsert(settings, db, SiteContentKeys.KakaoUrl, Clip(form["kakaoUrl"], 500));
        Upsert(settings, db, SiteContentKeys.InstagramUrl, Clip(form["instagramUrl"], 500));
        Upsert(settings, db, SiteContentKeys.StoreUrl, Clip(form["storeUrl"], 500));
        Upsert(settings, db, SiteContentKeys.EventEyebrow, Clip(form["eventEyebrow"], 80));
        Upsert(settings, db, SiteContentKeys.EventTitle, Clip(form["eventTitle"], 120));
        Upsert(settings, db, SiteContentKeys.EventDesc, Clip(form["eventDesc"], 1000));
        Upsert(settings, db, SiteContentKeys.FooterCompany, Clip(form["footerCompany"], 80));
        Upsert(settings, db, SiteContentKeys.FooterOwner, Clip(form["footerOwner"], 80));
        Upsert(settings, db, SiteContentKeys.FooterEmail, Clip(form["footerEmail"], 120));
        Upsert(settings, db, SiteContentKeys.FooterBizNo, Clip(form["footerBizNo"], 40));
        Upsert(settings, db, SiteContentKeys.FooterAddress, Clip(form["footerAddress"], 200));
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/basic", imageError);
    }

    private static async Task<IResult> SaveAboutAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.SiteSettings.ToListAsync();
        var (image, imageError) = await uploads.ResolveImageAsync(
            form.Files.GetFile("aboutImage"),
            form["aboutImageUrl"],
            GetSetting(settings, SiteContentKeys.AboutImage));

        Upsert(settings, db, SiteContentKeys.AboutTitle, Clip(form["aboutTitle"], 80));
        Upsert(settings, db, SiteContentKeys.AboutBody, Clip(form["aboutBody"], 2000));
        Upsert(settings, db, SiteContentKeys.AboutList, Clip(form["aboutList"], 2000));
        Upsert(settings, db, SiteContentKeys.AboutImage, image);
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/about", imageError);
    }

    private static async Task<IResult> SaveSectionAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        if (!TryId(form, out var id))
            return Results.Redirect("/admin/site/sections");

        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.SiteSections.FindAsync(id);
        if (item is null)
            return Results.Redirect("/admin/site/sections");

        var (image1, error1) = await uploads.ResolveImageAsync(form.Files.GetFile("image1"), form["imageUrl1"], item.ImageUrl1);
        var (image2, error2) = await uploads.ResolveImageAsync(form.Files.GetFile("image2"), form["imageUrl2"], item.ImageUrl2);
        ApplySection(item, form, image1, image2);
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/sections", error1 ?? error2, id);
    }

    private static async Task<IResult> AddSectionAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var (image1, error1) = await uploads.ResolveImageAsync(form.Files.GetFile("image1"), form["imageUrl1"], "");
        var (image2, error2) = await uploads.ResolveImageAsync(form.Files.GetFile("image2"), form["imageUrl2"], "");
        var item = new SiteSection { SortOrder = await NextSortAsync<SiteSection>(db) };
        ApplySection(item, form, image1, image2);
        db.SiteSections.Add(item);
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/sections", error1 ?? error2, item.Id);
    }

    private static void ApplySection(SiteSection item, IFormCollection form, string image1, string image2)
    {
        item.Heading = Clip(form["heading"], 200);
        item.Title = Clip(form["title"], 400);
        item.Body = Clip(form["body"], 4000);
        item.ImageUrl1 = image1;
        item.ImageUrl2 = image2;
        item.ImageAlt1 = Clip(form["imageAlt1"], 200);
        item.ImageAlt2 = Clip(form["imageAlt2"], 200);
        item.IsReversed = IsChecked(form, "isReversed");
    }

    private static async Task<IResult> SaveCardAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        if (!TryId(form, out var id))
            return Results.Redirect("/admin/site/cards");

        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.SiteFeatureCards.FindAsync(id);
        if (item is null)
            return Results.Redirect("/admin/site/cards");

        var (image, error) = await uploads.ResolveImageAsync(form.Files.GetFile("image"), form["imageUrl"], item.ImageUrl);
        ApplyCard(item, form, image);
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/cards", error, id);
    }

    private static async Task<IResult> AddCardAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var (image, error) = await uploads.ResolveImageAsync(form.Files.GetFile("image"), form["imageUrl"], "");
        var item = new SiteFeatureCard { SortOrder = await NextSortAsync<SiteFeatureCard>(db) };
        ApplyCard(item, form, image);
        db.SiteFeatureCards.Add(item);
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/cards", error, item.Id);
    }

    private static void ApplyCard(SiteFeatureCard item, IFormCollection form, string image)
    {
        item.Eyebrow = Clip(form["eyebrow"], 200);
        item.Title = Clip(form["title"], 200);
        item.Pill = Clip(form["pill"], 80);
        item.ImageUrl = image;
        item.LinkUrl = Clip(form["linkUrl"], 1000);
        var variant = Clip(form["variant"], 20).ToLowerInvariant();
        item.Variant = variant is "class" or "store" ? variant : "store";
    }

    private static async Task<IResult> SaveGalleryAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        if (!TryId(form, out var id))
            return Results.Redirect("/admin/site/gallery");

        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.SiteGalleryItems.FindAsync(id);
        if (item is null)
            return Results.Redirect("/admin/site/gallery");

        var (image, error) = await uploads.ResolveImageAsync(form.Files.GetFile("image"), form["imageUrl"], item.ImageUrl);
        item.Category = Clip(form["category"], 80);
        item.Caption = Clip(form["caption"], 200);
        item.ImageUrl = image;
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/gallery", error, id);
    }

    private static async Task<IResult> AddGalleryAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var (image, error) = await uploads.ResolveImageAsync(form.Files.GetFile("image"), form["imageUrl"], "");
        if (string.IsNullOrWhiteSpace(image))
            return Results.Redirect("/admin/site/gallery?error=empty");

        var item = new SiteGalleryItem
        {
            Category = Clip(form["category"], 80),
            Caption = Clip(form["caption"], 200),
            ImageUrl = image,
            SortOrder = await NextSortAsync<SiteGalleryItem>(db)
        };
        db.SiteGalleryItems.Add(item);
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/gallery", error, item.Id);
    }

    private static async Task<IResult> SaveFaqAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory)
    {
        var form = await context.Request.ReadFormAsync();
        if (!TryId(form, out var id))
            return Results.Redirect("/admin/site/faq");

        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.SiteFaqItems.FindAsync(id);
        if (item is null)
            return Results.Redirect("/admin/site/faq");

        var question = Clip(form["question"], 300);
        var answer = Clip(form["answer"], 4000);
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
            return Results.Redirect($"/admin/site/faq?error=empty&id={id}");

        item.Question = question;
        item.Answer = answer;
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/faq", null, id);
    }

    private static async Task<IResult> AddFaqAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory)
    {
        var form = await context.Request.ReadFormAsync();
        var question = Clip(form["question"], 300);
        var answer = Clip(form["answer"], 4000);
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
            return Results.Redirect("/admin/site/faq?error=empty");

        await using var db = await dbFactory.CreateDbContextAsync();
        var item = new SiteFaqItem
        {
            Question = question,
            Answer = answer,
            SortOrder = await NextSortAsync<SiteFaqItem>(db)
        };
        db.SiteFaqItems.Add(item);
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/faq", null, item.Id);
    }

    private static async Task<IResult> SavePostAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        if (!TryId(form, out var id))
            return Results.Redirect("/admin/site/posts");

        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.SitePosts.FindAsync(id);
        if (item is null)
            return Results.Redirect("/admin/site/posts");

        var title = Clip(form["title"], 200);
        var body = Clip(form["body"], 8000);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            return Results.Redirect($"/admin/site/posts?error=empty&id={id}");

        var (image, error) = await uploads.ResolveImageAsync(form.Files.GetFile("image"), form["imageUrl"], item.ImageUrl ?? "");
        item.Title = title;
        item.Body = body;
        item.ImageUrl = string.IsNullOrWhiteSpace(image) ? null : image;
        item.IsPublished = IsChecked(form, "isPublished");
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/posts", error, id);
    }

    private static async Task<IResult> AddPostAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        SiteUploadService uploads)
    {
        var form = await context.Request.ReadFormAsync();
        var title = Clip(form["title"], 200);
        var body = Clip(form["body"], 8000);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            return Results.Redirect("/admin/site/posts?error=empty");

        await using var db = await dbFactory.CreateDbContextAsync();
        var (image, error) = await uploads.ResolveImageAsync(form.Files.GetFile("image"), form["imageUrl"], "");
        var item = new SitePost
        {
            Title = title,
            Body = body,
            ImageUrl = string.IsNullOrWhiteSpace(image) ? null : image,
            IsPublished = IsChecked(form, "isPublished"),
            CreatedAt = DateTime.Now,
            SortOrder = await NextSortAsync<SitePost>(db)
        };
        db.SitePosts.Add(item);
        await db.SaveChangesAsync();
        return RedirectSaved("/admin/site/posts", error, item.Id);
    }

    private static Func<HttpContext, IDbContextFactory<AppDbContext>, Task<IResult>> DeleteEntityAsync<T>(string path)
        where T : class, ISiteSortable
    {
        return async (context, dbFactory) =>
        {
            var form = await context.Request.ReadFormAsync();
            if (!TryId(form, out var id))
                return Results.Redirect(path);

            await using var db = await dbFactory.CreateDbContextAsync();
            var item = await db.Set<T>().FindAsync(id);
            if (item is not null)
            {
                db.Set<T>().Remove(item);
                await db.SaveChangesAsync();
            }

            return Results.Redirect($"{path}?deleted=true");
        };
    }

    private static Func<HttpContext, IDbContextFactory<AppDbContext>, Task<IResult>> MoveEntityAsync<T>(string path)
        where T : class, ISiteSortable
    {
        return async (context, dbFactory) =>
        {
            var form = await context.Request.ReadFormAsync();
            if (!TryId(form, out var id))
                return Results.Redirect(path);

            await using var db = await dbFactory.CreateDbContextAsync();
            await SiteContentService.MoveAsync<T>(db, id, form["direction"]);
            return Results.Redirect($"{path}?id={id}");
        };
    }

    private static async Task<int> NextSortAsync<T>(AppDbContext db)
        where T : class, ISiteSortable
    {
        var max = await db.Set<T>().Select(i => (int?)i.SortOrder).MaxAsync();
        return (max ?? 0) + 1;
    }

    private static void Upsert(List<SiteSetting> settings, AppDbContext db, string key, string value)
    {
        var setting = settings.FirstOrDefault(s => s.Key == key);
        if (setting is null)
        {
            setting = new SiteSetting { Key = key, Value = value };
            db.SiteSettings.Add(setting);
            settings.Add(setting);
            return;
        }

        setting.Value = value;
    }

    private static string GetSetting(IEnumerable<SiteSetting> settings, string key) =>
        settings.FirstOrDefault(s => s.Key == key)?.Value ?? "";

    private static bool TryId(IFormCollection form, out int id) =>
        int.TryParse(form["id"], out id) && id > 0;

    private static bool IsChecked(IFormCollection form, string name) =>
        form.TryGetValue(name, out var values)
        && values.Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

    private static string Clip(string? value, int max)
    {
        var text = (value ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static IResult RedirectSaved(string path, string? imageError, int? id = null)
    {
        var query = imageError is null ? "saved=true" : $"saved=true&error={imageError}";
        if (id is > 0)
            query += $"&id={id}";
        return Results.Redirect($"{path}?{query}");
    }
}
