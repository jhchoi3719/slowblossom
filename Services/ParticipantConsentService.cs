using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public class ParticipantConsentService
{
    public const string DefaultTitle = "참가자 확인 및 동의";

    public const string DefaultContent = """
        안녕하세요, slow blossom 로테이션 소개팅에 참여해 주셔서 감사합니다.

        본 행사는 불특정 다수를 대상으로 진행되는 소개팅 행사입니다.
        참가자의 신원·범죄이력·신상정보 등에 대해 주최측이 사전에 확인하거나 보증하지 않습니다.

        행사 참여를 위해 아래 내용을 확인하시고 동의해 주세요.

        【1. 본인 정보에 대한 확인】
        본인은 아래 사항에 문제가 없음을 확인하고 이에 대해 스스로 보증합니다.
        · 범죄이력 및 수사·재판 진행 중인 사실이 없을 것
        · 타인에게 피해를 줄 수 있는 신상정보·행동에 해당하지 않을 것
        · 행사에 제출한 이름, 연락처 등 본인 정보가 사실과 다르지 않을 것

        【2. 주최측 책임의 한계】
        · 참가자 간 대화, 연락, 만남 등 행사 이후 발생하는 모든 문제에 대해 주최측은 책임지지 않습니다.
        · 참가자 간 분쟁, 피해, 손해 등이 발생하더라도 당사자 간 해결을 원칙으로 하며, 주최측은 중재·배상 등의 의무를 부담하지 않습니다.
        · 허위 정보 제공, 타인 사칭, 불법·부적절 행위 등으로 문제가 발생한 경우, 해당 참가자 본인이 모든 책임을 집니다.

        【3. 행사 참여 동의】
        위 내용을 충분히 이해하였으며, 이에 동의한 상태에서 행사에 참여합니다.
        """;

    public static EventVenue VenueFromSession(ParticipantSession session) =>
        session.IsHotelSuseongSquare ? EventVenue.HotelSuseongSquare : EventVenue.UnoCoffee;

    public static string VenueKey(EventVenue venue) => VenueHelper.ToParam(venue);

    public async Task<VenueConsentSetting> GetSettingAsync(AppDbContext db, EventVenue venue)
    {
        var key = VenueKey(venue);
        var setting = await db.ConsentSettings.FirstOrDefaultAsync(s => s.VenueKey == key);
        if (setting is not null)
            return setting;

        return new VenueConsentSetting
        {
            VenueKey = key,
            IsEnabled = false,
            Title = DefaultTitle,
            Content = DefaultContent
        };
    }

    public async Task<bool> NeedsConsentAsync(AppDbContext db, ParticipantApplication application, EventVenue venue)
    {
        if (application.Id <= 0)
            return false;

        var setting = await GetSettingAsync(db, venue);
        if (!setting.IsEnabled)
            return false;

        return application.ConsentAcceptedAt is null;
    }

    public async Task SaveSettingAsync(
        AppDbContext db,
        EventVenue venue,
        bool isEnabled,
        string title,
        string content)
    {
        var key = VenueKey(venue);
        var setting = await db.ConsentSettings.FirstOrDefaultAsync(s => s.VenueKey == key);

        if (setting is null)
        {
            setting = new VenueConsentSetting { VenueKey = key };
            db.ConsentSettings.Add(setting);
        }

        setting.IsEnabled = isEnabled;
        setting.Title = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim();
        setting.Content = string.IsNullOrWhiteSpace(content) ? DefaultContent : content.Trim();
        setting.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task EnsureSeededAsync(AppDbContext db)
    {
        foreach (var venue in new[] { EventVenue.UnoCoffee, EventVenue.HotelSuseongSquare })
        {
            var key = VenueKey(venue);
            if (await db.ConsentSettings.AnyAsync(s => s.VenueKey == key))
                continue;

            db.ConsentSettings.Add(new VenueConsentSetting
            {
                VenueKey = key,
                IsEnabled = false,
                Title = DefaultTitle,
                Content = DefaultContent
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task RedirectIfConsentRequiredAsync(
        ParticipantSession? session,
        NavigationManager navigation,
        IDbContextFactory<AppDbContext> dbFactory)
    {
        if (session is null || session.ApplicationId <= 0)
            return;

        await using var db = await dbFactory.CreateDbContextAsync();
        var application = await db.Applications.FindAsync(session.ApplicationId);
        if (application is null)
            return;

        var venue = VenueFromSession(session);
        if (await NeedsConsentAsync(db, application, venue))
            navigation.NavigateTo("/consent", replace: true);
    }
}
