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
            .Where(v => v.Priority == 1)
            .ToDictionary(v => v.VoterApplicationId, v => v.TargetApplicationId);

        var secondChoices = votes
            .Where(v => v.Priority == 2)
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
                        votedForId,
                        votedForId > 0 ? nameById.GetValueOrDefault(votedForId) : null);
                })
                .ToList();

            var seatingContext = includeMidVotes ? "중간투표 기반 좌석 배치" : "행사 시작 전 초기 좌석 배치";
            var aiPairs = await RequestGeminiPairsAsync(remainingMales, remainingFemales, profiles, seatingContext, cancellationToken)
                ?? BuildFallbackPairs(remainingMales, remainingFemales, profiles);

            foreach (var pair in aiPairs)
            {
                if (!availableMales.Remove(pair.MaleId) || !availableFemales.Remove(pair.FemaleId))
                    continue;

                matches.Add(new ParticipantAiMatch
                {
                    EventId = eventId,
                    VoteType = seatType,
                    MaleApplicationId = pair.MaleId,
                    FemaleApplicationId = pair.FemaleId,
                    MatchSource = MatchSource.ProfileAi,
                    Reason = pair.Reason
                });
            }
        }

        // Ensure every remaining confirmed participant is paired as long as both genders remain.
        // This guarantees full pair coverage even when AI returns partial/invalid pairs.
        var finalRemainingMales = availableMales.Values.OrderBy(m => m.Name).ToList();
        var finalRemainingFemales = availableFemales.Values.OrderBy(f => f.Name).ToList();
        var fallbackCount = Math.Min(finalRemainingMales.Count, finalRemainingFemales.Count);
        for (var i = 0; i < fallbackCount; i++)
        {
            var male = finalRemainingMales[i];
            var female = finalRemainingFemales[i];

            if (!availableMales.Remove(male.Id) || !availableFemales.Remove(female.Id))
                continue;

            matches.Add(new ParticipantAiMatch
            {
                EventId = eventId,
                VoteType = seatType,
                MaleApplicationId = male.Id,
                FemaleApplicationId = female.Id,
                MatchSource = MatchSource.ProfileAi,
                Reason = $"{male.Name}님과 {female.Name}님을 남은 참가자 기준으로 자동 보정 매칭했습니다."
            });
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

        var profileJson = JsonSerializer.Serialize(profiles, JsonOptions);
        var malesJson = JsonSerializer.Serialize(males.Select(m => new { m.Id, m.Name }), JsonOptions);
        var femalesJson = JsonSerializer.Serialize(females.Select(f => new { f.Id, f.Name }), JsonOptions);

        var prompt = $"""
            로테이션 소개팅 행사의 {seatingContext}입니다.
            남녀 1:1로 매칭하세요. 참가자 신상정보(이름, 생년월일, 거주지, 직장, 선호 나이대, 관심사, 음주, 흡연, 연락 허용, 투표 선택)를 모두 고려해
            가장 잘 어울리는 {pairCount}쌍을 만드세요.

            규칙:
            - 남성 id는 males 목록, 여성 id는 females 목록에 있는 값만 사용
            - 한 사람은 최대 한 번만 매칭
            - 정확히 {pairCount}쌍 반환
            - reason은 한국어 2~4문장으로 작성
            - 반드시 두 사람의 실제 신상정보 값(관심사, 거주지, 직장, 선호 나이대, 생년월일, 음주, 흡연, 연락 허용, 투표 선택 등)을 구체적으로 인용하며 왜 잘 맞는지 상세히 설명
            - "신상정보 기준으로 매칭"처럼 뭉뚱그린 표현만 쓰지 말 것
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

    private static List<AiPairResult> BuildFallbackPairs(
        List<ParticipantApplication> males,
        List<ParticipantApplication> females,
        List<ParticipantMatchProfile> profiles)
    {
        var profileById = profiles.ToDictionary(p => p.Id);
        var scores = new List<(int MaleId, int FemaleId, int Score)>();

        foreach (var male in males)
        {
            foreach (var female in females)
            {
                scores.Add((male.Id, female.Id, ComputeCompatibilityScore(
                    profileById.GetValueOrDefault(male.Id),
                    profileById.GetValueOrDefault(female.Id))));
            }
        }

        var usedMales = new HashSet<int>();
        var usedFemales = new HashSet<int>();
        var pairs = new List<AiPairResult>();

        foreach (var (maleId, femaleId, _) in scores.OrderByDescending(s => s.Score))
        {
            if (!usedMales.Add(maleId) || !usedFemales.Add(femaleId))
                continue;

            var maleProfile = profileById.GetValueOrDefault(maleId);
            var femaleProfile = profileById.GetValueOrDefault(femaleId);

            pairs.Add(new AiPairResult
            {
                MaleId = maleId,
                FemaleId = femaleId,
                Reason = BuildDetailedMatchReason(maleProfile, femaleProfile)
            });

            if (pairs.Count >= Math.Min(males.Count, females.Count))
                break;
        }

        return pairs;
    }

    private static string BuildDetailedMatchReason(ParticipantMatchProfile? male, ParticipantMatchProfile? female)
    {
        if (male is null || female is null)
            return "등록된 신상정보를 비교해 가장 적합한 조합으로 배치했습니다.";

        var reasons = new List<string>();

        if (male.VotedForId == female.Id)
            reasons.Add($"{male.Name}님이 중간투표에서 {female.Name}님을 선택했으나 상대방 선택과 맞지 않았습니다");
        else if (female.VotedForId == male.Id)
            reasons.Add($"{female.Name}님이 중간투표에서 {male.Name}님을 선택했으나 상대방 선택과 맞지 않았습니다");
        else if (!string.IsNullOrWhiteSpace(male.VotedForName) || !string.IsNullOrWhiteSpace(female.VotedForName))
        {
            var voteNote = new List<string>();
            if (!string.IsNullOrWhiteSpace(male.VotedForName))
                voteNote.Add($"{male.Name}님은 {male.VotedForName}님을 선택");
            if (!string.IsNullOrWhiteSpace(female.VotedForName))
                voteNote.Add($"{female.Name}님은 {female.VotedForName}님을 선택");
            reasons.Add(string.Join(", ", voteNote) + "했으나 서로 맞지 않아 신상정보를 우선 반영했습니다");
        }

        var commonInterests = GetOverlappingTokens(male.Interests, female.Interests);
        if (commonInterests.Count > 0)
            reasons.Add($"관심사가 '{string.Join(", ", commonInterests)}'로 겹칩니다");
        else if (!string.IsNullOrWhiteSpace(male.Interests) && !string.IsNullOrWhiteSpace(female.Interests))
            reasons.Add($"관심사를 고려했습니다({male.Name}님: {male.Interests} · {female.Name}님: {female.Interests})");

        if (HasSignificantOverlap(male.Residence, female.Residence))
            reasons.Add($"거주지가 '{FormatPairValue(male.Residence, female.Residence)}'로 가깝습니다");
        else if (!string.IsNullOrWhiteSpace(male.Residence) && !string.IsNullOrWhiteSpace(female.Residence))
            reasons.Add($"거주지를 고려했습니다({male.Residence} · {female.Residence})");

        if (HasSignificantOverlap(male.Workplace, female.Workplace))
            reasons.Add($"직장/업종이 '{FormatPairValue(male.Workplace, female.Workplace)}'로 유사합니다");
        else if (!string.IsNullOrWhiteSpace(male.Workplace) || !string.IsNullOrWhiteSpace(female.Workplace))
            reasons.Add($"직장 정보를 고려했습니다({ValueOrDash(male.Workplace)} · {ValueOrDash(female.Workplace)})");

        if (!string.IsNullOrWhiteSpace(male.PreferredAgeRange) && !string.IsNullOrWhiteSpace(female.BirthDate))
            reasons.Add($"{male.Name}님의 선호 연령대({male.PreferredAgeRange})와 {female.Name}님의 생년월일({female.BirthDate})이 맞습니다");

        if (!string.IsNullOrWhiteSpace(female.PreferredAgeRange) && !string.IsNullOrWhiteSpace(male.BirthDate))
            reasons.Add($"{female.Name}님의 선호 연령대({female.PreferredAgeRange})와 {male.Name}님의 생년월일({male.BirthDate})이 맞습니다");

        if (male.Drinking == female.Drinking && male.Drinking is "O" or "X")
            reasons.Add($"음주 성향이 둘 다 {male.Drinking}입니다");

        if (male.Smoking == female.Smoking && male.Smoking is "O" or "X")
            reasons.Add($"흡연 성향이 둘 다 {male.Smoking}입니다");

        if (male.AllowContact == "O" && female.AllowContact == "O")
            reasons.Add("둘 다 연락 허용 의사가 있습니다");

        if (reasons.Count == 0)
        {
            var profileHints = new List<string>();
            if (!string.IsNullOrWhiteSpace(male.Interests))
                profileHints.Add($"{male.Name}님 관심사 {male.Interests}");
            if (!string.IsNullOrWhiteSpace(female.Interests))
                profileHints.Add($"{female.Name}님 관심사 {female.Interests}");
            if (!string.IsNullOrWhiteSpace(male.Residence))
                profileHints.Add($"{male.Name}님 거주 {male.Residence}");
            if (!string.IsNullOrWhiteSpace(female.Residence))
                profileHints.Add($"{female.Name}님 거주 {female.Residence}");

            if (profileHints.Count > 0)
                return string.Join(", ", profileHints) + " 등을 종합해 가장 잘 맞는 조합으로 배치했습니다.";

            return $"{male.Name}님과 {female.Name}님의 등록된 신상정보를 비교해 남녀 중 가장 궁합이 좋은 조합으로 배치했습니다.";
        }

        return string.Join(". ", reasons) + ".";
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

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static int ComputeCompatibilityScore(ParticipantMatchProfile? male, ParticipantMatchProfile? female)
    {
        if (male is null || female is null)
            return 0;

        var score = 0;

        if (male.VotedForId == female.Id || female.VotedForId == male.Id)
            score += 30;

        score += TextOverlapScore(male.Interests, female.Interests) * 4;
        score += TextOverlapScore(male.Residence, female.Residence) * 3;
        score += TextOverlapScore(male.Workplace, female.Workplace) * 2;
        score += TextOverlapScore(male.PreferredAgeRange, female.BirthDate) * 2;
        score += TextOverlapScore(female.PreferredAgeRange, male.BirthDate) * 2;

        if (male.Drinking == female.Drinking && male.Drinking is not null)
            score += 2;
        if (male.Smoking == female.Smoking && male.Smoking is not null)
            score += 2;
        if (male.AllowContact == "O" && female.AllowContact == "O")
            score += 3;

        return score;
    }

    private static int TextOverlapScore(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return 0;

        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        return leftTokens.Count(t => rightTokens.Contains(t));
    }

    private static HashSet<string> Tokenize(string text) =>
        text.Split([' ', ',', '·', '/', '|', '，', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length >= 2)
            .ToHashSet();

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

        public static ParticipantMatchProfile FromApplication(
            ParticipantApplication app,
            int votedForId = 0,
            string? votedForName = null) =>
            new()
            {
                Id = app.Id,
                Name = app.Name,
                Gender = app.Gender ?? "",
                BirthDate = app.BirthDate,
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
