using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Models;
using RotationDating.Web.Services;

namespace RotationDating.Web.Data;

public static class DatabaseInitializer
{
    private static readonly Dictionary<string, string> ApplicationColumnMigrations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BirthDate"] = "ALTER TABLE ParticipantApplications ADD COLUMN BirthDate TEXT NULL",
        ["Residence"] = "ALTER TABLE ParticipantApplications ADD COLUMN Residence TEXT NULL",
        ["Workplace"] = "ALTER TABLE ParticipantApplications ADD COLUMN Workplace TEXT NULL",
        ["PreferredAgeRange"] = "ALTER TABLE ParticipantApplications ADD COLUMN PreferredAgeRange TEXT NULL",
        ["Interests"] = "ALTER TABLE ParticipantApplications ADD COLUMN Interests TEXT NULL",
        ["Drinking"] = "ALTER TABLE ParticipantApplications ADD COLUMN Drinking INTEGER NULL",
        ["Smoking"] = "ALTER TABLE ParticipantApplications ADD COLUMN Smoking INTEGER NULL",
        ["AllowContact"] = "ALTER TABLE ParticipantApplications ADD COLUMN AllowContact INTEGER NULL",
        ["IsConfirmed"] = "ALTER TABLE ParticipantApplications ADD COLUMN IsConfirmed INTEGER NOT NULL DEFAULT 0",
        ["Memo"] = "ALTER TABLE ParticipantApplications ADD COLUMN Memo TEXT NULL",
        ["Password"] = "ALTER TABLE ParticipantApplications ADD COLUMN Password TEXT NULL",
        ["HasArrived"] = "ALTER TABLE ParticipantApplications ADD COLUMN HasArrived INTEGER NOT NULL DEFAULT 0",
        ["ConsentAcceptedAt"] = "ALTER TABLE ParticipantApplications ADD COLUMN ConsentAcceptedAt TEXT NULL"
    };

    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        const string createSql = """
            CREATE TABLE IF NOT EXISTS ParticipantApplications (
                Id INTEGER NOT NULL CONSTRAINT PK_ParticipantApplications PRIMARY KEY AUTOINCREMENT,
                EventId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                BirthDate TEXT NULL,
                Gender TEXT NULL,
                Phone TEXT NULL,
                Residence TEXT NULL,
                Workplace TEXT NULL,
                PreferredAgeRange TEXT NULL,
                Interests TEXT NULL,
                Drinking INTEGER NULL,
                Smoking INTEGER NULL,
                AllowContact INTEGER NULL,
                IsConfirmed INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_ParticipantApplications_Events_EventId
                    FOREIGN KEY (EventId) REFERENCES Events (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_ParticipantApplications_EventId
                ON ParticipantApplications (EventId);
            """;

        await db.Database.ExecuteSqlRawAsync(createSql);
        await MigrateParticipantApplicationsAsync(db);

        const string votesSql = """
            CREATE TABLE IF NOT EXISTS ParticipantVotes (
                Id INTEGER NOT NULL CONSTRAINT PK_ParticipantVotes PRIMARY KEY AUTOINCREMENT,
                EventId INTEGER NOT NULL,
                VoterApplicationId INTEGER NOT NULL,
                TargetApplicationId INTEGER NOT NULL,
                VoteType INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_ParticipantVotes_Events_EventId
                    FOREIGN KEY (EventId) REFERENCES Events (Id) ON DELETE CASCADE,
                CONSTRAINT FK_ParticipantVotes_Voter_ApplicationId
                    FOREIGN KEY (VoterApplicationId) REFERENCES ParticipantApplications (Id) ON DELETE CASCADE,
                CONSTRAINT FK_ParticipantVotes_Target_ApplicationId
                    FOREIGN KEY (TargetApplicationId) REFERENCES ParticipantApplications (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ParticipantVotes_Voter_Target_Type
                ON ParticipantVotes (VoterApplicationId, TargetApplicationId, VoteType);
            """;

        await db.Database.ExecuteSqlRawAsync(votesSql);
        await MigrateParticipantVotesAsync(db);

        const string questionsSql = """
            CREATE TABLE IF NOT EXISTS QuestionCards (
                Id INTEGER NOT NULL CONSTRAINT PK_QuestionCards PRIMARY KEY AUTOINCREMENT,
                Text TEXT NOT NULL,
                SortOrder INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_QuestionCards_SortOrder
                ON QuestionCards (SortOrder);
            """;

        await db.Database.ExecuteSqlRawAsync(questionsSql);
        await SeedQuestionCardsAsync(db);

        const string aiMatchesSql = """
            CREATE TABLE IF NOT EXISTS ParticipantAiMatches (
                Id INTEGER NOT NULL CONSTRAINT PK_ParticipantAiMatches PRIMARY KEY AUTOINCREMENT,
                EventId INTEGER NOT NULL,
                VoteType INTEGER NOT NULL,
                MaleApplicationId INTEGER NOT NULL,
                FemaleApplicationId INTEGER NOT NULL,
                MatchSource INTEGER NOT NULL DEFAULT 3,
                Reason TEXT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_ParticipantAiMatches_Male_ApplicationId
                    FOREIGN KEY (MaleApplicationId) REFERENCES ParticipantApplications (Id) ON DELETE CASCADE,
                CONSTRAINT FK_ParticipantAiMatches_Female_ApplicationId
                    FOREIGN KEY (FemaleApplicationId) REFERENCES ParticipantApplications (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_ParticipantAiMatches_Event_VoteType
                ON ParticipantAiMatches (EventId, VoteType);
            """;

        await db.Database.ExecuteSqlRawAsync(aiMatchesSql);
        await MigrateParticipantAiMatchesAsync(db);
        await MigrateEventsAsync(db);
        await MigrateDatePollTablesAsync(db);
        await MigrateSurveyTablesAsync(db);
        await MigrateConsentSettingsAsync(db);
    }

    private static async Task MigrateConsentSettingsAsync(AppDbContext db)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS VenueConsentSettings (
                Id INTEGER NOT NULL CONSTRAINT PK_VenueConsentSettings PRIMARY KEY AUTOINCREMENT,
                VenueKey TEXT NOT NULL,
                IsEnabled INTEGER NOT NULL DEFAULT 0,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_VenueConsentSettings_VenueKey
                ON VenueConsentSettings (VenueKey);
            """;

        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task MigrateSurveyTablesAsync(AppDbContext db)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS Surveys (
                Id INTEGER NOT NULL CONSTRAINT PK_Surveys PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Slug TEXT NOT NULL,
                WelcomeTitle TEXT NOT NULL,
                WelcomeContent TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Surveys_Slug ON Surveys (Slug);

            CREATE TABLE IF NOT EXISTS SurveyQuestions (
                Id INTEGER NOT NULL CONSTRAINT PK_SurveyQuestions PRIMARY KEY AUTOINCREMENT,
                SurveyId INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                QuestionType INTEGER NOT NULL DEFAULT 0,
                AllowMultiple INTEGER NOT NULL DEFAULT 0,
                IsRequired INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL,
                CONSTRAINT FK_SurveyQuestions_Surveys_SurveyId
                    FOREIGN KEY (SurveyId) REFERENCES Surveys (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_SurveyQuestions_SurveyId_SortOrder
                ON SurveyQuestions (SurveyId, SortOrder);

            CREATE TABLE IF NOT EXISTS SurveyOptions (
                Id INTEGER NOT NULL CONSTRAINT PK_SurveyOptions PRIMARY KEY AUTOINCREMENT,
                QuestionId INTEGER NOT NULL,
                Text TEXT NOT NULL,
                AllowCustomInput INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL,
                CONSTRAINT FK_SurveyOptions_SurveyQuestions_QuestionId
                    FOREIGN KEY (QuestionId) REFERENCES SurveyQuestions (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_SurveyOptions_QuestionId_SortOrder
                ON SurveyOptions (QuestionId, SortOrder);

            CREATE TABLE IF NOT EXISTS SurveyResponses (
                Id INTEGER NOT NULL CONSTRAINT PK_SurveyResponses PRIMARY KEY AUTOINCREMENT,
                SurveyId INTEGER NOT NULL,
                PhoneNumber TEXT NOT NULL DEFAULT '',
                NormalizedPhone TEXT NOT NULL DEFAULT '',
                MarketingConsent INTEGER NOT NULL DEFAULT 0,
                SubmittedAt TEXT NOT NULL,
                CONSTRAINT FK_SurveyResponses_Surveys_SurveyId
                    FOREIGN KEY (SurveyId) REFERENCES Surveys (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_SurveyResponses_SurveyId
                ON SurveyResponses (SurveyId);

            CREATE TABLE IF NOT EXISTS SurveyAnswers (
                Id INTEGER NOT NULL CONSTRAINT PK_SurveyAnswers PRIMARY KEY AUTOINCREMENT,
                ResponseId INTEGER NOT NULL,
                QuestionId INTEGER NOT NULL,
                OptionId INTEGER NULL,
                TextAnswer TEXT NULL,
                CONSTRAINT FK_SurveyAnswers_SurveyResponses_ResponseId
                    FOREIGN KEY (ResponseId) REFERENCES SurveyResponses (Id) ON DELETE CASCADE,
                CONSTRAINT FK_SurveyAnswers_SurveyQuestions_QuestionId
                    FOREIGN KEY (QuestionId) REFERENCES SurveyQuestions (Id) ON DELETE CASCADE,
                CONSTRAINT FK_SurveyAnswers_SurveyOptions_OptionId
                    FOREIGN KEY (OptionId) REFERENCES SurveyOptions (Id) ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS IX_SurveyAnswers_ResponseId_QuestionId
                ON SurveyAnswers (ResponseId, QuestionId);
            """;

        await db.Database.ExecuteSqlRawAsync(sql);
        await MigrateSurveyResponseConsentAsync(db);
        await MigrateSurveyOptionCustomInputAsync(db);
    }

    private static async Task MigrateSurveyOptionCustomInputAsync(AppDbContext db)
    {
        var columns = await GetTableColumnsAsync(db, "SurveyOptions");
        if (!columns.Contains("AllowCustomInput"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SurveyOptions ADD COLUMN AllowCustomInput INTEGER NOT NULL DEFAULT 0");
    }

    private static async Task MigrateSurveyResponseConsentAsync(AppDbContext db)
    {
        var columns = await GetTableColumnsAsync(db, "SurveyResponses");
        if (!columns.Contains("PhoneNumber"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SurveyResponses ADD COLUMN PhoneNumber TEXT NOT NULL DEFAULT ''");
        if (!columns.Contains("MarketingConsent"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SurveyResponses ADD COLUMN MarketingConsent INTEGER NOT NULL DEFAULT 0");
        if (!columns.Contains("NormalizedPhone"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE SurveyResponses ADD COLUMN NormalizedPhone TEXT NOT NULL DEFAULT ''");
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE SurveyResponses
                SET NormalizedPhone = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(PhoneNumber, '-', ''), ' ', ''), '.', ''), '(', ''), ')', '')
                WHERE NormalizedPhone = '';
                UPDATE SurveyResponses
                SET PhoneNumber = NormalizedPhone
                WHERE NormalizedPhone != '' AND PhoneNumber != NormalizedPhone;
                """);
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_SurveyResponses_SurveyId_NormalizedPhone
                ON SurveyResponses (SurveyId, NormalizedPhone)
                WHERE NormalizedPhone != '';
            """);
    }

    private static async Task MigrateEventsAsync(AppDbContext db)
    {
        var columns = await GetTableColumnsAsync(db, "Events");
        if (!columns.Contains("Kind"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Events ADD COLUMN Kind INTEGER NOT NULL DEFAULT 0");
        if (!columns.Contains("FinalizedDate"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Events ADD COLUMN FinalizedDate TEXT NULL");
    }

    private static async Task MigrateDatePollTablesAsync(AppDbContext db)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS EventCandidateDates (
                Id INTEGER NOT NULL CONSTRAINT PK_EventCandidateDates PRIMARY KEY AUTOINCREMENT,
                EventId INTEGER NOT NULL,
                EventDate TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                CONSTRAINT FK_EventCandidateDates_Events_EventId
                    FOREIGN KEY (EventId) REFERENCES Events (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_EventCandidateDates_EventId
                ON EventCandidateDates (EventId);

            CREATE TABLE IF NOT EXISTS ApplicationAvailabilities (
                Id INTEGER NOT NULL CONSTRAINT PK_ApplicationAvailabilities PRIMARY KEY AUTOINCREMENT,
                ApplicationId INTEGER NOT NULL,
                AvailableDate TEXT NOT NULL,
                CONSTRAINT FK_ApplicationAvailabilities_Applications_ApplicationId
                    FOREIGN KEY (ApplicationId) REFERENCES ParticipantApplications (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_ApplicationAvailabilities_ApplicationId
                ON ApplicationAvailabilities (ApplicationId);
            """;

        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task MigrateParticipantAiMatchesAsync(AppDbContext db)
    {
        var columns = await GetTableColumnsAsync(db, "ParticipantAiMatches");
        if (!columns.Contains("MatchSource"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE ParticipantAiMatches ADD COLUMN MatchSource INTEGER NOT NULL DEFAULT 3");
    }

    private static async Task SeedQuestionCardsAsync(AppDbContext db)
    {
        if (await db.QuestionCards.AnyAsync())
            return;

        var cards = QuestionCardData.All
            .Select((text, index) => new QuestionCard
            {
                Text = text,
                SortOrder = index + 1
            })
            .ToList();

        db.QuestionCards.AddRange(cards);
        await db.SaveChangesAsync();
    }

    private static async Task MigrateParticipantApplicationsAsync(AppDbContext db)
    {
        var existingColumns = await GetTableColumnsAsync(db, "ParticipantApplications");

        foreach (var (columnName, sql) in ApplicationColumnMigrations)
        {
            if (!existingColumns.Contains(columnName))
                await db.Database.ExecuteSqlRawAsync(sql);
        }
    }

    private static async Task MigrateParticipantVotesAsync(AppDbContext db)
    {
        var columns = await GetTableColumnsAsync(db, "ParticipantVotes");
        if (!columns.Contains("Priority"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE ParticipantVotes ADD COLUMN Priority INTEGER NOT NULL DEFAULT 1");
        if (!columns.Contains("IsExplicitNone"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE ParticipantVotes ADD COLUMN IsExplicitNone INTEGER NOT NULL DEFAULT 0");

        await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_ParticipantVotes_Voter_Target_Type");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ParticipantVotes_Voter_Type_Priority
                ON ParticipantVotes (VoterApplicationId, VoteType, Priority);
            """);
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(AppDbContext db, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = db.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        return columns;
    }
}
