using System.Text.RegularExpressions;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public static partial class MatchCompatibilityHelper
{
    public static bool TryGetAgeAt(string? birthDate, DateTime referenceDate, out int age)
    {
        age = 0;
        if (!TryParseBirthDate(birthDate, out var birth))
            return false;

        age = referenceDate.Year - birth.Year;
        if (referenceDate.Month < birth.Month
            || (referenceDate.Month == birth.Month && referenceDate.Day < birth.Day))
            age--;

        return age >= 0;
    }

    public static bool TryParsePreferredAgeRange(string? range, out int minAge, out int maxAge)
    {
        minAge = 0;
        maxAge = 0;
        if (string.IsNullOrWhiteSpace(range))
            return false;

        var numbers = PreferredAgeNumberRegex().Matches(range)
            .Select(m => int.Parse(m.Value))
            .Where(n => n is >= 18 and <= 80)
            .ToList();

        if (numbers.Count == 0)
            return false;

        if (numbers.Count == 1)
        {
            minAge = maxAge = numbers[0];
            return true;
        }

        minAge = Math.Min(numbers[0], numbers[1]);
        maxAge = Math.Max(numbers[0], numbers[1]);
        return true;
    }

    public static bool IsAgeWithinPreferredRange(string? birthDate, string? preferredRange, DateTime referenceDate)
    {
        if (string.IsNullOrWhiteSpace(preferredRange))
            return true;

        if (!TryGetAgeAt(birthDate, referenceDate, out var age))
            return false;

        if (!TryParsePreferredAgeRange(preferredRange, out var minAge, out var maxAge))
            return false;

        return age >= minAge && age <= maxAge;
    }

    public static int ComputeCompatibilityScore(
        ParticipantApplication? male,
        ParticipantApplication? female,
        DateTime referenceDate,
        int maleVotedForId = 0,
        int femaleVotedForId = 0)
    {
        if (male is null || female is null)
            return int.MinValue / 4;

        var score = 0;

        if (maleVotedForId == female.Id || femaleVotedForId == male.Id)
            score += 30;

        score += TextOverlapScore(male.Interests, female.Interests) * 4;
        score += TextOverlapScore(male.Residence, female.Residence) * 3;
        score += TextOverlapScore(male.Workplace, female.Workplace) * 2;

        if (IsAgeWithinPreferredRange(female.BirthDate, male.PreferredAgeRange, referenceDate))
            score += 25;
        else if (!string.IsNullOrWhiteSpace(male.PreferredAgeRange) && TryGetAgeAt(female.BirthDate, referenceDate, out _))
            score -= 40;

        if (IsAgeWithinPreferredRange(male.BirthDate, female.PreferredAgeRange, referenceDate))
            score += 25;
        else if (!string.IsNullOrWhiteSpace(female.PreferredAgeRange) && TryGetAgeAt(male.BirthDate, referenceDate, out _))
            score -= 40;

        if (male.Drinking == female.Drinking && male.Drinking is not null)
            score += 2;
        if (male.Smoking == female.Smoking && male.Smoking is not null)
            score += 2;
        if (male.AllowContact == true && female.AllowContact == true)
            score += 3;

        return score;
    }

    public static string BuildMatchReason(
        ParticipantApplication male,
        ParticipantApplication female,
        DateTime referenceDate,
        int maleVotedForId = 0,
        int femaleVotedForId = 0,
        string? maleVotedForName = null,
        string? femaleVotedForName = null)
    {
        var reasons = new List<string>();

        if (maleVotedForId == female.Id)
            reasons.Add($"{male.Name}님이 중간투표에서 {female.Name}님을 선택했으나 상대방 선택과 맞지 않았습니다");
        else if (femaleVotedForId == male.Id)
            reasons.Add($"{female.Name}님이 중간투표에서 {male.Name}님을 선택했으나 상대방 선택과 맞지 않았습니다");
        else if (!string.IsNullOrWhiteSpace(maleVotedForName) || !string.IsNullOrWhiteSpace(femaleVotedForName))
        {
            var voteNote = new List<string>();
            if (!string.IsNullOrWhiteSpace(maleVotedForName))
                voteNote.Add($"{male.Name}님은 {maleVotedForName}님을 선택");
            if (!string.IsNullOrWhiteSpace(femaleVotedForName))
                voteNote.Add($"{female.Name}님은 {femaleVotedForName}님을 선택");
            reasons.Add(string.Join(", ", voteNote) + "했으나 서로 맞지 않아 신상정보를 우선 반영했습니다");
        }

        var commonInterests = GetOverlappingTokens(male.Interests, female.Interests);
        if (commonInterests.Count > 0)
            reasons.Add($"관심사가 '{string.Join(", ", commonInterests)}'(으)로 겹칩니다");

        if (HasSignificantOverlap(male.Residence, female.Residence))
            reasons.Add($"거주지가 '{FormatPairValue(male.Residence, female.Residence)}'(으)로 가깝습니다");

        if (HasSignificantOverlap(male.Workplace, female.Workplace))
            reasons.Add($"직장/업종이 '{FormatPairValue(male.Workplace, female.Workplace)}'(으)로 유사합니다");

        AppendAgeReason(reasons, male.Name, male.PreferredAgeRange, female.Name, female.BirthDate, referenceDate);
        AppendAgeReason(reasons, female.Name, female.PreferredAgeRange, male.Name, male.BirthDate, referenceDate);

        var maleDrink = ParticipantApplication.OxLabel(male.Drinking);
        var femaleDrink = ParticipantApplication.OxLabel(female.Drinking);
        if (maleDrink == femaleDrink && maleDrink is "O" or "X")
            reasons.Add($"음주 성향이 둘 다 {maleDrink}입니다");

        var maleSmoke = ParticipantApplication.OxLabel(male.Smoking);
        var femaleSmoke = ParticipantApplication.OxLabel(female.Smoking);
        if (maleSmoke == femaleSmoke && maleSmoke is "O" or "X")
            reasons.Add($"흡연 성향이 둘 다 {maleSmoke}입니다");

        if (male.AllowContact == true && female.AllowContact == true)
            reasons.Add("둘 다 연락 허용 의사가 있습니다");

        AppendSoftComparisonReasons(reasons, male, female, referenceDate, includeRemainderNote: reasons.Count == 0);

        if (reasons.Count == 0)
        {
            return $"{male.Name}님과 {female.Name}님의 신상정보(관심사, 거주지, 직장, 연령대, 음주, 흡연)를 비교해 남은 참가자 중 상대적으로 가장 적합한 조합으로 배치했습니다.";
        }

        return string.Join(". ", reasons) + ".";
    }

    public static List<(int MaleId, int FemaleId)> PairByCompatibility(
        IReadOnlyList<ParticipantApplication> males,
        IReadOnlyList<ParticipantApplication> females,
        DateTime referenceDate,
        IReadOnlyDictionary<int, int>? voteByVoter = null)
    {
        voteByVoter ??= new Dictionary<int, int>();
        var scores = new List<(int MaleId, int FemaleId, int Score)>();

        foreach (var male in males)
        {
            foreach (var female in females)
            {
                scores.Add((male.Id, female.Id, ComputeCompatibilityScore(
                    male,
                    female,
                    referenceDate,
                    voteByVoter.GetValueOrDefault(male.Id),
                    voteByVoter.GetValueOrDefault(female.Id))));
            }
        }

        var usedMales = new HashSet<int>();
        var usedFemales = new HashSet<int>();
        var pairs = new List<(int MaleId, int FemaleId)>();

        foreach (var (maleId, femaleId, _) in scores.OrderByDescending(s => s.Score))
        {
            if (!usedMales.Add(maleId) || !usedFemales.Add(femaleId))
                continue;

            pairs.Add((maleId, femaleId));

            if (pairs.Count >= Math.Min(males.Count, females.Count))
                break;
        }

        return pairs;
    }

    private static void AppendSoftComparisonReasons(
        List<string> reasons,
        ParticipantApplication male,
        ParticipantApplication female,
        DateTime referenceDate,
        bool includeRemainderNote)
    {
        var startCount = reasons.Count;

        var commonInterests = GetOverlappingTokens(male.Interests, female.Interests);
        if (commonInterests.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(male.Interests) && !string.IsNullOrWhiteSpace(female.Interests))
                reasons.Add($"관심사를 고려했습니다({male.Name}님: {male.Interests.Trim()} · {female.Name}님: {female.Interests.Trim()})");
            else if (!string.IsNullOrWhiteSpace(male.Interests))
                reasons.Add($"{male.Name}님의 관심사는 {male.Interests.Trim()}입니다");
            else if (!string.IsNullOrWhiteSpace(female.Interests))
                reasons.Add($"{female.Name}님의 관심사는 {female.Interests.Trim()}입니다");
        }

        if (!HasSignificantOverlap(male.Residence, female.Residence))
        {
            if (!string.IsNullOrWhiteSpace(male.Residence) && !string.IsNullOrWhiteSpace(female.Residence))
            {
                if (HasSameRegion(male.Residence, female.Residence))
                    reasons.Add($"거주지가 '{FormatPairValue(male.Residence, female.Residence)}'(으)로 같은 지역입니다");
                else
                    reasons.Add($"거주지를 고려했습니다({male.Residence.Trim()} · {female.Residence.Trim()})");
            }
        }

        if (!HasSignificantOverlap(male.Workplace, female.Workplace)
            && (!string.IsNullOrWhiteSpace(male.Workplace) || !string.IsNullOrWhiteSpace(female.Workplace)))
        {
            reasons.Add($"직장 정보를 고려했습니다({ValueOrDash(male.Workplace)} · {ValueOrDash(female.Workplace)})");
        }

        AppendAgeProximityReason(reasons, male.Name, male.PreferredAgeRange, female.Name, female.BirthDate, referenceDate);
        AppendAgeProximityReason(reasons, female.Name, female.PreferredAgeRange, male.Name, male.BirthDate, referenceDate);

        var maleDrink = ParticipantApplication.OxLabel(male.Drinking);
        var femaleDrink = ParticipantApplication.OxLabel(female.Drinking);
        if (maleDrink != femaleDrink && maleDrink is "O" or "X" && femaleDrink is "O" or "X")
            reasons.Add($"음주 성향({male.Name}님 {maleDrink} · {female.Name}님 {femaleDrink})을 확인했습니다");

        var maleSmoke = ParticipantApplication.OxLabel(male.Smoking);
        var femaleSmoke = ParticipantApplication.OxLabel(female.Smoking);
        if (maleSmoke != femaleSmoke && maleSmoke is "O" or "X" && femaleSmoke is "O" or "X")
            reasons.Add($"흡연 성향({male.Name}님 {maleSmoke} · {female.Name}님 {femaleSmoke})을 확인했습니다");

        if (includeRemainderNote && reasons.Count > startCount)
            reasons.Add("남은 참가자 중 위 조건을 종합해 상대적으로 가장 적합한 조합입니다");
    }

    private static bool AppendAgeProximityReason(
        List<string> reasons,
        string preferrerName,
        string? preferredRange,
        string partnerName,
        string? partnerBirthDate,
        DateTime referenceDate)
    {
        if (string.IsNullOrWhiteSpace(preferredRange) || string.IsNullOrWhiteSpace(partnerBirthDate))
            return false;

        if (!TryGetAgeAt(partnerBirthDate, referenceDate, out var partnerAge))
            return false;

        if (!TryParsePreferredAgeRange(preferredRange, out var minAge, out var maxAge))
            return false;

        if (partnerAge >= minAge && partnerAge <= maxAge)
            return false;

        var gap = partnerAge < minAge ? minAge - partnerAge : partnerAge - maxAge;
        if (gap <= 3)
        {
            reasons.Add(
                $"{preferrerName}님의 선호 연령대({preferredRange})와 {partnerName}님(만 {partnerAge}세)은 다소 차이가 있으나, 남은 참가자 중 가장 가깝습니다");
            return true;
        }

        return false;
    }

    private static bool HasSameRegion(string left, string right)
    {
        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        var regionMarkers = new[] { "대구", "서울", "부산", "인천", "광주", "대전", "울산", "세종", "경기", "강원", "충북", "충남", "전북", "전남", "경북", "경남", "제주" };

        foreach (var marker in regionMarkers)
        {
            if (left.Contains(marker, StringComparison.Ordinal) && right.Contains(marker, StringComparison.Ordinal))
                return true;
        }

        return leftTokens.Intersect(rightTokens).Any(token => token.Length >= 2);
    }

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static void AppendAgeReason(
        List<string> reasons,
        string preferrerName,
        string? preferredRange,
        string partnerName,
        string? partnerBirthDate,
        DateTime referenceDate)
    {
        if (string.IsNullOrWhiteSpace(preferredRange) || string.IsNullOrWhiteSpace(partnerBirthDate))
            return;

        if (!TryGetAgeAt(partnerBirthDate, referenceDate, out var partnerAge))
            return;

        if (!TryParsePreferredAgeRange(preferredRange, out var minAge, out var maxAge))
            return;

        if (partnerAge >= minAge && partnerAge <= maxAge)
        {
            reasons.Add(
                $"{preferrerName}님의 선호 연령대({preferredRange})에 {partnerName}님(만 {partnerAge}세, 생년월일 {partnerBirthDate})이 해당됩니다");
        }
    }

    public static bool TryParseBirthDate(string? birthDate, out DateOnly birth)
    {
        birth = default;
        if (string.IsNullOrWhiteSpace(birthDate))
            return false;

        var digits = new string(birthDate.Where(char.IsDigit).ToArray());
        if (digits.Length >= 8
            && int.TryParse(digits[..4], out var fullYear)
            && int.TryParse(digits.Substring(4, 2), out var fullMonth)
            && int.TryParse(digits.Substring(6, 2), out var fullDay)
            && fullYear is >= 1900 and <= 2099
            && fullMonth is >= 1 and <= 12
            && fullDay is >= 1 and <= 31)
        {
            try
            {
                birth = new DateOnly(fullYear, fullMonth, fullDay);
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (digits.Length >= 6
            && int.TryParse(digits[..2], out var yy)
            && int.TryParse(digits.Substring(2, 2), out var mm)
            && int.TryParse(digits.Substring(4, 2), out var dd))
        {
            var year = yy <= 30 ? 2000 + yy : 1900 + yy;
            if (mm is < 1 or > 12 || dd is < 1 or > 31)
                return false;

            try
            {
                birth = new DateOnly(year, mm, dd);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static List<string> GetOverlappingTokens(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return [];

        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        return leftTokens.Intersect(rightTokens).OrderBy(t => t).ToList();
    }

    private static bool HasSignificantOverlap(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        if (left.Trim().Equals(right.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        return GetOverlappingTokens(left, right).Count > 0;
    }

    private static string FormatPairValue(string? left, string? right) =>
        left?.Trim().Equals(right?.Trim(), StringComparison.OrdinalIgnoreCase) == true
            ? left!.Trim()
            : $"{left?.Trim()} · {right?.Trim()}";

    private static int TextOverlapScore(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return 0;

        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        return leftTokens.Count(t => rightTokens.Contains(t));
    }

    private static HashSet<string> Tokenize(string text) =>
        text.Split([' ', ',', '·', '/', '|', '，', '、', '~', '-', '～'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length >= 2)
            .ToHashSet();

    [GeneratedRegex(@"\d{1,2}")]
    private static partial Regex PreferredAgeNumberRegex();
}
