namespace RotationDating.Web.Services.MailNotification;

public sealed class NotificationSendResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SentSubject { get; init; }

    public static NotificationSendResult Ok(string? sentSubject = null) => new()
    {
        Success = true,
        SentSubject = sentSubject
    };

    public static NotificationSendResult Fail(string? errorCode, string? errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
