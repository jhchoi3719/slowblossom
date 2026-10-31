using RotationDating.Web.Models;
using RotationDating.Web.Services.MailNotification;

namespace RotationDating.Web.Services.ApplicationForm;

public sealed class ApplicationNotifier
{
    private readonly TelegramNotifier _telegram;
    private readonly ILogger<ApplicationNotifier> _logger;

    public ApplicationNotifier(TelegramNotifier telegram, ILogger<ApplicationNotifier> logger)
    {
        _telegram = telegram;
        _logger = logger;
    }

    public static string BuildMessage(Event evt, ParticipantApplication app)
    {
        var lines = new List<string>
        {
            $"🌸 새 신청서 ({evt.TabLabel})",
            string.Join(" · ", new[] { app.Name, app.Gender, ParticipantApplication.ToBirthYearLabel(app.BirthDate) }
                .Where(s => !string.IsNullOrWhiteSpace(s))),
            $"{app.Phone} / 카톡 {app.KakaoId}"
        };
        if (app.IsReturning)
            lines.Add("기존 참가자");
        return string.Join("\n", lines);
    }

    // 알림 실패나 지연이 신청 저장·완료 화면을 막으면 안 된다 (최대 5초만 기다린다).
    public async Task NotifyAsync(Event evt, ParticipantApplication app)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            if (!await _telegram.IsConfiguredAsync(timeout.Token))
                return;

            var result = await _telegram.SendMessageAsync(BuildMessage(evt, app), timeout.Token);
            if (!result.Success)
                _logger.LogWarning("Application telegram notice failed: {Error}", result.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Application telegram notice failed for application {Id}", app.Id);
        }
    }
}
