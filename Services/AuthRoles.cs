namespace RotationDating.Web.Services;

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string Participant = "Participant";

    private static readonly HashSet<string> AdminUserNames = new(StringComparer.Ordinal)
    {
        "최준형",
        "이효정"
    };

    public static bool IsAdmin(string username) =>
        AdminUserNames.Contains(username.Trim());
}

public static class AuthClaims
{
    public const string EventId = "event_id";
    public const string EventTitle = "event_title";
    public const string ApplicationId = "application_id";
    public const string Gender = "gender";
    public const string VenueLabel = "venue_label";
    public const string EventDateLabel = "event_date_label";
}
