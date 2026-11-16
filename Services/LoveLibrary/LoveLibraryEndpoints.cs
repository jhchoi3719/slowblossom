using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;
using RotationDating.Web.Services.ApplicationForm;

namespace RotationDating.Web.Services.LoveLibrary;

public static class LoveLibraryEndpoints
{
    public const string PagePath = "/love";
    public const string CookieName = "sb_love";
    public const string DraftKey = "love-survey-draft";

    public static void Map(WebApplication app)
    {
        app.MapPost("/love/survey", SubmitAsync)
            .RequireRateLimiting(ApplicationFormEndpoints.SubmitRateLimitPolicy)
            .DisableAntiforgery();
        app.MapGet("/love/book/{bookId:int}", ReadBookAsync);

        app.MapGet("/admin/love/export", ExportAsync).RequireAuthorization(Admin);
        app.MapPost("/admin/love/delete", DeleteResponseAsync).RequireAuthorization(Admin).DisableAntiforgery();
        app.MapPost("/admin/ebooks/save", SaveBookAsync).RequireAuthorization(Admin).DisableAntiforgery();
        app.MapPost("/admin/ebooks/toggle", ToggleBookAsync).RequireAuthorization(Admin).DisableAntiforgery();
        app.MapPost("/admin/ebooks/delete", DeleteBookAsync).RequireAuthorization(Admin).DisableAntiforgery();
    }

    private static void Admin(Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder policy) =>
        policy.RequireRole(AuthRoles.Admin);

    // 설문을 마친 사람의 응답 (쿠키 토큰으로 찾음)
    public static async Task<LoveSurveyResponse?> FindResponseAsync(HttpContext context, AppDbContext db)
    {
        var token = context.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(token) || token.Length != 32)
            return null;
        return await db.LoveSurveyResponses.AsNoTracking()
            .Where(r => r.Token == token)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();
    }

    private static async Task<IResult> SubmitAsync(HttpContext context, IDbContextFactory<AppDbContext> dbFactory)
    {
        var form = await context.Request.ReadFormAsync();
        var input = LoveSurveyInput.FromForm(form);
        if (input.Website is not null)
            return Results.Redirect(PagePath);

        var error = LoveSurveyValidator.Validate(input);
        if (error is not null)
        {
            context.Session.SetString(DraftKey, JsonSerializer.Serialize(input));
            return Results.Redirect($"{PagePath}?error={error}#survey");
        }

        context.Session.Remove(DraftKey);
        var response = new LoveSurveyResponse
        {
            Status = input.Status,
            Mbti = input.Mbti,
            PartnerMbti = input.PartnerMbti,
            Gender = input.Gender,
            AgeRange = input.AgeRange,
            Concern = input.Concern,
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()
        };

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.LoveSurveyResponses.Add(response);
            await db.SaveChangesAsync();
        }

        context.Response.Cookies.Append(CookieName, response.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(180),
            IsEssential = true
        });

        return Results.Redirect($"{PagePath}?opened=1#books");
    }

    public static LoveSurveyInput? TakeDraft(HttpContext context)
    {
        var json = context.Session.GetString(DraftKey);
        if (json is null)
            return null;
        context.Session.Remove(DraftKey);
        try
        {
            return JsonSerializer.Deserialize<LoveSurveyInput>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<IResult> ReadBookAsync(int bookId, HttpContext context, IDbContextFactory<AppDbContext> dbFactory, EBookStore store)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await FindResponseAsync(context, db) is null)
            return Results.Redirect($"{PagePath}?error=book#survey");

        var book = await db.EBooks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId && b.IsVisible);
        var path = book is null ? null : store.GetPath(book.FileName);
        if (book is null || path is null)
            return Results.Redirect($"{PagePath}#books");

        context.Response.Headers.CacheControl = "private, max-age=3600";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers.ContentDisposition =
            $"inline; filename=\"ebook.pdf\"; filename*=UTF-8''{Uri.EscapeDataString(book.Title + ".pdf")}";
        return Results.File(path, "application/pdf", enableRangeProcessing: true);
    }

    private static async Task<IResult> ExportAsync(IDbContextFactory<AppDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var responses = await db.LoveSurveyResponses.AsNoTracking().OrderByDescending(r => r.Id).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("연애 설문");
        string[] headers = ["번호", "응답 시각", "연애 상황", "내 MBTI", "상대 MBTI", "성별", "나이대", "고민"];
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).SetValue(headers[c]);
        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F4D6DE");

        for (var i = 0; i < responses.Count; i++)
        {
            var r = responses[i];
            var row = i + 2;
            sheet.Cell(row, 1).SetValue(r.Id);
            sheet.Cell(row, 2).SetValue(r.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            sheet.Cell(row, 3).SetValue(r.Status);
            sheet.Cell(row, 4).SetValue(r.Mbti);
            sheet.Cell(row, 5).SetValue(r.PartnerMbti ?? "");
            sheet.Cell(row, 6).SetValue(r.Gender ?? "");
            sheet.Cell(row, 7).SetValue(r.AgeRange ?? "");
            sheet.Cell(row, 8).SetValue(r.Concern);
        }

        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns(1, 7).AdjustToContents(6, 20);
        sheet.Column(8).Width = 80;
        sheet.Column(8).Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Results.File(stream.ToArray(), ApplicationExcelExporter.ContentType,
            $"연애설문_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    private static async Task<IResult> DeleteResponseAsync(HttpContext context, IDbContextFactory<AppDbContext> dbFactory)
    {
        var form = await context.Request.ReadFormAsync();
        if (int.TryParse(form["id"], out var id))
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.LoveSurveyResponses.Where(r => r.Id == id).ExecuteDeleteAsync();
        }
        return Results.Redirect("/admin/love");
    }

    private static async Task<IResult> SaveBookAsync(HttpContext context, IDbContextFactory<AppDbContext> dbFactory, EBookStore store)
    {
        var form = await context.Request.ReadFormAsync();
        var title = form["title"].ToString().Trim();
        var audience = form["audience"].ToString();
        var mbti = form["mbti"].ToString().Trim().ToUpperInvariant();
        if (title.Length is 0 or > 100)
            return Results.Redirect("/admin/love?tab=books&error=title");
        if (!LoveSurveyOptions.Audiences.Contains(audience))
            audience = LoveSurveyOptions.Common;
        if (!LoveSurveyOptions.MbtiTypes.Contains(mbti))
            mbti = "";

        await using var db = await dbFactory.CreateDbContextAsync();
        EBook? book = null;
        if (int.TryParse(form["id"], out var id))
            book = await db.EBooks.FindAsync(id);

        var file = form.Files.GetFile("file");
        string? newFile = null;
        if (file is not null && file.Length > 0)
        {
            newFile = await store.SaveAsync(file);
            if (newFile is null)
                return Results.Redirect("/admin/love?tab=books&error=pdf");
        }
        else if (book is null)
        {
            return Results.Redirect("/admin/love?tab=books&error=pdf");
        }

        if (book is null)
        {
            book = new EBook { SortOrder = (await db.EBooks.MaxAsync(b => (int?)b.SortOrder) ?? -1) + 1 };
            db.EBooks.Add(book);
        }

        var oldFile = newFile is not null ? book.FileName : null;
        book.Title = title;
        book.Subtitle = Trim(form["subtitle"].ToString(), 200);
        book.Audience = audience;
        book.Mbti = mbti.Length == 0 ? null : mbti;
        if (int.TryParse(form["sortOrder"], out var sortOrder))
            book.SortOrder = sortOrder;
        if (newFile is not null)
            book.FileName = newFile;

        await db.SaveChangesAsync();
        if (!string.IsNullOrEmpty(oldFile))
            store.Delete(oldFile);

        return Results.Redirect("/admin/love?tab=books&saved=1");
    }

    private static async Task<IResult> ToggleBookAsync(HttpContext context, IDbContextFactory<AppDbContext> dbFactory)
    {
        var form = await context.Request.ReadFormAsync();
        if (int.TryParse(form["id"], out var id))
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var book = await db.EBooks.FindAsync(id);
            if (book is not null)
            {
                book.IsVisible = !book.IsVisible;
                await db.SaveChangesAsync();
            }
        }
        return Results.Redirect("/admin/love?tab=books");
    }

    private static async Task<IResult> DeleteBookAsync(HttpContext context, IDbContextFactory<AppDbContext> dbFactory, EBookStore store)
    {
        var form = await context.Request.ReadFormAsync();
        if (int.TryParse(form["id"], out var id))
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var book = await db.EBooks.FindAsync(id);
            if (book is not null)
            {
                db.EBooks.Remove(book);
                await db.SaveChangesAsync();
                store.Delete(book.FileName);
            }
        }
        return Results.Redirect("/admin/love?tab=books");
    }

    private static string? Trim(string value, int max)
    {
        value = value.Trim();
        if (value.Length == 0)
            return null;
        return value.Length > max ? value[..max] : value;
    }
}
