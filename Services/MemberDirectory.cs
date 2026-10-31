using RotationDating.Web.Models;
using RotationDating.Web.Services.ApplicationForm;

namespace RotationDating.Web.Services;

public sealed record MemberHistoryItem(EventVenue Venue, string EventLabel, bool IsConfirmed, DateTime AppliedAt);

public sealed class Member
{
    public string Name { get; init; } = "";
    public string? Gender { get; init; }
    public string? BirthYearLabel { get; init; }
    public string? Phone { get; init; }
    public string? KakaoId { get; init; }
    public DateTime LastAppliedAt { get; init; }
    public List<MemberHistoryItem> History { get; init; } = [];
    public int ConfirmedCount => History.Count(h => h.IsConfirmed);
}

public sealed record MemberList(List<Member> Male, List<Member> Female, List<Member> Unknown);

// 지금까지의 모든 신청서를 연락처 기준으로 묶어 "회원" 목록을 만든다.
public static class MemberDirectory
{
    public static MemberList Build(IEnumerable<ParticipantApplication> applications, EventVenue? venue, string? search = null)
    {
        var members = applications
            .Where(a => a.Event is not null)
            .GroupBy(Key)
            .Select(ToMember)
            .Where(m => venue is null || m.History.Any(h => h.Venue == venue))
            .Where(m => Matches(m, search))
            .OrderByDescending(m => m.LastAppliedAt)
            .ToList();

        return new MemberList(
            members.Where(m => m.Gender == "남").ToList(),
            members.Where(m => m.Gender == "여").ToList(),
            members.Where(m => m.Gender is not ("남" or "여")).ToList());
    }

    // 다른 행사에 신청한 적이 있는 연락처들 ("기존 참가자" 체크가 사실인지 관리자가 확인하는 용도)
    public static HashSet<string> PhonesWithHistory(IEnumerable<ParticipantApplication> applications, int excludingEventId) =>
        applications
            .Where(a => a.EventId != excludingEventId)
            .Select(a => ApplicationFormValidator.NormalizePhone(a.Phone))
            .Where(p => p.Length > 0)
            .ToHashSet();

    private static string Key(ParticipantApplication app)
    {
        var phone = ApplicationFormValidator.NormalizePhone(app.Phone);
        return phone.Length > 0 ? $"p:{phone}" : $"n:{app.Name.Trim()}|{app.BirthDate}";
    }

    private static Member ToMember(IGrouping<string, ParticipantApplication> group)
    {
        var latestFirst = group.OrderByDescending(a => a.CreatedAt).ToList();
        string? Latest(Func<ParticipantApplication, string?> pick) =>
            latestFirst.Select(pick).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return new Member
        {
            Name = latestFirst[0].Name,
            Gender = Latest(a => a.Gender),
            BirthYearLabel = ParticipantApplication.ToBirthYearLabel(Latest(a => a.BirthDate)),
            Phone = Latest(a => a.Phone),
            KakaoId = Latest(a => a.KakaoId),
            LastAppliedAt = latestFirst[0].CreatedAt,
            History = latestFirst
                .Select(a => new MemberHistoryItem(VenueHelper.FromEvent(a.Event!), a.Event!.TabLabel, a.IsConfirmed, a.CreatedAt))
                .ToList()
        };
    }

    private static bool Matches(Member member, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        var term = search.Trim();
        var digits = ApplicationFormValidator.NormalizePhone(term);
        return member.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (digits.Length >= 3 && ApplicationFormValidator.NormalizePhone(member.Phone).Contains(digits))
            || (member.KakaoId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
