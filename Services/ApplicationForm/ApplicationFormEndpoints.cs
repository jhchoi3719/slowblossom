using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services.ApplicationForm;

public static class ApplicationFormEndpoints
{
    public const string SubmitRateLimitPolicy = "apply-submit";

    // 같은 접속자는 10분에 30번까지만 제출 (사진으로 디스크를 채우는 스크립트 방지).
    // 통신사·와이파이를 함께 쓰는 실제 신청자들이 막히지 않도록 넉넉하게 잡는다.
    public const int SubmitsPerWindow = 30;

    // Render 앞단의 Cloudflare를 거치면 접속 주소가 Cloudflare 서버로 보여 모든 신청자가
    // 한 묶음으로 세어진다. Cloudflare가 넣어 주는 실제 방문자 주소를 먼저 쓴다.
    public static string ClientKey(HttpContext context)
    {
        var cf = context.Request.Headers["CF-Connecting-IP"].ToString().Trim();
        if (System.Net.IPAddress.TryParse(cf, out var visitor))
            return visitor.ToString();
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public static void AddRateLimit(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(SubmitRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = SubmitsPerWindow,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0
                    }));
            options.OnRejected = (context, _) =>
            {
                var path = context.HttpContext.Request.Path.Value ?? "/apply";
                var page = path.EndsWith("/submit", StringComparison.Ordinal) ? path[..^"/submit".Length] : "/apply";
                context.HttpContext.Response.Redirect($"{page}?error=rate#apply-errors");
                return ValueTask.CompletedTask;
            };
        });
    }

    public static void Map(WebApplication app)
    {
        app.MapPost("/apply/{eventId:int}/submit", SubmitAsync)
            .RequireRateLimiting(SubmitRateLimitPolicy)
            .DisableAntiforgery();

        app.MapGet("/applications/file/{applicationId:int}/{kind}", GetFileAsync)
            .RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin));
        app.MapPost("/applications/apply-settings", SaveApplySettingsAsync)
            .RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin))
            .DisableAntiforgery();
    }

    private static async Task<IResult> GetFileAsync(
        int applicationId,
        string kind,
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        ApplicationFileStore fileStore)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var application = await db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == applicationId);
        var path = application is null ? null : fileStore.GetPath(ApplicationFileStore.FileNameOf(application, kind));
        if (path is null)
            return Results.NotFound();

        // 파일 이름은 바뀌지 않는 GUID라서 관리자 브라우저에 오래 보관해도 안전하다 (공용 캐시는 금지).
        context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        var contentType = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";
        return Results.File(path, contentType);
    }

    private static async Task<IResult> SaveApplySettingsAsync(
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory)
    {
        var form = await context.Request.ReadFormAsync();
        if (!int.TryParse(form["eventId"], out var eventId))
            return Results.Redirect("/applications");

        await using var db = await dbFactory.CreateDbContextAsync();
        var evt = await db.Events.FindAsync(eventId);
        if (evt is null)
            return Results.Redirect("/applications");

        evt.ApplyOpen = form["applyOpen"].ToString() == "true";
        evt.ApplyTitle = string.IsNullOrWhiteSpace(form["applyTitle"]) ? null : form["applyTitle"].ToString().Trim();
        evt.ApplyNotice = ApplicationNoticeDefaults.ToStored(form["applyNotice"].ToString());
        await db.SaveChangesAsync();

        return Results.Redirect(VenueHelper.AdminPageUrl("/applications", VenueHelper.FromEvent(evt), eventId, "applySaved=true"));
    }

    private static async Task<IResult> SubmitAsync(
        int eventId,
        HttpContext context,
        IDbContextFactory<AppDbContext> dbFactory,
        ApplicationFileStore fileStore,
        ApplicationNotifier notifier)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var evt = await db.Events
            .Include(e => e.CandidateDates)
            .FirstOrDefaultAsync(e => e.Id == eventId);
        if (evt is null || !evt.ApplyOpen)
            return Results.Redirect($"/apply/{eventId}");

        var form = await context.Request.ReadFormAsync();

        // 스팸 봇용 숨은 칸: 사람은 채우지 않는다.
        if (!string.IsNullOrWhiteSpace(form["website"]))
            return Results.Redirect($"/apply/{eventId}/done");

        var photo = form.Files.GetFile("photo");
        var employmentProof = form.Files.GetFile("employmentProof");
        var marriageCert = form.Files.GetFile("marriageCert");
        var input = ReadInput(form);
        if (input.IsReturning)
        {
            // 숨겨진 칸에 남은 사진·답변은 받지 않는다 (보이지 않는 사진 오류로 막히는 일 방지).
            input.KeepOnlyReturningFields();
            photo = employmentProof = marriageCert = null;
        }
        input.HasPhoto = photo is { Length: > 0 };
        input.HasEmploymentProof = employmentProof is { Length: > 0 };
        input.HasMarriageCert = marriageCert is { Length: > 0 };

        var errors = ApplicationFormValidator.Validate(input, evt);
        if (errors.Count > 0)
        {
            ApplicationDraftStore.Save(context, eventId, input, errors);
            return Results.Redirect($"/apply/{eventId}?error=1#apply-errors");
        }

        var phone = ApplicationFormValidator.NormalizePhone(input.Phone);
        var existingPhones = await db.Applications
            .Where(a => a.EventId == eventId && a.Phone != null)
            .Select(a => a.Phone!)
            .ToListAsync();
        if (existingPhones.Any(p => ApplicationFormValidator.NormalizePhone(p) == phone))
        {
            ApplicationDraftStore.Save(context, eventId, input, []);
            return Results.Redirect($"/apply/{eventId}?error=duplicate#apply-errors");
        }

        var saved = new List<string>();
        async Task<string?> SaveFileAsync(IFormFile? file)
        {
            var (fileName, error) = await fileStore.TrySaveAsync(file);
            if (error is not null)
                throw new InvalidDataException(error);
            if (fileName is not null)
                saved.Add(fileName);
            return fileName;
        }

        var application = new ParticipantApplication
        {
            EventId = eventId,
            Name = input.Name.Trim(),
            BirthDate = ApplicationFormValidator.NormalizeBirthDate(input.BirthDate),
            Gender = NullIfEmpty(input.Gender),
            Phone = phone,
            Residence = NullIfEmpty(input.Residence),
            KakaoId = NullIfEmpty(input.KakaoId),
            Education = NullIfEmpty(input.Education),
            Workplace = NullIfEmpty(input.Workplace),
            PreferredAgeRange = NullIfEmpty(input.PreferredAgeRange),
            Interests = input.Interests.Count == 0 ? null : string.Join(", ", input.Interests),
            ReferralSource = NullIfEmpty(input.ReferralSource),
            Motivation = NullIfEmpty(input.Motivation),
            Drinking = input.Drinking,
            Smoking = input.Smoking,
            SelfIntro = NullIfEmpty(input.SelfIntro),
            IsReturning = input.IsReturning,
            Source = "web",
            AgreedAt = DateTime.Now
        };

        try
        {
            application.PhotoFile = await SaveFileAsync(photo);
            application.EmploymentProofFile = await SaveFileAsync(employmentProof);
            application.MarriageCertFile = await SaveFileAsync(marriageCert);
        }
        catch (InvalidDataException ex)
        {
            saved.ForEach(fileStore.Delete);
            ApplicationDraftStore.Save(context, eventId, input,
                [ex.Message switch
                {
                    "size" => "사진 한 장의 용량은 10MB 이하여야 해요.",
                    "disk" => "지금은 사진을 받을 수 없어요. 잠시 후 다시 시도하거나 카카오톡 채널 '싱싱한팜'으로 문의해 주세요.",
                    _ => "사진은 jpg, jpeg, png 파일만 첨부할 수 있어요."
                }]);
            return Results.Redirect($"/apply/{eventId}?error=1#apply-errors");
        }

        if (evt.Kind == EventKind.DatePoll)
        {
            foreach (var date in input.AvailableDates.Select(d => d.Date).Distinct())
                application.Availabilities.Add(new ApplicationAvailability { AvailableDate = date });
        }

        db.Applications.Add(application);
        await db.SaveChangesAsync();
        await notifier.NotifyAsync(evt, application);

        ApplicationDraftStore.Clear(context, eventId);
        return Results.Redirect($"/apply/{eventId}/done");
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ApplicationFormInput ReadInput(IFormCollection form)
    {
        string Text(string key) => form[key].ToString().Trim();
        bool Checked(string key) => form[key].ToString() == "true";
        bool? Ox(string key) => form[key].ToString() switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };

        return new ApplicationFormInput
        {
            Name = Text("name"),
            BirthDate = Text("birthDate"),
            Gender = Text("gender"),
            Residence = Text("residence"),
            Phone = Text("phone"),
            KakaoId = Text("kakaoId"),
            Education = Text("education"),
            Workplace = Text("workplace"),
            PreferredAgeRange = Text("preferredAgeRange"),
            Interests = form["interests"].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList(),
            ReferralSource = Text("referralSource"),
            Motivation = Text("motivation"),
            Drinking = Ox("drinking"),
            Smoking = Ox("smoking"),
            SelfIntro = Text("selfIntro"),
            IsReturning = Checked("isReturning"),
            AvailableDates = form["availableDates"]
                .Select(v => DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList(),
            AgreeSelection = Checked("agreeSelection"),
            AgreeMarriage = Checked("agreeMarriage"),
            AgreePhoto = Checked("agreePhoto"),
            AgreeRefund = Checked("agreeRefund"),
            AgreePrivacy = Checked("agreePrivacy")
        };
    }
}
