namespace RotationDating.Web.Services;

public static class ParticipantPasswordSetupStore
{
    private const string Key = "ParticipantPasswordSetupAppId";

    public static void SetPendingApplicationId(HttpContext context, int applicationId) =>
        context.Session.SetInt32(Key, applicationId);

    public static int? GetPendingApplicationId(HttpContext context) =>
        context.Session.GetInt32(Key);

    public static void Clear(HttpContext context) =>
        context.Session.Remove(Key);
}
