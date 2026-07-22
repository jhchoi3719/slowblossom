namespace RotationDating.Web.Services.MailNotification;

public class MailNotificationOptions
{
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    public NaverImapOptions NaverImap { get; set; } = new();
    public TelegramOptions Telegram { get; set; } = new();
}

public class NaverImapOptions
{
    public string Host { get; set; } = "imap.naver.com";
    public int Port { get; set; } = 993;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Folder { get; set; } = "INBOX";
    public string? FromFilter { get; set; }
    public string? SubjectContains { get; set; } = "네이버폼";
}

public class TelegramOptions
{
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
}

public static class MailNotificationOptionsSetup
{
    public static void Configure(IConfiguration configuration, MailNotificationOptions options)
    {
        configuration.GetSection("MailNotification").Bind(options);

        if (bool.TryParse(Environment.GetEnvironmentVariable("MAIL_NOTIFICATION_ENABLED"), out var enabled))
            options.Enabled = enabled;

        options.NaverImap.Username = FirstNonEmpty(
            Environment.GetEnvironmentVariable("NAVER_IMAP_USERNAME"),
            options.NaverImap.Username);
        options.NaverImap.Password = FirstNonEmpty(
            Environment.GetEnvironmentVariable("NAVER_IMAP_PASSWORD"),
            options.NaverImap.Password);
        options.NaverImap.FromFilter = FirstNonEmpty(
            Environment.GetEnvironmentVariable("NAVER_IMAP_FROM_FILTER"),
            options.NaverImap.FromFilter);
        options.NaverImap.SubjectContains = FirstNonEmpty(
            Environment.GetEnvironmentVariable("NAVER_IMAP_SUBJECT_CONTAINS"),
            options.NaverImap.SubjectContains);

        options.Telegram.BotToken = FirstNonEmpty(
            Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"),
            options.Telegram.BotToken);
        options.Telegram.ChatId = FirstNonEmpty(
            Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID"),
            options.Telegram.ChatId);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }
}
