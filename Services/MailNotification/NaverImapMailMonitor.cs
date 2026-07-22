using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace RotationDating.Web.Services.MailNotification;

public sealed class NaverImapMailMonitor
{
    private const string ManualCheckSubjectPrefix = "[네이버 폼]";

    private readonly MailNotificationSettingsProvider _settingsProvider;
    private readonly TelegramNotifier _telegramNotifier;
    private readonly MailNotificationStateStore _stateStore;
    private readonly ILogger<NaverImapMailMonitor> _logger;

    public NaverImapMailMonitor(
        MailNotificationSettingsProvider settingsProvider,
        TelegramNotifier telegramNotifier,
        MailNotificationStateStore stateStore,
        ILogger<NaverImapMailMonitor> logger)
    {
        _settingsProvider = settingsProvider;
        _telegramNotifier = telegramNotifier;
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsProvider.GetEffectiveSettingsAsync(cancellationToken);
        return settings.Enabled
            && !string.IsNullOrWhiteSpace(settings.NaverImap.Username)
            && !string.IsNullOrWhiteSpace(settings.NaverImap.Password)
            && await _telegramNotifier.IsConfiguredAsync(cancellationToken);
    }

    public async Task<int> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync(cancellationToken))
            return 0;

        var settings = await _settingsProvider.GetEffectiveSettingsAsync(cancellationToken);
        var imap = settings.NaverImap;
        var state = await _stateStore.LoadAsync(cancellationToken);
        var notifiedCount = 0;

        using var client = new ImapClient();
        await client.ConnectAsync(imap.Host, imap.Port, true, cancellationToken);
        await client.AuthenticateAsync(imap.Username, imap.Password, cancellationToken);

        var folder = await client.GetFolderAsync(imap.Folder, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        SyncFolderBaseline(state, folder);

        if (state.LastUid == 0 && folder.Count > 0)
        {
            state.LastUid = GetHighestUid(folder);
            state.LastCheckedAtUtc = DateTime.UtcNow;
            await _stateStore.SaveAsync(state, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            _logger.LogInformation("Initialized mail monitor baseline at UID {Uid}", state.LastUid);
            return 0;
        }

        var query = SearchQuery.Uids(new UniqueIdRange(new UniqueId(state.LastUid + 1), UniqueId.MaxValue));
        var uids = (await folder.SearchAsync(query, cancellationToken))
            .Where(uid => uid.Id > state.LastUid)
            .OrderBy(uid => uid.Id)
            .ToList();

        foreach (var uid in uids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var message = await folder.GetMessageAsync(uid, cancellationToken);
            if (!MatchesFilter(message, imap))
            {
                state.LastUid = uid.Id;
                continue;
            }

            var subject = string.IsNullOrWhiteSpace(message.Subject) ? "(제목 없음)" : message.Subject.Trim();

            if ((await _telegramNotifier.SendMessageAsync(subject, cancellationToken)).Success)
            {
                notifiedCount++;
                state.LastNotifiedAtUtc = DateTime.UtcNow;
                state.LastNotifiedSubject = subject;
                _logger.LogInformation("Sent Telegram message for mail UID {Uid}: {Subject}", uid.Id, subject);
            }

            state.LastUid = uid.Id;
        }

        state.LastCheckedAtUtc = DateTime.UtcNow;
        await _stateStore.SaveAsync(state, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        return notifiedCount;
    }

    public async Task<NotificationSendResult> SendLatestNaverFormNotificationAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync(cancellationToken))
            return NotificationSendResult.Fail("notConfigured", "IMAP 또는 텔레그램 설정이 필요합니다.");

        var settings = await _settingsProvider.GetEffectiveSettingsAsync(cancellationToken);
        var imap = settings.NaverImap;

        using var client = new ImapClient();
        await client.ConnectAsync(imap.Host, imap.Port, true, cancellationToken);
        await client.AuthenticateAsync(imap.Username, imap.Password, cancellationToken);

        var folder = await client.GetFolderAsync(imap.Folder, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        var uids = await folder.SearchAsync(SearchQuery.SubjectContains(ManualCheckSubjectPrefix), cancellationToken);
        if (uids.Count == 0)
        {
            await client.DisconnectAsync(true, cancellationToken);
            return NotificationSendResult.Fail(
                "noMail",
                $"제목이 '{ManualCheckSubjectPrefix}'(으)로 시작하는 메일이 없습니다.");
        }

        var summaries = folder.Fetch(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId).ToList();
        var latest = summaries
            .Where(summary => summary.Envelope?.Subject?.StartsWith(ManualCheckSubjectPrefix, StringComparison.Ordinal) == true)
            .Where(summary => MatchesFromFilter(summary, imap))
            .OrderByDescending(summary => summary.Envelope?.Date?.UtcDateTime ?? DateTime.MinValue)
            .ThenByDescending(summary => summary.UniqueId.Id)
            .FirstOrDefault();

        if (latest?.Envelope?.Subject is not { } rawSubject)
        {
            await client.DisconnectAsync(true, cancellationToken);
            return NotificationSendResult.Fail(
                "noMail",
                $"제목이 '{ManualCheckSubjectPrefix}'(으)로 시작하는 메일이 없습니다.");
        }

        var subject = rawSubject.Trim();
        var sendResult = await _telegramNotifier.SendMessageAsync(subject, cancellationToken);
        if (!sendResult.Success)
        {
            await client.DisconnectAsync(true, cancellationToken);
            return sendResult;
        }

        var state = await _stateStore.LoadAsync(cancellationToken);
        state.LastUid = Math.Max(state.LastUid, latest.UniqueId.Id);
        state.LastNotifiedAtUtc = DateTime.UtcNow;
        state.LastNotifiedSubject = subject;
        state.LastCheckedAtUtc = DateTime.UtcNow;
        await _stateStore.SaveAsync(state, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Manual check sent latest Naver Form mail: {Subject}", subject);
        return NotificationSendResult.Ok(subject);
    }

    private static void SyncFolderBaseline(MailNotificationState state, IMailFolder folder)
    {
        var uidValidity = folder.UidValidity;
        var folderName = folder.FullName;

        if (!string.Equals(state.Folder, folderName, StringComparison.Ordinal)
            || state.UidValidity != uidValidity)
        {
            state.Folder = folderName;
            state.UidValidity = uidValidity;
            state.LastUid = folder.Count > 0 ? GetHighestUid(folder) : 0;
        }
    }

    private static uint GetHighestUid(IMailFolder folder)
    {
        if (folder.UidNext is { Id: > 1 } uidNext)
            return uidNext.Id - 1;

        if (folder.Count == 0)
            return 0;

        return folder.Fetch(folder.Count - 1, folder.Count - 1, MessageSummaryItems.UniqueId).First().UniqueId.Id;
    }

    private static bool MatchesFilter(MimeMessage message, NaverImapOptions imapOptions)
    {
        if (!string.IsNullOrWhiteSpace(imapOptions.FromFilter))
        {
            var fromAddresses = message.From.Mailboxes.Select(mailbox => mailbox.Address).ToList();
            if (fromAddresses.All(address =>
                    !address.Contains(imapOptions.FromFilter, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(imapOptions.SubjectContains)
            && (message.Subject?.Contains(imapOptions.SubjectContains, StringComparison.OrdinalIgnoreCase) != true))
            return false;

        return true;
    }

    private static bool MatchesFromFilter(IMessageSummary summary, NaverImapOptions imapOptions)
    {
        if (string.IsNullOrWhiteSpace(imapOptions.FromFilter))
            return true;

        var fromAddresses = summary.Envelope?.From.Mailboxes.Select(mailbox => mailbox.Address).ToList() ?? [];
        return fromAddresses.Any(address =>
            address.Contains(imapOptions.FromFilter, StringComparison.OrdinalIgnoreCase));
    }
}
