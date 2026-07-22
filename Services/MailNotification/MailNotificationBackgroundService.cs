using Microsoft.Extensions.DependencyInjection;

namespace RotationDating.Web.Services.MailNotification;

public sealed class MailNotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MailNotificationBackgroundService> _logger;

    public MailNotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MailNotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delaySeconds = 60;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var settingsProvider = scope.ServiceProvider.GetRequiredService<MailNotificationSettingsProvider>();
                var monitor = scope.ServiceProvider.GetRequiredService<NaverImapMailMonitor>();

                var settings = await settingsProvider.GetEffectiveSettingsAsync(stoppingToken);
                delaySeconds = Math.Clamp(settings.PollIntervalSeconds, 30, 600);

                if (await monitor.IsConfiguredAsync(stoppingToken))
                {
                    var count = await monitor.CheckAsync(stoppingToken);
                    if (count > 0)
                        _logger.LogInformation("Mail notification sent {Count} Telegram message(s).", count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Mail notification check failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }
}
