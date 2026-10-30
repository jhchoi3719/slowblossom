using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services.ApplicationForm;

public static class ApplicationFormEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/apply/{eventId:int}/submit", SubmitAsync).DisableAntiforgery();
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
        input.HasPhoto = photo is { Length: > 0 };
        input.HasEmploymentProof = employmentProof is { Length: > 0 };
        input.HasMarriageCert = marriageCert is { Length: > 0 };

        var errors = ApplicationFormValidator.Validate(input, evt);
        if (errors.Count > 0)
        {
            ApplicationDraftStore.Save(context, eventId, input, errors);
            return Results.Redirect($"/apply/{eventId}?error=1");
        }

        var phone = ApplicationFormValidator.NormalizePhone(input.Phone);
        var existingPhones = await db.Applications
            .Where(a => a.EventId == eventId && a.Phone != null)
            .Select(a => a.Phone!)
            .ToListAsync();
        if (existingPhones.Any(p => ApplicationFormValidator.NormalizePhone(p) == phone))
        {
            ApplicationDraftStore.Save(context, eventId, input, []);
            return Results.Redirect($"/apply/{eventId}?error=duplicate");
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
            Gender = input.Gender,
            Phone = phone,
            Residence = input.Residence.Trim(),
            KakaoId = input.KakaoId.Trim(),
            Education = input.Education.Trim(),
            Workplace = input.Workplace.Trim(),
            PreferredAgeRange = input.PreferredAgeRange,
            Interests = string.Join(", ", input.Interests),
            ReferralSource = input.ReferralSource,
            Motivation = input.Motivation.Trim(),
            Drinking = input.Drinking,
            Smoking = input.Smoking,
            SelfIntro = input.SelfIntro.Trim(),
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
                [ex.Message == "size"
                    ? "사진 한 장의 용량은 10MB 이하여야 해요."
                    : "사진은 jpg, jpeg, png 파일만 첨부할 수 있어요."]);
            return Results.Redirect($"/apply/{eventId}?error=1");
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
