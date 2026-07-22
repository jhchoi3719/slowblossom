using System.Net.Http.Json;
using System.Text.Json;

namespace RotationDating.Web.Services.MailNotification;

public sealed class TelegramNotifier
{
    private readonly HttpClient _httpClient;
    private readonly MailNotificationSettingsProvider _settingsProvider;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(
        HttpClient httpClient,
        MailNotificationSettingsProvider settingsProvider,
        ILogger<TelegramNotifier> logger)
    {
        _httpClient = httpClient;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var telegram = (await _settingsProvider.GetEffectiveSettingsAsync(cancellationToken)).Telegram;
        return !string.IsNullOrWhiteSpace(telegram.BotToken)
            && !string.IsNullOrWhiteSpace(telegram.ChatId);
    }

    public async Task<NotificationSendResult> SendMessageAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var telegram = (await _settingsProvider.GetEffectiveSettingsAsync(cancellationToken)).Telegram;
        if (string.IsNullOrWhiteSpace(telegram.BotToken) || string.IsNullOrWhiteSpace(telegram.ChatId))
            return NotificationSendResult.Fail("noTelegram", "텔레그램 Bot Token과 Chat ID를 먼저 저장하세요.");

        var url = $"https://api.telegram.org/bot{telegram.BotToken.Trim()}/sendMessage";
        using var response = await _httpClient.PostAsJsonAsync(url, new
        {
            chat_id = telegram.ChatId.Trim(),
            text
        }, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("ok", out var okElement) && okElement.GetBoolean())
                    return NotificationSendResult.Ok();
            }
            catch
            {
                return NotificationSendResult.Ok();
            }
        }

        _logger.LogWarning("Telegram send failed: {Status} {Body}", response.StatusCode, body);
        return ParseTelegramError(body);
    }

    private static NotificationSendResult ParseTelegramError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("description", out var description))
                return NotificationSendResult.Fail("telegram", description.GetString());

            if (root.TryGetProperty("error_code", out var errorCode))
                return NotificationSendResult.Fail(errorCode.ToString(), body);
        }
        catch
        {
            // ignore parse errors
        }

        return NotificationSendResult.Fail("telegram", body);
    }
}
