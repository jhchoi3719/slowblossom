using System.Globalization;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services.ApplicationForm;

public static class ApplicationFormValidator
{
    public static List<string> Validate(ApplicationFormInput input, Event evt)
    {
        var errors = new List<string>();

        void Require(string? value, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add(message);
        }

        Require(input.Name, "이름을 입력해 주세요.");
        // 성별은 기존 참가자도 받는다: 날짜별 남녀 인원(5:5)을 세야 한다.
        if (input.Gender is not ("남" or "여"))
            errors.Add("성별을 선택해 주세요.");
        if (NormalizePhone(input.Phone).Length is not (10 or 11))
            errors.Add("연락처를 숫자로 정확히 입력해 주세요.");

        // 기존 참가자는 이름·연락처(+후보일, 개인정보 동의)만 받는다. 나머지는 이전 신청서에 있다.
        if (!input.IsReturning)
            ValidateNewApplicant(input, errors, Require);

        if (evt.Kind == EventKind.DatePoll)
        {
            var candidates = evt.CandidateDates.Select(c => c.EventDate.Date).ToHashSet();
            if (input.AvailableDates.Count == 0)
                errors.Add("참여 가능한 날짜를 하나 이상 선택해 주세요.");
            else if (input.AvailableDates.Any(d => !candidates.Contains(d.Date)))
                errors.Add("선택할 수 없는 날짜가 포함되어 있어요.");
        }

        if (input.IsReturning)
        {
            if (!input.AgreePrivacy)
                errors.Add("개인정보 수집 및 이용에 동의해 주세요.");
        }
        else if (!(input.AgreeSelection && input.AgreeMarriage && input.AgreePhoto && input.AgreeRefund && input.AgreePrivacy))
        {
            errors.Add("안내 및 동의 항목(18~22번)에 모두 동의해 주세요.");
        }

        // 화면의 maxlength와 같은 한도. 스크립트로 보낸 긴 글이 DB와 엑셀을 망가뜨리지 않게 막는다.
        foreach (var (value, max, label) in new[]
        {
            (input.Name, 50, "이름"),
            (input.BirthDate, 10, "생년월일"),
            (input.Phone, 13, "연락처"),
            (input.KakaoId, 50, "카카오톡 ID"),
            (input.Residence, 100, "거주지"),
            (input.Education, 100, "최종학력"),
            (input.Workplace, 200, "직장"),
            (input.Motivation, 2000, "신청동기"),
            (input.SelfIntro, 2000, "자기소개")
        })
        {
            if ((value?.Length ?? 0) > max)
                errors.Add($"{label}이(가) 너무 길어요. {max}자 이내로 입력해 주세요.");
        }

        return errors;
    }

    private static void ValidateNewApplicant(ApplicationFormInput input, List<string> errors, Action<string?, string> Require)
    {
        if (NormalizeBirthDate(input.BirthDate) is null)
            errors.Add("생년월일 8자리를 입력해 주세요.");
        Require(input.Residence, "거주지를 입력해 주세요.");
        Require(input.KakaoId, "카카오톡 ID를 입력해 주세요.");
        Require(input.Education, "최종학력을 입력해 주세요.");
        Require(input.Workplace, "직장명, 부서, 직위를 입력해 주세요.");
        if (!ApplicationFormOptions.AgeRanges.Contains(input.PreferredAgeRange))
            errors.Add("원하는 상대 연령대를 선택해 주세요.");

        var interests = input.Interests.Where(ApplicationFormOptions.Interests.Contains).Distinct().Count();
        if (interests == 0 || interests != input.Interests.Count)
            errors.Add("관심사를 선택해 주세요.");
        else if (interests > ApplicationFormOptions.MaxInterests)
            errors.Add("관심사는 3개까지 선택할 수 있어요.");

        if (!ApplicationFormOptions.ReferralSources.Contains(input.ReferralSource))
            errors.Add("참여경로를 선택해 주세요.");
        Require(input.Motivation, "신청동기를 입력해 주세요.");
        if (input.Drinking is null)
            errors.Add("음주 여부를 선택해 주세요.");
        if (input.Smoking is null)
            errors.Add("흡연 여부를 선택해 주세요.");
        Require(input.SelfIntro, "자기소개를 입력해 주세요.");

        if (!input.HasPhoto)
            errors.Add("본인 사진을 첨부해 주세요.");
        if (!input.HasEmploymentProof)
            errors.Add("직장인/사업자/학생 증빙을 첨부해 주세요.");
        if (!input.HasMarriageCert)
            errors.Add("혼인관계증명서를 첨부해 주세요.");
    }

    public static string? NormalizeBirthDate(string? raw)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 8)
            return null;

        return DateTime.TryParseExact(digits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && date.Year >= 1900 && date <= DateTime.Today
            ? digits
            : null;
    }

    public static string NormalizePhone(string? raw) =>
        new((raw ?? "").Where(char.IsDigit).ToArray());
}
