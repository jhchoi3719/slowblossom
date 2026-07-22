using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Components;
using RotationDating.Web.Data;
using RotationDating.Web.Models;
using RotationDating.Web.Services;
using RotationDating.Web.Services.MailNotification;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "RotationDating.Auth";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        if (!builder.Environment.IsDevelopment())
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    if (!builder.Environment.IsDevelopment())
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddScoped<QuestionCardService>();
builder.Services.AddScoped<SurveyService>();
builder.Services.AddHttpClient(nameof(SeatMatchingService));
builder.Services.AddScoped<SeatMatchingService>();
builder.Services.AddScoped<ParticipantConsentService>();
builder.Services.AddOptions<MailNotificationOptions>()
    .Configure<IConfiguration>((options, configuration) => MailNotificationOptionsSetup.Configure(configuration, options));
builder.Services.AddSingleton<MailNotificationStateStore>();
builder.Services.AddSingleton<MailNotificationSettingsStore>();
builder.Services.AddSingleton<MailNotificationSettingsProvider>();
builder.Services.AddHttpClient<TelegramNotifier>();
builder.Services.AddScoped<NaverImapMailMonitor>();
builder.Services.AddHostedService<MailNotificationBackgroundService>();

var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDir))
    dataDir = Directory.Exists("/data") ? "/data" : builder.Environment.ContentRootPath;
try
{
    Directory.CreateDirectory(dataDir);
}
catch
{
    dataDir = builder.Environment.ContentRootPath;
}
var dbPath = Path.Combine(dataDir, "rotationdating.db");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
    await DatabaseInitializer.InitializeAsync(db);
    await scope.ServiceProvider.GetRequiredService<ParticipantConsentService>().EnsureSeededAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/ping", () => Results.Text("ok", "text/plain"));

app.MapPost("/login", async ([FromForm] string? username, [FromForm] string? password, HttpContext context, IDbContextFactory<AppDbContext> dbFactory, ParticipantConsentService consentService) =>
{
    if (string.IsNullOrWhiteSpace(username))
        return InvalidLoginResponse();

    var name = username.Trim();
    var trimmedPassword = string.IsNullOrWhiteSpace(password) ? null : password.Trim();

    if (AuthRoles.IsAdmin(name))
    {
        var adminClaims = new[]
        {
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, AuthRoles.Admin)
        };
        var adminIdentity = new ClaimsIdentity(adminClaims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(adminIdentity));

        return Results.Redirect("/home");
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    if (name is "테스트우노" or "테스트수성")
    {
        var targetVenue = name == "테스트수성"
            ? EventVenue.HotelSuseongSquare
            : EventVenue.UnoCoffee;
        var targetKind = VenueHelper.ToEventKind(targetVenue);

        var testApplication = await db.Applications
            .Include(a => a.Event)
            .Where(a => a.IsConfirmed && a.Event != null && a.Event.Kind == targetKind)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();

        var testEvent = testApplication?.Event
            ?? await db.Events
                .AsNoTracking()
                .Where(e => e.Kind == targetKind)
                .OrderByDescending(e => e.EventDate)
                .FirstOrDefaultAsync();

        if (testEvent is null)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return InvalidLoginResponse();
        }

        var testVenue = VenueHelper.FromEventKind(testEvent.Kind);
        var testEffectiveDate = EventDateHelper.GetEffectiveLoginDate(testEvent);
        var testClaims = new[]
        {
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, AuthRoles.Participant),
            new Claim(AuthClaims.EventId, testEvent.Id.ToString()),
            new Claim(AuthClaims.EventTitle, testEvent.Title),
            new Claim(AuthClaims.ApplicationId, (testApplication?.Id ?? 0).ToString()),
            new Claim(AuthClaims.Gender, testApplication?.Gender ?? "남"),
            new Claim(AuthClaims.VenueLabel, VenueHelper.DisplayName(testVenue)),
            new Claim(AuthClaims.EventDateLabel, testEffectiveDate?.ToString("yyyy년 M월 d일 (ddd)") ?? "")
        };

        var testIdentity = new ClaimsIdentity(testClaims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(testIdentity));

        return Results.Redirect("/welcome");
    }

    var application = await ParticipantAuthService.FindConfirmedParticipantAsync(db, name);

    if (application?.Event is null)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return InvalidLoginResponse();
    }

    if (!ParticipantAuthService.HasPassword(application))
    {
        ParticipantPasswordSetupStore.SetPendingApplicationId(context, application.Id);
        return Results.Redirect("/set-password");
    }

    if (!ParticipantAuthService.IsValidPasswordFormat(trimmedPassword)
        || trimmedPassword != application.Password)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect(string.IsNullOrEmpty(trimmedPassword) ? "/login?error=required" : "/login?error=password");
    }

    await ParticipantAuthService.SignInParticipantAsync(context, application);
    return await RedirectParticipantAfterSignInAsync(context, application, consentService, dbFactory);
}).DisableAntiforgery();

app.MapPost("/set-password", async ([FromForm] string? password, HttpContext context, IDbContextFactory<AppDbContext> dbFactory, ParticipantConsentService consentService) =>
{
    var applicationId = ParticipantPasswordSetupStore.GetPendingApplicationId(context);
    if (applicationId is null)
        return Results.Redirect("/set-password?error=session");

    var trimmedPassword = password?.Trim();
    if (!ParticipantAuthService.IsValidPasswordFormat(trimmedPassword))
        return Results.Redirect("/set-password?error=format");

    await using var db = await dbFactory.CreateDbContextAsync();
    var application = await db.Applications
        .Include(a => a.Event)
        .FirstOrDefaultAsync(a => a.Id == applicationId.Value && a.IsConfirmed);

    if (application?.Event is null)
    {
        ParticipantPasswordSetupStore.Clear(context);
        return Results.Redirect("/set-password?error=session");
    }

    application.Password = trimmedPassword;
    await db.SaveChangesAsync();

    ParticipantPasswordSetupStore.Clear(context);
    await ParticipantAuthService.SignInParticipantAsync(context, application);
    return await RedirectParticipantAfterSignInAsync(context, application, consentService, dbFactory);
}).DisableAntiforgery();

app.MapPost("/consent/accept", async (HttpContext context, IDbContextFactory<AppDbContext> dbFactory) =>
{
    var session = ParticipantSession.FromClaims(context.User);
    if (session is null || session.ApplicationId <= 0)
        return Results.Redirect("/");

    await using var db = await dbFactory.CreateDbContextAsync();
    var application = await db.Applications.FindAsync(session.ApplicationId);
    if (application is null)
        return Results.Redirect("/");

    application.ConsentAcceptedAt = DateTime.Now;
    await db.SaveChangesAsync();
    return Results.Redirect("/welcome");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Participant)).DisableAntiforgery();

app.MapPost("/admin/consent/save", async (
    [FromForm] string? venue,
    [FromForm] string? isEnabled,
    [FromForm] string? title,
    [FromForm] string? content,
    IDbContextFactory<AppDbContext> dbFactory,
    ParticipantConsentService consentService) =>
{
    var parsedVenue = VenueHelper.TryParse(venue) ?? EventVenue.UnoCoffee;
    await using var db = await dbFactory.CreateDbContextAsync();
    await consentService.SaveSettingAsync(
        db,
        parsedVenue,
        isEnabled == "true",
        title ?? "",
        content ?? "");

    return Results.Redirect(VenueHelper.AdminPageUrl("/admin/consent", parsedVenue, extraQuery: "saved=true"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapGet("/force-logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return InvalidLoginResponse();
});

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/participants/delete", async ([FromForm] int participantId, [FromForm] int eventId, IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var participant = await db.Participants.FindAsync(participantId);
    if (participant is not null)
    {
        db.Participants.Remove(participant);
        await db.SaveChangesAsync();
    }

    await using var db2 = await dbFactory.CreateDbContextAsync();
    var kind = await db2.Events.AsNoTracking()
        .Where(e => e.Id == eventId)
        .Select(e => e.Kind)
        .FirstOrDefaultAsync();
    return Results.Redirect(VenueHelper.AdminPageUrl("/participants", VenueHelper.FromEventKind(kind), eventId));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/participants/reset-password", async (
    [FromForm] int applicationId,
    [FromForm] int eventId,
    [FromForm] string? venue,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var application = await db.Applications.FindAsync(applicationId);
    if (application is not null)
    {
        application.Password = null;
        await db.SaveChangesAsync();
    }

    var parsedVenue = VenueHelper.TryParse(venue) ?? EventVenue.UnoCoffee;
    return Results.Redirect(VenueHelper.AdminPageUrl("/participants", parsedVenue, eventId));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/participants/toggle-arrival", async (
    [FromForm] int applicationId,
    [FromForm] int eventId,
    [FromForm] string? venue,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var application = await db.Applications.FindAsync(applicationId);
    if (application is not null)
    {
        application.HasArrived = !application.HasArrived;
        await db.SaveChangesAsync();
    }

    var parsedVenue = VenueHelper.TryParse(venue) ?? EventVenue.UnoCoffee;
    return Results.Redirect(VenueHelper.AdminPageUrl("/participants", parsedVenue, eventId));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/participants/add-date", async (
    [FromForm] DateTime eventDate,
    [FromForm] string? title,
    [FromForm] string? venue,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    var dateOnly = eventDate.Date;
    await using var db = await dbFactory.CreateDbContextAsync();

    if (await db.Events.AnyAsync(e => e.Kind == EventKind.FixedDate && e.EventDate.Date == dateOnly))
        return Results.Redirect(ParticipantsAddDateUrl("duplicate"));

    var eventTitle = string.IsNullOrWhiteSpace(title)
        ? $"{dateOnly:M월 d일} 로테이션 소개팅"
        : title.Trim();

    var evt = new Event
    {
        Title = eventTitle,
        Kind = EventKind.FixedDate,
        EventDate = dateOnly.AddHours(18),
        Location = VenueHelper.LocationName(EventVenue.UnoCoffee)
    };

    db.Events.Add(evt);
    await db.SaveChangesAsync();
    return Results.Redirect(VenueHelper.AdminPageUrl("/participants", EventVenue.UnoCoffee, evt.Id));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/participants/save", async (
    [FromForm] int eventId,
    [FromForm] int? participantId,
    [FromForm] string? name,
    [FromForm] Gender gender,
    [FromForm] int? age,
    [FromForm] string? phone,
    [FromForm] string? occupation,
    [FromForm] string? memo,
    [FromForm] ParticipantStatus status,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    if (string.IsNullOrWhiteSpace(name))
        return Results.Redirect($"/participants?eventId={eventId}&{(participantId.HasValue ? $"editId={participantId}" : "addParticipant=true")}&error=name");

    if (string.IsNullOrWhiteSpace(phone))
        return Results.Redirect($"/participants?eventId={eventId}&{(participantId.HasValue ? $"editId={participantId}" : "addParticipant=true")}&error=phone");

    await using var db = await dbFactory.CreateDbContextAsync();

    if (participantId.HasValue)
    {
        var existing = await db.Participants.FindAsync(participantId.Value);
        if (existing is null)
            return Results.Redirect($"/participants?eventId={eventId}");

        existing.Name = name.Trim();
        existing.Gender = gender;
        existing.Age = age;
        existing.Phone = phone.Trim();
        existing.Occupation = string.IsNullOrWhiteSpace(occupation) ? null : occupation.Trim();
        existing.Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
        existing.Status = status;
    }
    else
    {
        db.Participants.Add(new Participant
        {
            EventId = eventId,
            Name = name.Trim(),
            Gender = gender,
            Age = age,
            Phone = phone.Trim(),
            Occupation = string.IsNullOrWhiteSpace(occupation) ? null : occupation.Trim(),
            Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim(),
            Status = status
        });
    }

    await db.SaveChangesAsync();
    return Results.Redirect($"/participants?eventId={eventId}");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/delete", async ([FromForm] int applicationId, [FromForm] int eventId, IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var application = await db.Applications.FindAsync(applicationId);
    if (application is not null)
    {
        db.Applications.Remove(application);
        await db.SaveChangesAsync();
    }

    return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/add-date", async (
    [FromForm] string? eventKind,
    [FromForm] string? venue,
    [FromForm] DateTime? eventDate,
    [FromForm] string? title,
    [FromForm] DateTime[]? candidateDates,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var parsedVenue = VenueHelper.TryParse(venue);
    var kind = eventKind == "poll" ? EventKind.DatePoll : EventKind.FixedDate;
    if (parsedVenue.HasValue)
        kind = VenueHelper.ToEventKind(parsedVenue.Value);

    if (kind == EventKind.DatePoll)
    {
        var rawDates = (candidateDates ?? [])
            .Where(d => d != default)
            .Select(d => d.Date)
            .ToList();

        if (rawDates.Count == 0)
            return Results.Redirect(ApplicationsAddDateUrl(VenueHelper.SuseongParam, "pollneed"));

        if (rawDates.Count != rawDates.Distinct().Count())
            return Results.Redirect(ApplicationsAddDateUrl(VenueHelper.SuseongParam, "pollduplicate"));

        var dates = rawDates.Distinct().OrderBy(d => d).ToList();

        var eventTitle = string.IsNullOrWhiteSpace(title)
            ? $"{dates[0]:M월} 호텔수성스퀘어 소개팅"
            : title.Trim();

        var evt = new Event
        {
            Title = eventTitle,
            Kind = EventKind.DatePoll,
            EventDate = dates[0].AddHours(18),
            Location = VenueHelper.LocationName(EventVenue.HotelSuseongSquare),
            CandidateDates = dates.Select((d, i) => new EventCandidateDate
            {
                EventDate = d,
                SortOrder = i + 1
            }).ToList()
        };

        db.Events.Add(evt);
        await db.SaveChangesAsync();
        return Results.Redirect(ApplicationsUrl(EventKind.DatePoll, evt.Id));
    }

    if (eventDate is null)
        return Results.Redirect(ApplicationsAddDateUrl(VenueHelper.UnoParam, "date"));

    var dateOnly = eventDate.Value.Date;

    if (await db.Events.AnyAsync(e => e.Kind == EventKind.FixedDate && e.EventDate.Date == dateOnly))
        return Results.Redirect(ApplicationsAddDateUrl(VenueHelper.UnoParam, "duplicate"));

    var fixedTitle = string.IsNullOrWhiteSpace(title)
        ? $"{dateOnly:M월 d일} 로테이션 소개팅"
        : title.Trim();

    var fixedEvent = new Event
    {
        Title = fixedTitle,
        Kind = EventKind.FixedDate,
        EventDate = dateOnly.AddHours(18),
        Location = VenueHelper.LocationName(EventVenue.UnoCoffee)
    };

    db.Events.Add(fixedEvent);
    await db.SaveChangesAsync();
    return Results.Redirect(ApplicationsUrl(EventKind.FixedDate, fixedEvent.Id));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/update-date", async (
    [FromForm] int eventId,
    [FromForm] DateTime eventDate,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    var dateOnly = eventDate.Date;
    await using var db = await dbFactory.CreateDbContextAsync();
    var evt = await db.Events.FindAsync(eventId);

    if (evt is null || evt.Kind != EventKind.FixedDate)
        return Results.Redirect("/home");

    if (await db.Events.AnyAsync(e =>
            e.Id != eventId && e.Kind == EventKind.FixedDate && e.EventDate.Date == dateOnly))
        return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, "editDate=true&error=duplicate"));

    evt.EventDate = dateOnly.AddHours(18);
    ParticipantEventInfoService.SyncFixedDateTitle(evt, dateOnly);
    await db.SaveChangesAsync();
    return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, "dateUpdated=true"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/save", async (
    [FromForm] int eventId,
    [FromForm] int? applicationId,
    [FromForm] string? name,
    [FromForm] string? birthDate,
    [FromForm] string? gender,
    [FromForm] string? phone,
    [FromForm] string? residence,
    [FromForm] string? workplace,
    [FromForm] string? preferredAgeRange,
    [FromForm] string? interests,
    [FromForm] string? drinking,
    [FromForm] string? smoking,
    [FromForm] DateTime[]? availableDates,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    if (string.IsNullOrWhiteSpace(name))
        return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, (applicationId.HasValue ? $"editId={applicationId}" : "addApplication=true") + "&error=name"));

    await using var db = await dbFactory.CreateDbContextAsync();
    var evt = await db.Events
        .Include(e => e.CandidateDates)
        .FirstOrDefaultAsync(e => e.Id == eventId);

    if (evt is null)
        return Results.Redirect("/home");

    if (evt.Kind == EventKind.DatePoll && (availableDates is null || availableDates.Length == 0))
        return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, (applicationId.HasValue ? $"editId={applicationId}" : "addApplication=true") + "&error=availability"));

    void ApplyFields(ParticipantApplication app)
    {
        app.Name = name.Trim();
        app.BirthDate = TrimOrNull(birthDate);
        app.Gender = TrimOrNull(gender);
        app.Phone = TrimOrNull(phone);
        app.Residence = TrimOrNull(residence);
        app.Workplace = TrimOrNull(workplace);
        app.PreferredAgeRange = TrimOrNull(preferredAgeRange);
        app.Interests = TrimOrNull(interests);
        app.Drinking = ParseOx(drinking);
        app.Smoking = ParseOx(smoking);
    }

    ParticipantApplication target;

    if (applicationId.HasValue)
    {
        target = await db.Applications
            .Include(a => a.Availabilities)
            .FirstOrDefaultAsync(a => a.Id == applicationId.Value)
            ?? new ParticipantApplication { EventId = eventId };

        if (target.Id == 0)
            return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId));

        ApplyFields(target);
    }
    else
    {
        target = new ParticipantApplication { EventId = eventId, AllowContact = false };
        ApplyFields(target);
        db.Applications.Add(target);
        await db.SaveChangesAsync();
    }

    if (evt.Kind == EventKind.DatePoll)
        await ReplaceAvailabilitiesAsync(db, target, availableDates!, evt);

    await db.SaveChangesAsync();
    return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/bulk-import", async (
    [FromForm] int eventId,
    [FromForm] string? pasteData,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    if (string.IsNullOrWhiteSpace(pasteData))
        return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, "bulkAdd=true&error=empty"));

    await using var db = await dbFactory.CreateDbContextAsync();
    var evt = await db.Events
        .Include(e => e.CandidateDates)
        .FirstOrDefaultAsync(e => e.Id == eventId);

    if (evt is null)
        return Results.Redirect("/home");

    var isPoll = evt.Kind == EventKind.DatePoll;
    var allowedDates = evt.CandidateDates.Select(c => c.EventDate.Date).ToList();
    var parsed = BulkApplicationParser.Parse(pasteData, eventId, isPoll, allowedDates);
    if (parsed.Count == 0)
        return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, "bulkAdd=true&error=noparse"));

    foreach (var item in parsed)
    {
        if (isPoll)
        {
            item.AvailableDates = item.AvailableDates
                .Where(d => allowedDates.Contains(d.Date))
                .Distinct()
                .ToList();

            if (item.AvailableDates.Count == 0)
                return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, "bulkAdd=true&error=availability"));
        }

        db.Applications.Add(item.Application);
        await db.SaveChangesAsync();

        if (isPoll)
            await ReplaceAvailabilitiesAsync(db, item.Application, item.AvailableDates, evt);
    }

    await db.SaveChangesAsync();
    return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, $"bulkImported={parsed.Count}"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/finalize-poll", async (
    [FromForm] int eventId,
    [FromForm] DateTime finalizedDate,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var evt = await db.Events
        .Include(e => e.CandidateDates)
        .FirstOrDefaultAsync(e => e.Id == eventId);

    if (evt is null || evt.Kind != EventKind.DatePoll)
        return Results.Redirect("/home");

    var dateOnly = finalizedDate.Date;
    if (!evt.CandidateDates.Any(c => c.EventDate.Date == dateOnly))
        return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, "error=invalidfinal"));

    evt.FinalizedDate = dateOnly;
    evt.EventDate = dateOnly.AddHours(18);

    var applications = await db.Applications
        .Include(a => a.Availabilities)
        .Where(a => a.EventId == eventId)
        .ToListAsync();

    foreach (var application in applications)
    {
        if (!application.Availabilities.Any(a => a.AvailableDate.Date == dateOnly))
            application.IsConfirmed = false;
    }

    await db.SaveChangesAsync();
    return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, "finalized=true"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/set-contact", async (
    [FromForm] int applicationId,
    [FromForm] int eventId,
    [FromForm] string contacted,
    [FromForm] string? gender,
    [FromForm] string? confirmed,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var application = await db.Applications.FindAsync(applicationId);
    if (application is not null)
    {
        var newValue = ParseOx(contacted);
        application.AllowContact = newValue switch
        {
            true when application.AllowContact == true => null,
            false when application.AllowContact == false => null,
            _ => newValue
        };
        await db.SaveChangesAsync();
    }

    var filterQuery = BuildApplicationsListFilterQuery(gender, confirmed);
    return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, filterQuery, fragment: $"app-{applicationId}"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/set-confirmed", async (
    [FromForm] int applicationId,
    [FromForm] int eventId,
    [FromForm] string confirmed,
    [FromForm] string? gender,
    [FromForm] string? confirmedFilter,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var application = await db.Applications
        .Include(a => a.Event)
        .Include(a => a.Availabilities)
        .FirstOrDefaultAsync(a => a.Id == applicationId);

    var filterQuery = BuildApplicationsListFilterQuery(gender, confirmedFilter);

    if (application is not null)
    {
        if (confirmed == "true" && !EventDateHelper.CanConfirmOnPoll(application))
            return Results.Redirect(await ApplicationsUrlForEventAsync(
                dbFactory,
                eventId,
                MergeApplicationListQuery(filterQuery, "error=confirm"),
                fragment: $"app-{applicationId}"));

        application.IsConfirmed = confirmed == "true";
        await db.SaveChangesAsync();
    }

    return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, filterQuery, fragment: $"app-{applicationId}"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/applications/set-memo", async (
    [FromForm] int applicationId,
    [FromForm] int eventId,
    [FromForm] string? memo,
    [FromForm] string? gender,
    [FromForm] string? confirmedFilter,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var application = await db.Applications.FindAsync(applicationId);
    if (application is not null)
    {
        application.Memo = TrimOrNull(memo);
        await db.SaveChangesAsync();
    }

    var filterQuery = BuildApplicationsListFilterQuery(gender, confirmedFilter);
    return Results.Redirect(await ApplicationsUrlForEventAsync(dbFactory, eventId, filterQuery, fragment: $"app-{applicationId}"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/events/delete", async (
    [FromForm] int eventId,
    [FromForm] string returnPage,
    [FromForm] string? venue,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var evt = await db.Events.FindAsync(eventId);
    EventKind kind = evt?.Kind ?? EventKind.FixedDate;
    if (evt is not null)
    {
        kind = evt.Kind;
        db.Events.Remove(evt);
        await db.SaveChangesAsync();
    }

    var parsedVenue = VenueHelper.TryParse(venue) ?? VenueHelper.FromEventKind(kind);
    var path = returnPage == "participants" ? "/participants" : "/applications";
    return Results.Redirect(VenueHelper.AdminPageUrl(path, parsedVenue));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/questions/save", async (
    [FromForm] int id,
    [FromForm] string? text,
    [FromForm] string? venue,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    var venueQuery = VenueHelper.TryParse(venue) is { } parsedVenue
        ? $"&{VenueHelper.VenueQuery(parsedVenue)}"
        : "";

    if (string.IsNullOrWhiteSpace(text))
        return Results.Redirect($"/admin/questions?error=empty&id={id}{venueQuery}");

    await using var db = await dbFactory.CreateDbContextAsync();
    var question = await db.QuestionCards.FindAsync(id);
    if (question is not null)
    {
        question.Text = text.Trim();
        await db.SaveChangesAsync();
    }

    return Results.Redirect($"/admin/questions?saved=true&id={id}{venueQuery}");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/surveys/create", async (
    [FromForm] string? title,
    SurveyService surveys) =>
{
    if (string.IsNullOrWhiteSpace(title))
        return Results.Redirect("/admin/surveys?error=title");

    var id = await surveys.CreateAsync(title);
    return Results.Redirect($"/admin/surveys/edit?id={id}&created=true");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/surveys/delete", async (
    [FromForm] int id,
    SurveyService surveys) =>
{
    await surveys.DeleteAsync(id);
    return Results.Redirect("/admin/surveys?deleted=true");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/surveys/complete", async (
    [FromForm] int id,
    [FromForm] string? returnTo,
    SurveyService surveys) =>
{
    await surveys.CompleteAsync(id);
    return returnTo == "list"
        ? Results.Redirect("/admin/surveys?completed=true")
        : Results.Redirect($"/admin/surveys/edit?id={id}&completed=true");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/surveys/duplicate", async (
    [FromForm] int id,
    SurveyService surveys) =>
{
    var newId = await surveys.DuplicateAsync(id);
    if (newId is null)
        return Results.Redirect("/admin/surveys");

    return Results.Redirect($"/admin/surveys/edit?id={newId}&copied=true");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/surveys/save-welcome", async (
    [FromForm] int id,
    [FromForm] string? title,
    [FromForm] string? welcomeTitle,
    [FromForm] string? welcomeContent,
    SurveyService surveys) =>
{
    if (string.IsNullOrWhiteSpace(welcomeTitle))
        return Results.Redirect($"/admin/surveys/edit?id={id}&error=title");

    await surveys.SaveWelcomeAsync(
        id,
        welcomeTitle,
        welcomeContent ?? "",
        title);

    return Results.Redirect($"/admin/surveys/edit?id={id}&welcomeSaved=true");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/surveys/questions/save", async (HttpContext context, SurveyService surveys) =>
{
    var form = await context.Request.ReadFormAsync();

    if (!int.TryParse(form["surveyId"], out var surveyId))
        return Results.Redirect("/admin/surveys");

    int? questionId = int.TryParse(form["questionId"], out var parsedQuestionId)
        ? parsedQuestionId
        : null;

    var title = form["title"].ToString();
    var content = form["content"].ToString();
    var questionType = form["questionType"].ToString();
    var isRequired = form["isRequired"].ToString();
    var allowMultiple = form["allowMultiple"].ToString();

    if (string.IsNullOrWhiteSpace(title))
        return Results.Redirect($"/admin/surveys/edit?id={surveyId}&error=title");

    if (!Enum.TryParse<SurveyQuestionType>(questionType, ignoreCase: true, out var parsedType))
        parsedType = SurveyQuestionType.Objective;

    var optionTexts = form["options"].ToList();
    var optionFlags = form["optionAllowsCustom"].ToList();
    var optionList = new List<SurveyOptionInput>();
    for (var i = 0; i < optionTexts.Count; i++)
    {
        var text = optionTexts[i]?.Trim();
        if (string.IsNullOrEmpty(text))
            continue;

        var isCustom = i < optionFlags.Count && optionFlags[i] == "true";
        optionList.Add(new SurveyOptionInput
        {
            Text = text,
            AllowCustomInput = isCustom
        });
    }

    try
    {
        await surveys.SaveQuestionAsync(
            surveyId,
            questionId,
            title,
            content,
            parsedType,
            allowMultiple == "true",
            isRequired == "true",
            optionList);
    }
    catch (InvalidOperationException)
    {
        var editQuery = questionId.HasValue ? $"&editQuestionId={questionId}" : "&addQuestion=true";
        return Results.Redirect($"/admin/surveys/edit?id={surveyId}{editQuery}&error=options");
    }

    return Results.Redirect($"/admin/surveys/edit?id={surveyId}&questionSaved=true");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/surveys/questions/delete", async (
    [FromForm] int surveyId,
    [FromForm] int questionId,
    SurveyService surveys) =>
{
    await surveys.DeleteQuestionAsync(surveyId, questionId);
    return Results.Redirect($"/admin/surveys/edit?id={surveyId}&questionDeleted=true");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/s/{slug}/submit", async (string slug, HttpContext context, SurveyService surveys) =>
{
    var form = await context.Request.ReadFormAsync();
    var input = new SurveySubmitInput();

    foreach (var key in form.Keys)
    {
        if (key.EndsWith("_text", StringComparison.Ordinal) && key.StartsWith("q_", StringComparison.Ordinal))
        {
            var qIdStr = key["q_".Length..^"_text".Length];
            if (int.TryParse(qIdStr, out var qId))
                input.TextAnswers[qId] = form[key].ToString();
        }
        else if (key.Contains("_custom_", StringComparison.Ordinal) && key.StartsWith("q_", StringComparison.Ordinal))
        {
            var body = key["q_".Length..];
            var parts = body.Split("_custom_", 2, StringSplitOptions.None);
            if (parts.Length == 2
                && int.TryParse(parts[0], out _)
                && int.TryParse(parts[1], out _))
            {
                input.CustomOptionTexts[$"{parts[0]}_{parts[1]}"] = form[key].ToString();
            }
        }
        else if (key.StartsWith("q_", StringComparison.Ordinal))
        {
            var qIdStr = key["q_".Length..];
            if (int.TryParse(qIdStr, out var qId))
            {
                var values = form[key]
                    .Where(v => !string.IsNullOrWhiteSpace(v) && int.TryParse(v, out _))
                    .Select(v => int.Parse(v!))
                    .ToList();

                if (values.Count > 0)
                    input.SelectedOptions[qId] = values;
            }
        }
    }

    input.MarketingConsent = form["marketingConsent"].ToString() == "true";
    input.PhoneNumber = form["phoneNumber"].ToString();

    var (success, error) = await surveys.SubmitResponseAsync(slug, input);
    if (!success)
    {
        SurveyResponseDraftStore.Save(context, slug, input);

        if (error == "notfound")
            return Results.Redirect($"/s/{slug}/take");

        if (error == "consent")
            return Results.Redirect($"/s/{slug}/take?error=consent");

        if (error == "phone")
            return Results.Redirect($"/s/{slug}/take?error=phone");

        if (error == "duplicate")
            return Results.Redirect($"/s/{slug}/take?error=duplicate");

        if (error?.StartsWith("required_", StringComparison.Ordinal) == true)
        {
            var qId = error["required_".Length..];
            return Results.Redirect($"/s/{slug}/take?error=required&questionId={qId}");
        }

        if (error?.StartsWith("custom_", StringComparison.Ordinal) == true)
        {
            var qId = error["custom_".Length..];
            return Results.Redirect($"/s/{slug}/take?error=custom&questionId={qId}");
        }
    }

    SurveyResponseDraftStore.Clear(context, slug);
    return Results.Redirect($"/s/{slug}/complete");
}).DisableAntiforgery();

app.MapPost("/vote/save", async (
    HttpContext context,
    [FromForm] string voteType,
    [FromForm] int? targetId,
    [FromForm] string? targetId1,
    [FromForm] string? targetId2,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    var session = ParticipantSession.FromClaims(context.User);
    if (session is null)
        return Results.Redirect("/login");

    if (!Enum.TryParse<VoteType>(voteType, ignoreCase: true, out var parsedVoteType))
        return Results.Redirect("/welcome");

    if (session.IsHotelSuseongSquare && parsedVoteType == VoteType.Mid)
        return Results.Redirect("/vote/final");

    await using var db = await dbFactory.CreateDbContextAsync();

    var voter = await db.Applications.FindAsync(session.ApplicationId);
    if (voter is null || !voter.IsConfirmed || voter.EventId != session.EventId)
        return Results.Redirect("/login");

    var oppositeGender = session.OppositeGender;
    if (string.IsNullOrEmpty(oppositeGender))
        return Results.Redirect("/welcome");

    async Task<int?> ResolveTargetAsync(int? candidateId)
    {
        if (candidateId is not > 0 || candidateId == session.ApplicationId)
            return null;

        var isValidTarget = await db.Applications.AnyAsync(a =>
            a.Id == candidateId
            && a.EventId == session.EventId
            && a.IsConfirmed
            && a.Gender == oppositeGender);

        return isValidTarget ? candidateId : null;
    }

    var existing = await db.Votes
        .Where(v => v.VoterApplicationId == session.ApplicationId && v.VoteType == parsedVoteType)
        .ToListAsync();
    db.Votes.RemoveRange(existing);

    if (parsedVoteType == VoteType.Final)
    {
        if (string.IsNullOrWhiteSpace(targetId1))
            return Results.Redirect("/vote/final?error=required1");

        int? first = null;
        int? second = null;
        var firstIsNone = string.Equals(targetId1.Trim(), "none", StringComparison.OrdinalIgnoreCase);
        var secondIsNone = !firstIsNone
            && string.Equals(targetId2?.Trim(), "none", StringComparison.OrdinalIgnoreCase);

        if (!firstIsNone && int.TryParse(targetId1, out var parsedFirst))
            first = await ResolveTargetAsync(parsedFirst);

        if (!firstIsNone && !secondIsNone && int.TryParse(targetId2, out var parsedSecond))
            second = await ResolveTargetAsync(parsedSecond);

        if (!firstIsNone && first is null)
            return Results.Redirect("/vote/final?error=required1");

        if (first is not null && second is not null && first == second)
            return Results.Redirect("/vote/final?error=same");

        if (firstIsNone)
        {
            db.Votes.Add(new ParticipantVote
            {
                EventId = session.EventId,
                VoterApplicationId = session.ApplicationId,
                TargetApplicationId = session.ApplicationId,
                VoteType = parsedVoteType,
                Priority = 1,
                IsExplicitNone = true
            });
        }
        else if (first is not null)
        {
            db.Votes.Add(new ParticipantVote
            {
                EventId = session.EventId,
                VoterApplicationId = session.ApplicationId,
                TargetApplicationId = first.Value,
                VoteType = parsedVoteType,
                Priority = 1
            });
        }

        if (secondIsNone)
        {
            db.Votes.Add(new ParticipantVote
            {
                EventId = session.EventId,
                VoterApplicationId = session.ApplicationId,
                TargetApplicationId = session.ApplicationId,
                VoteType = parsedVoteType,
                Priority = 2,
                IsExplicitNone = true
            });
        }
        else if (!firstIsNone && second is not null)
        {
            db.Votes.Add(new ParticipantVote
            {
                EventId = session.EventId,
                VoterApplicationId = session.ApplicationId,
                TargetApplicationId = second.Value,
                VoteType = parsedVoteType,
                Priority = 2
            });
        }
    }
    else
    {
        var selectedId = await ResolveTargetAsync(targetId);
        if (selectedId is not null)
        {
            db.Votes.Add(new ParticipantVote
            {
                EventId = session.EventId,
                VoterApplicationId = session.ApplicationId,
                TargetApplicationId = selectedId.Value,
                VoteType = parsedVoteType,
                Priority = 1
            });
        }
    }

    await db.SaveChangesAsync();

    var redirectPath = parsedVoteType == VoteType.Mid ? "/vote/mid" : "/vote/final";
    return Results.Redirect($"{redirectPath}?saved=true");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Participant)).DisableAntiforgery();

app.MapPost("/vote/final/generate", async (
    [FromForm] int eventId,
    [FromForm] string? venue,
    SeatMatchingService seatMatching) =>
{
    var (_, error) = await seatMatching.GenerateFinalCoupleMatchingAsync(eventId);

    var parsedVenue = VenueHelper.TryParse(venue) ?? EventVenue.UnoCoffee;
    var extraQuery = string.IsNullOrEmpty(error)
        ? "generated=true"
        : $"generateError={Uri.EscapeDataString(error)}";

    return Results.Redirect(VenueHelper.AdminPageUrl("/vote-results/final", parsedVenue, eventId, extraQuery));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/seating/mid/generate", async (
    [FromForm] int eventId,
    SeatMatchingService seatMatching,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    var (_, _, error) = await seatMatching.GenerateMidVoteSeatingAsync(eventId);

    if (!string.IsNullOrEmpty(error))
        return Results.Redirect(await SeatingUrlForEventAsync(dbFactory, "/seating/mid", eventId, $"generateError={Uri.EscapeDataString(error)}"));

    return Results.Redirect(await SeatingUrlForEventAsync(dbFactory, "/seating/mid", eventId, "generated=true"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/seating/initial/generate", async (
    [FromForm] int eventId,
    SeatMatchingService seatMatching,
    IDbContextFactory<AppDbContext> dbFactory) =>
{
    var (_, _, error) = await seatMatching.GenerateInitialSeatingAsync(eventId);

    if (!string.IsNullOrEmpty(error))
        return Results.Redirect(await SeatingUrlForEventAsync(dbFactory, "/seating/initial", eventId, $"generateError={Uri.EscapeDataString(error)}"));

    return Results.Redirect(await SeatingUrlForEventAsync(dbFactory, "/seating/initial", eventId, "generated=true"));
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/mail-notify/save", async (
    HttpContext httpContext,
    [FromForm] string? username,
    [FromForm] string? password,
    [FromForm] string? subjectContains,
    [FromForm] string? fromFilter,
    [FromForm] int pollIntervalSeconds,
    MailNotificationSettingsProvider settingsProvider) =>
{
    var stored = await settingsProvider.GetStoredSettingsAsync();
    var effective = await settingsProvider.GetEffectiveSettingsAsync();
    var resolvedUsername = effective.UsernameLockedByEnv
        ? stored.Username
        : username?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(resolvedUsername))
        return Results.Redirect("/admin/mail-notify?error=save");

    await settingsProvider.SaveImapSettingsAsync(new MailNotificationStoredSettings
    {
        Enabled = IsFormCheckboxChecked(httpContext.Request.Form, "enabled"),
        Username = resolvedUsername,
        SubjectContains = effective.SubjectLockedByEnv
            ? stored.SubjectContains
            : subjectContains,
        FromFilter = effective.FromFilterLockedByEnv
            ? stored.FromFilter
            : fromFilter,
        PollIntervalSeconds = pollIntervalSeconds
    }, effective.PasswordLockedByEnv ? null : password);

    return Results.Redirect("/admin/mail-notify?saved=1");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/mail-notify/save-telegram", async (
    [FromForm] string? botToken,
    [FromForm] string? chatId,
    MailNotificationSettingsProvider settingsProvider) =>
{
    if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        return Results.Redirect("/admin/mail-notify?error=saveTelegram");

    await settingsProvider.SaveTelegramSettingsAsync(botToken, chatId);
    return Results.Redirect("/admin/mail-notify?savedTelegram=1");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/mail-notify/test", async (TelegramNotifier telegramNotifier) =>
{
    var result = await telegramNotifier.SendMessageAsync("Slowblossom 신청 알림 테스트입니다.");
    if (result.Success)
        return Results.Redirect("/admin/mail-notify?sent=1");

    var error = result.ErrorCode switch
    {
        "noTelegram" => "noTelegram",
        _ => "send"
    };

    var detail = Uri.EscapeDataString(result.ErrorMessage ?? result.ErrorCode ?? "unknown");
    return Results.Redirect($"/admin/mail-notify?error={error}&detail={detail}");
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapPost("/admin/mail-notify/check-now", async (NaverImapMailMonitor mailMonitor) =>
{
    try
    {
        var result = await mailMonitor.SendLatestNaverFormNotificationAsync();
        if (result.Success)
        {
            var subject = Uri.EscapeDataString(result.SentSubject ?? "");
            return Results.Redirect($"/admin/mail-notify?checked=1&detail={subject}");
        }

        var error = result.ErrorCode switch
        {
            "noMail" => "noMail",
            "noTelegram" => "noTelegram",
            "notConfigured" => "imap",
            _ => "send"
        };
        var detail = Uri.EscapeDataString(result.ErrorMessage ?? result.ErrorCode ?? "unknown");
        return Results.Redirect($"/admin/mail-notify?error={error}&detail={detail}");
    }
    catch
    {
        return Results.Redirect("/admin/mail-notify?error=imap");
    }
}).RequireAuthorization(policy => policy.RequireRole(AuthRoles.Admin)).DisableAntiforgery();

app.MapRazorComponents<App>();

app.Run();

static string ApplicationsUrl(EventKind kind, int? eventId = null, string? extraQuery = null, string? fragment = null) =>
    VenueHelper.AdminPageUrl("/applications", VenueHelper.FromEventKind(kind), eventId, extraQuery, fragment);

static string ApplicationsAddDateUrl(string venueParam, string error) =>
    VenueHelper.AdminPageUrl(
        "/applications",
        VenueHelper.TryParse(venueParam) ?? EventVenue.UnoCoffee,
        extraQuery: $"addDate=true&error={error}");

static string ParticipantsAddDateUrl(string error) =>
    VenueHelper.AdminPageUrl("/participants", EventVenue.UnoCoffee, extraQuery: $"addDate=true&error={error}");

static async Task<string> ApplicationsUrlForEventAsync(
    IDbContextFactory<AppDbContext> dbFactory,
    int eventId,
    string? extraQuery = null,
    string? fragment = null)
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var kind = await db.Events.AsNoTracking()
        .Where(e => e.Id == eventId)
        .Select(e => e.Kind)
        .FirstOrDefaultAsync();
    return ApplicationsUrl(kind, eventId, extraQuery, fragment);
}

static async Task<string> SeatingUrlForEventAsync(
    IDbContextFactory<AppDbContext> dbFactory,
    string pagePath,
    int eventId,
    string? extraQuery = null)
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var kind = await db.Events.AsNoTracking()
        .Where(e => e.Id == eventId)
        .Select(e => e.Kind)
        .FirstOrDefaultAsync();
    return VenueHelper.AdminPageUrl(pagePath, VenueHelper.FromEventKind(kind), eventId, extraQuery);
}

static string? MergeApplicationListQuery(string? filterQuery, string? extraQuery)
{
    if (string.IsNullOrWhiteSpace(filterQuery))
        return extraQuery;
    if (string.IsNullOrWhiteSpace(extraQuery))
        return filterQuery;
    return $"{filterQuery}&{extraQuery}";
}

static string? BuildApplicationsListFilterQuery(string? gender, string? confirmed)
{
    var parts = new List<string>();
    if (gender is "남" or "여")
        parts.Add($"gender={Uri.EscapeDataString(gender)}");
    if (confirmed is "confirmed" or "pending")
        parts.Add($"confirmed={Uri.EscapeDataString(confirmed)}");
    return parts.Count == 0 ? null : string.Join("&", parts);
}

static string? TrimOrNull(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static bool? ParseOx(string? value) => value?.Trim().ToLowerInvariant() switch
{
    "true" or "o" => true,
    "false" or "x" => false,
    _ => null
};

static bool IsFormCheckboxChecked(IFormCollection form, string name) =>
    form.TryGetValue(name, out var values)
    && values.Count > 0
    && values.Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

static async Task ReplaceAvailabilitiesAsync(
    AppDbContext db,
    ParticipantApplication application,
    IEnumerable<DateTime> availableDates,
    Event evt)
{
    var allowed = evt.CandidateDates.Select(c => c.EventDate.Date).ToHashSet();
    var existing = await db.ApplicationAvailabilities
        .Where(a => a.ApplicationId == application.Id)
        .ToListAsync();
    db.ApplicationAvailabilities.RemoveRange(existing);

    foreach (var date in availableDates.Select(d => d.Date).Where(allowed.Contains).Distinct())
    {
        db.ApplicationAvailabilities.Add(new ApplicationAvailability
        {
            ApplicationId = application.Id,
            AvailableDate = date
        });
    }
}

static async Task<IResult> RedirectParticipantAfterSignInAsync(
    HttpContext context,
    ParticipantApplication application,
    ParticipantConsentService consentService,
    IDbContextFactory<AppDbContext> dbFactory)
{
    if (application.Id <= 0 || application.Event is null)
        return Results.Redirect("/welcome");

    await using var db = await dbFactory.CreateDbContextAsync();
    var venue = VenueHelper.FromEventKind(application.Event.Kind);
    if (await consentService.NeedsConsentAsync(db, application, venue))
        return Results.Redirect("/consent");

    return Results.Redirect("/welcome");
}

static IResult InvalidLoginResponse() => Results.Content(
    """
    <!DOCTYPE html>
    <html lang="ko">
    <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>로그인</title>
    </head>
    <body>
        <script>
            alert('이름+행사일(예: 홍길동260705)을 확인해주세요');
            location.replace('/login');
        </script>
    </body>
    </html>
    """,
    "text/html; charset=utf-8");
