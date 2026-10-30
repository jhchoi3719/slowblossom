using System.Text.Json;

namespace RotationDating.Web.Services.ApplicationForm;

// 검증 실패로 신청 페이지에 돌아갈 때 입력값과 오류 목록을 세션에 잠시 보관한다.
public static class ApplicationDraftStore
{
    private static string DraftKey(int eventId) => $"apply_draft:{eventId}";
    private static string ErrorsKey(int eventId) => $"apply_errors:{eventId}";

    public static void Save(HttpContext context, int eventId, ApplicationFormInput input, IEnumerable<string> errors)
    {
        context.Session.SetString(DraftKey(eventId), JsonSerializer.Serialize(input));
        context.Session.SetString(ErrorsKey(eventId), JsonSerializer.Serialize(errors.ToList()));
    }

    public static ApplicationFormInput? LoadDraft(HttpContext context, int eventId)
    {
        var json = context.Session.GetString(DraftKey(eventId));
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ApplicationFormInput>(json);
    }

    public static List<string> LoadErrors(HttpContext context, int eventId)
    {
        var json = context.Session.GetString(ErrorsKey(eventId));
        return string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    public static void Clear(HttpContext context, int eventId)
    {
        context.Session.Remove(DraftKey(eventId));
        context.Session.Remove(ErrorsKey(eventId));
    }
}
