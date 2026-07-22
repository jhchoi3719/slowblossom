using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public class SeatMatchingService(
    IDbContextFactory<AppDbContext> dbFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<SeatMatchingService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<(List<ParticipantAiMatch> Matches, List<string> UnmatchedNames, string? Error)> GenerateMidVoteSeatingAsync(
        int eventId,
        CancellationToken cancellationToken = default) =>
        GenerateSeatingAsync(eventId, VoteType.Mid, includeMidVotes: true, cancellationToken);

    public Task<(List<ParticipantAiMatch> Matches, List<string> UnmatchedNames, string? Error)> GenerateInitialSeatingAsync(
        int eventId,
        CancellationToken cancellationToken = default) =>
        GenerateSeatingAsync(eventId, VoteType.Initial, includeMidVotes: false, cancellationToken);

    public async Task<List<ParticipantAiMatch>> GetMidVoteSeatingAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.AiMatches
            .Include(m => m.Male)
            .Include(m => m.Female)
            .Where(m => m.EventId == eventId && m.VoteType == VoteType.Mid)
            .OrderBy(m => m.MatchSource)
            .ThenBy(m => m.Male!.Name)
            .ThenBy(m => m.Female!.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ParticipantAiMatch>> GetInitialSeatingAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.AiMatches
            .Include(m => m.Male)
            .Include(m => m.Female)
            .Where(m => m.EventId == eventId && m.VoteType == VoteType.Initial)
            .OrderBy(m => m.MatchSource)
            .ThenBy(m => m.Male!.Name)
            .ThenBy(m => m.Female!.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ParticipantAiMatch>> GetFinalCoupleMatchingAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.AiMatches
            .Include(m => m.Male)
            .Include(m => m.Female)
            .Where(m => m.EventId == eventId && m.VoteType == VoteType.Final)
            .OrderBy(m => m.MatchSource)
            .ThenBy(m => m.Male!.Name)
            .ThenBy(m => m.Female!.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<ParticipantAiMatch> Matches, string? Error)> GenerateFinalCoupleMatchingAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var applications = await db.Applications
            .Where(a => a.EventId == eventId && a.IsConfirmed && (a.Gender == "남" || a.Gender == "여"))
            .ToListAsync(cancellationToken);

        var males = applications.Where(a => a.Gender == "남").ToDictionary(a => a.Id);
        var females = applications.Where(a => a.Gender == "여").ToDictionary(a => a.Id);

        if (males.Count == 0 || females.Count == 0)
            return ([], "확정된 남녀 참가자가 모두 있어야 최종 커플 매칭이 가능합니다.");

        var votes = await db.Votes
            .Where(v => v.EventId == eventId && v.VoteType == VoteType.Final)
            .ToListAsync(cancellationToken);

        var firstChoices = votes
            .Where(v => v.Priority == 1 && !v.IsExplicitNone)
            .ToDictionary(v => v.VoterApplicationId, v => v.TargetApplicationId);

        var secondChoices = votes
            .Where(v => v.Priority == 2 && !v.IsExplicitNone)
            .ToDictionary(v => v.VoterApplicationId, v => v.TargetApplicationId);

        var matched = new HashSet<int>();
        var matches = new List<ParticipantAiMatch>();

        bool TryAddCouple(int idA, int idB, MatchSource source, string reason)
        {
            if (matched.Contains(idA) || matched.Contains(idB))
                return false;

            if (!TryResolvePair(idA, idB, males, females, out var maleId, out var femaleId))
                return false;

            males.Remove(maleId);
            females.Remove(femaleId);
            matched.Add(maleId);
            matched.Add(femaleId);

            matches.Add(new ParticipantAiMatch
            {
                EventId = eventId,
                VoteType = VoteType.Final,
                MaleApplicationId = maleId,
                FemaleApplicationId = femaleId,
                MatchSource = source,
                Reason = reason
            });
            return true;
        }

        foreach (var (voterId, targetId) in firstChoices.OrderBy(p => p.Key))
        {
            if (!firstChoices.TryGetValue(targetId, out var reciprocal) || reciprocal != voterId)
                continue;

            TryAddCouple(voterId, targetId, MatchSource.MutualFirst, "1순위 상호 선택");
        }

        foreach (var (voterId, targetId) in firstChoices.OrderBy(p => p.Key))
        {
            if (secondChoices.TryGetValue(targetId, out var reciprocal) && reciprocal == voterId)
                TryAddCouple(voterId, targetId, MatchSource.FirstSecondCross, "1순위 ↔ 2순위");
        }

        foreach (var (voterId, targetId) in secondChoices.OrderBy(p => p.Key))
        {
            if (firstChoices.TryGetValue(targetId, out var reciprocal) && reciprocal == voterId)
                TryAddCouple(voterId, targetId, MatchSource.FirstSecondCross, "1순위 ↔ 2순위");
        }

        foreach (var (voterId, targetId) in secondChoices.OrderBy(p => p.Key))
        {
            if (!secondChoices.TryGetValue(targetId, out var reciprocal) || reciprocal != voterId)
                continue;

            TryAddCouple(voterId, targetId, MatchSource.MutualSecond, "2순위 상호 선택");
        }

        var existing = await db.AiMatches
            .Where(m => m.EventId == eventId && m.VoteType == VoteType.Final)
            .ToListAsync(cancellationToken);
        db.AiMatches.RemoveRange(existing);
        db.AiMatches.AddRange(matches);
        await db.SaveChangesAsync(cancellationToken);

        return (matches, null);
    }

    public static string MatchSourceLabel(MatchSource source) => source switch
    {
        MatchSource.MutualFirst => "1순위 상호",
        MatchSource.FirstSecondCross => "1순위 ↔ 2순위",
        MatchSource.MutualSecond => "2순위 상호",
        MatchSource.Mutual => "상호 선택",
        MatchSource.MaleVote => "남성 선택",
        MatchSource.FemaleVote => "여성 선택",
        MatchSource.ProfileAi => "AI 매칭",
        _ => source.ToString()
    };

    private async Task<(List<ParticipantAiMatch> Matches, List<string> UnmatchedNames, string? Error)> GenerateSeatingAsync(
        int eventId,
        VoteType seatType,
        bool includeMidVotes,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var applications = await db.Applications
            .Where(a => a.EventId == eventId && a.IsConfirmed && (a.Gender == "남" || a.Gender == "여"))
            .ToListAsync(cancellationToken);

        var evt = await db.Events.FindAsync([eventId], cancellationToken);
        var referenceDate = evt?.EventDate.Date ?? DateTime.Today;

        var males = applications.Where(a => a.Gender == "남").ToList();
        var females = applications.Where(a => a.Gender == "여").ToList();

        if (males.Count == 0 || females.Count == 0)
        {
            return ([], applications.Select(a => a.Name).OrderBy(n => n).ToList(),
                "확정된 남녀 참가자가 모두 있어야 좌석 배치가 가능합니다.");
        }

        var votes = includeMidVotes
            ? await db.Votes
                .Where(v => v.EventId == eventId && v.VoteType == VoteType.Mid)
                .ToListAsync(cancellationToken)
            : [];

        var voteByVoter = votes
            .GroupBy(v => v.VoterApplicationId)
            .ToDictionary(g => g.Key, g => g.OrderBy(v => v.Priority).First().TargetApplicationId);
        var nameById = applications.ToDictionary(a => a.Id, a => a.Name);

        var availableMales = males.ToDictionary(m => m.Id);
        var availableFemales = females.ToDictionary(f => f.Id);
        var matches = new List<ParticipantAiMatch>();

        if (includeMidVotes)
        {
            AddMutualPairs(votes, availableMales, availableFemales, matches, eventId, seatType);
            AddMaleVotePairs(voteByVoter, availableMales, availableFemales, matches, eventId, seatType, nameById);
            AddFemaleVotePairs(voteByVoter, availableMales, availableFemales, matches, eventId, seatType, nameById);
        }

        var remainingMales = availableMales.Values.OrderBy(m => m.Name).ToList();
        var remainingFemales = availableFemales.Values.OrderBy(f => f.Name).ToList();

        if (remainingMales.Count > 0 && remainingFemales.Count > 0)
        {
            var profiles = applications
                .Where(a => availableMales.ContainsKey(a.Id) || availableFemales.ContainsKey(a.Id))
                .Select(a =>
                {
                    var votedForId = includeMidVotes ? voteByVoter.GetValueOrDefault(a.Id) : 0;
                    return ParticipantMatchProfile.FromApplication(
                        a,
                        referenceDate,
                        votedForId,
                        votedForId > 0 ? nameById.GetValueOrDefault(votedForId) : null);
                })
                .ToList();

            var seatingContext = includeMidVotes ? "중간투표 기반 좌석 배치" : "행사 시작 전 초기 좌석 배치";
            var algorithmPairs = BuildFallbackPairs(remainingMales, remainingFemales, voteByVoter, referenceDate);
            var aiPairs = await RequestGeminiPairsAsync(
                remainingMales,
                remainingFemales,
                profiles,
                referenceDate,
                seatingContext,
                cancellationToken);

            var chosenPairs = ChooseBestPairs(
                algorithmPairs,
                aiPairs,
                remainingMales,
                remainingFemales,
                referenceDate,
                voteByVoter);

            var appById = applications.ToDictionary(a => a.Id);

            AddProfileAiPairs(
                matches,
                eventId,
                seatType,
                availableMales,
                availableFemales,
                appById,
                voteByVoter,
                nameById,
                referenceDate,
                chosenPairs.Select(p => (p.MaleId, p.FemaleId)));
        }

        // AI/알고리즘 이후 남은 참가자도 호환성 점수 기준으로 매칭
        if (availableMales.Count > 0 && availableFemales.Count > 0)
        {
            AddProfileAiPairs(
                matches,
                eventId,
                seatType,
                availableMales,
                availableFemales,
                applications.ToDictionary(a => a.Id),
                voteByVoter,
                nameById,
                referenceDate,
                MatchCompatibilityHelper.PairByCompatibility(
                    availableMales.Values.ToList(),
                    availableFemales.Values.ToList(),
                    referenceDate,
                    voteByVoter));
        }

        var existing = await db.AiMatches
            .Where(m => m.EventId == eventId && m.VoteType == seatType)
            .ToListAsync(cancellationToken);
        db.AiMatches.RemoveRange(existing);
        db.AiMatches.AddRange(matches);
        await db.SaveChangesAsync(cancellationToken);

        var matchedIds = matches
            .SelectMany(m => new[] { m.MaleApplicationId, m.FemaleApplicationId })
            .ToHashSet();

        var unmatchedNames = applications
            .Where(a => !matchedIds.Contains(a.Id))
            .Select(a => a.Name)
            .OrderBy(n => n)
            .ToList();

        return (matches, unmatchedNames, null);
    }

    public static HashSet<int> GetMutualParticipantIds(IReadOnlyList<ParticipantVote> votes)
    {
        var voterToTargets = votes
            .GroupBy(v => v.VoterApplicationId)
            .ToDictionary(g => g.Key, g => g.Select(v => v.TargetApplicationId).ToHashSet());

        var participantIds = new HashSet<int>();
        var seen = new HashSet<long>();

        foreach (var vote in votes)
        {
            var voterId = vote.VoterApplicationId;
            var targetId = vote.TargetApplicationId;

            if (!voterToTargets.TryGetValue(targetId, out var reciprocal) || !reciprocal.Contains(voterId))
                continue;

            var pairKey = ((long)Math.Min(voterId, targetId) << 32) | (uint)Math.Max(voterId, targetId);
            if (!seen.Add(pairKey))
                continue;

            participantIds.Add(voterId);
            participantIds.Add(targetId);
        }

        return participantIds;
    }

    private static void AddMutualPairs(
        List<ParticipantVote> votes,
        Dictionary<int, ParticipantApplication> availableMales,
        Dictionary<int, ParticipantApplication> availableFemales,
        List<ParticipantAiMatch> matches,
        int eventId,
        VoteType seatType)
    {
        var voterToTargets = votes
            .GroupBy(v => v.VoterApplicationId)
            .ToDictionary(g => g.Key, g => g.Select(v => v.TargetApplicationId).ToHashSet());

        var seen = new HashSet<long>();

        foreach (var vote in votes)
        {
            var voterId = vote.VoterApplicationId;
            var targetId = vote.TargetApplicationId;

            if (!voterToTargets.TryGetValue(targetId, out var reciprocal) || !reciprocal.Contains(voterId))
                continue;

            var pairKey = ((long)Math.Min(voterId, targetId) << 32) | (uint)Math.Max(voterId, targetId);
            if (!seen.Add(pairKey))
                continue;

            if (!TryResolvePair(voterId, targetId, availableMales, availableFemales, out var maleId, out var femaleId))
                continue;

            availableMales.Remove(maleId);
            availableFemales.Remove(femaleId);

            matches.Add(new ParticipantAiMatch
            {
                EventId = eventId,
                VoteType = seatType,
                MaleApplicationId = maleId,
                FemaleApplicationId = femaleId,
                MatchSource = MatchSource.Mutual,
                Reason = null
            });
        }
    }

    private static void AddMaleVotePairs(
        Dictionary<int, int> voteByVoter,
        Dictionary<int, ParticipantApplication> availableMales,
        Dictionary<int, ParticipantApplication> availableFemales,
        List<ParticipantAiMatch> matches,
        int eventId,
        VoteType seatType,
        Dictionary<int, string> nameById)
    {
        foreach (var male in availableMales.Values.OrderBy(m => m.Name).ToList())
        {
            if (!voteByVoter.TryGetValue(male.Id, out var targetId))
                continue;

            if (!availableFemales.ContainsKey(targetId))
                continue;

            var female = availableFemales[targetId];
            availableMales.Remove(male.Id);
            availableFemales.Remove(female.Id);

            matches.Add(new ParticipantAiMatch
            {
                EventId = eventId,
                VoteType = seatType,
                MaleApplicationId = male.Id,
                FemaleApplicationId = female.Id,
                MatchSource = MatchSource.MaleVote,
                Reason = $"{male.Name}(남)님이 {female.Name}(여)님을 선택하여 매칭했습니다."
            });
        }
    }

    private static void AddFemaleVotePairs(
        Dictionary<int, int> voteByVoter,
        Dictionary<int, ParticipantApplication> availableMales,
        Dictionary<int, ParticipantApplication> availableFemales,
        List<ParticipantAiMatch> matches,
        int eventId,
        VoteType seatType,
        Dictionary<int, string> nameById)
    {
        foreach (var female in availableFemales.Values.OrderBy(f => f.Name).ToList())
        {
            if (!voteByVoter.TryGetValue(female.Id, out var targetId))
                continue;

            if (!availableMales.ContainsKey(targetId))
                continue;

            var male = availableMales[targetId];
            availableMales.Remove(male.Id);
            availableFemales.Remove(female.Id);

            matches.Add(new ParticipantAiMatch
            {
                EventId = eventId,
                VoteType = seatType,
                MaleApplicationId = male.Id,
                FemaleApplicationId = female.Id,
                MatchSource = MatchSource.FemaleVote,
                Reason = $"{female.Name}(여)님이 {male.Name}(남)님을 선택하여 매칭했습니다."
            });
        }
    }

    private static bool TryResolvePair(
        int idA,
        int idB,
        Dictionary<int, ParticipantApplication> availableMales,
        Dictionary<int, ParticipantApplication> availableFemales,
        out int maleId,
        out int femaleId)
    {
        maleId = 0;
        femaleId = 0;

        if (availableMales.ContainsKey(idA) && availableFemales.ContainsKey(idB))
        {
            maleId = idA;
            femaleId = idB;
            return true;
        }

        if (availableMales.ContainsKey(idB) && availableFemales.ContainsKey(idA))
        {
            maleId = idB;
            femaleId = idA;
            return true;
        }

        return false;
    }

    private async Task<List<AiPairResult>?> RequestGeminiPairsAsync(
        List<ParticipantApplication> males,
        List<ParticipantApplication> females,
        List<ParticipantMatchProfile> profiles,
        DateTime referenceDate,
        string seatingContext,
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var model = configuration["Gemini:Model"] ?? "gemini-2.0-flash";
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var pairCount = Math.Min(males.Count, females.Count);
        var eventDateLabel = referenceDate.ToString("yyyy-MM-dd");

        var profileJson = JsonSerializer.Serialize(profiles, JsonOptions);
        var malesJson = JsonSerializer.Serialize(males.Select(m => new { m.Id, m.Name }), JsonOptions);
        var femalesJson = JsonSerializer.Serialize(females.Select(f => new { f.Id, f.Name }), JsonOptions);

        var prompt = $"""
            로테이션 소개팅 행사의 {seatingContext}입니다. 행사 기준일은 {eventDateLabel}입니다.
            남녀 1:1로 매칭하세요. participants JSON의 ageAtEvent(만 나이)와 preferredAgeRange를 반드시 확인하세요.

            규칙:
            - 남성 id는 males 목록, 여성 id는 females 목록에 있는 값만 사용
            - 한 사람은 최대 한 번만 매칭
            - 정확히 {pairCount}쌍 반환
            - 선호 연령대가 있는 경우, 상대방 ageAtEvent가 그 범위에 들어가는 조합을 우선
            - 선호 연령대와 맞지 않는 사람끼리는 가급적 매칭하지 말 것
            - reason 필드는 빈 문자열 "" 로 두어도 됨 (서버에서 다시 작성함)
            - JSON만 반환하고 pairs 배열에 maleId, femaleId, reason 필드를 포함

            males: {malesJson}
            females: {femalesJson}
            participants: {profileJson}
            """;

        try
        {
            var client = httpClientFactory.CreateClient(nameof(SeatMatchingService));
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    responseMimeType = "application/json"
                }
            }, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini API failed: {Status} {Body}", response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            content = ExtractJson(content);
            var parsed = JsonSerializer.Deserialize<AiPairResponse>(content, JsonOptions);
            return parsed?.Pairs?.Where(p => p.MaleId > 0 && p.FemaleId > 0).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gemini matching request failed");
            return null;
        }
    }

    private static void AddProfileAiPairs(
        List<ParticipantAiMatch> matches,
        int eventId,
        VoteType seatType,
        Dictionary<int, ParticipantApplication> availableMales,
        Dictionary<int, ParticipantApplication> availableFemales,
        Dictionary<int, ParticipantApplication> appById,
        Dictionary<int, int> voteByVoter,
        Dictionary<int, string> nameById,
        DateTime referenceDate,
        IEnumerable<(int MaleId, int FemaleId)> pairs)
    {
        foreach (var (maleId, femaleId) in pairs)
        {
            if (!availableMales.Remove(maleId) || !availableFemales.Remove(femaleId))
                continue;

            if (!appById.TryGetValue(maleId, out var maleApp)
                || !appById.TryGetValue(femaleId, out var femaleApp))
                continue;

            var maleVoteTarget = voteByVoter.GetValueOrDefault(maleId);
            var femaleVoteTarget = voteByVoter.GetValueOrDefault(femaleId);

            matches.Add(new ParticipantAiMatch
            {
                EventId = eventId,
                VoteType = seatType,
                MaleApplicationId = maleId,
                FemaleApplicationId = femaleId,
                MatchSource = MatchSource.ProfileAi,
                Reason = MatchCompatibilityHelper.BuildMatchReason(
                    maleApp,
                    femaleApp,
                    referenceDate,
                    maleVoteTarget,
                    femaleVoteTarget,
                    maleVoteTarget > 0 ? nameById.GetValueOrDefault(maleVoteTarget) : null,
                    femaleVoteTarget > 0 ? nameById.GetValueOrDefault(femaleVoteTarget) : null)
            });
        }
    }

    private static List<AiPairResult> BuildFallbackPairs(
        List<ParticipantApplication> males,
        List<ParticipantApplication> females,
        Dictionary<int, int> voteByVoter,
        DateTime referenceDate) =>
        MatchCompatibilityHelper.PairByCompatibility(males, females, referenceDate, voteByVoter)
            .Select(pair => new AiPairResult
            {
                MaleId = pair.MaleId,
                FemaleId = pair.FemaleId
            })
            .ToList();

    private static List<AiPairResult> ChooseBestPairs(
        List<AiPairResult> algorithmPairs,
        List<AiPairResult>? aiPairs,
        List<ParticipantApplication> males,
        List<ParticipantApplication> females,
        DateTime referenceDate,
        Dictionary<int, int> voteByVoter)
    {
        var expectedCount = Math.Min(males.Count, females.Count);
        if (aiPairs is null || !IsValidPairSet(aiPairs, males, females, expectedCount))
            return algorithmPairs;

        var maleById = males.ToDictionary(m => m.Id);
        var femaleById = females.ToDictionary(f => f.Id);

        var algorithmScore = SumPairScores(algorithmPairs, maleById, femaleById, referenceDate, voteByVoter);
        var aiScore = SumPairScores(aiPairs, maleById, femaleById, referenceDate, voteByVoter);

        return aiScore > algorithmScore ? aiPairs : algorithmPairs;
    }

    private static bool IsValidPairSet(
        List<AiPairResult> pairs,
        List<ParticipantApplication> males,
        List<ParticipantApplication> females,
        int expectedCount)
    {
        if (pairs.Count != expectedCount)
            return false;

        var maleIds = males.Select(m => m.Id).ToHashSet();
        var femaleIds = females.Select(f => f.Id).ToHashSet();
        var usedMales = new HashSet<int>();
        var usedFemales = new HashSet<int>();

        foreach (var pair in pairs)
        {
            if (!maleIds.Contains(pair.MaleId) || !femaleIds.Contains(pair.FemaleId))
                return false;
            if (!usedMales.Add(pair.MaleId) || !usedFemales.Add(pair.FemaleId))
                return false;
        }

        return true;
    }

    private static int SumPairScores(
        List<AiPairResult> pairs,
        Dictionary<int, ParticipantApplication> maleById,
        Dictionary<int, ParticipantApplication> femaleById,
        DateTime referenceDate,
        Dictionary<int, int> voteByVoter)
    {
        var total = 0;
        foreach (var pair in pairs)
        {
            if (!maleById.TryGetValue(pair.MaleId, out var male)
                || !femaleById.TryGetValue(pair.FemaleId, out var female))
                continue;

            total += MatchCompatibilityHelper.ComputeCompatibilityScore(
                male,
                female,
                referenceDate,
                voteByVoter.GetValueOrDefault(pair.MaleId),
                voteByVoter.GetValueOrDefault(pair.FemaleId));
        }

        return total;
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var start = trimmed.IndexOf('\n') + 1;
            var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (end > start)
                trimmed = trimmed[start..end].Trim();
        }

        return trimmed;
    }

    public static string GetMatchSourceLabel(MatchSource source) => source switch
    {
        MatchSource.Mutual => "서로 선택",
        MatchSource.MaleVote => "남자 선택 반영",
        MatchSource.FemaleVote => "여자 선택 반영",
        MatchSource.ProfileAi => "신상정보 AI 매칭",
        _ => ""
    };

    private sealed class AiPairResponse
    {
        public List<AiPairResult>? Pairs { get; set; }
    }

    private sealed class AiPairResult
    {
        public int MaleId { get; set; }
        public int FemaleId { get; set; }
        public string Reason { get; set; } = "";
    }

    private sealed class ParticipantMatchProfile
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Gender { get; set; } = "";
        public string? BirthDate { get; set; }
        public string? Residence { get; set; }
        public string? Workplace { get; set; }
        public string? PreferredAgeRange { get; set; }
        public string? Interests { get; set; }
        public string? Drinking { get; set; }
        public string? Smoking { get; set; }
        public string? AllowContact { get; set; }
        public int? VotedForId { get; set; }
        public string? VotedForName { get; set; }
        public int? AgeAtEvent { get; set; }

        public static ParticipantMatchProfile FromApplication(
            ParticipantApplication app,
            DateTime referenceDate,
            int votedForId = 0,
            string? votedForName = null)
        {
            int? ageAtEvent = null;
            if (MatchCompatibilityHelper.TryGetAgeAt(app.BirthDate, referenceDate, out var age))
                ageAtEvent = age;

            return new ParticipantMatchProfile
            {
                Id = app.Id,
                Name = app.Name,
                Gender = app.Gender ?? "",
                BirthDate = app.BirthDate,
                AgeAtEvent = ageAtEvent,
                Residence = app.Residence,
                Workplace = app.Workplace,
                PreferredAgeRange = app.PreferredAgeRange,
                Interests = app.Interests,
                Drinking = ParticipantApplication.OxLabel(app.Drinking),
                Smoking = ParticipantApplication.OxLabel(app.Smoking),
                AllowContact = ParticipantApplication.OxLabel(app.AllowContact),
                VotedForId = votedForId > 0 ? votedForId : null,
                VotedForName = votedForName
            };
        }
    }
}
