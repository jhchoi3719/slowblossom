using System.Text.Json;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public static class SurveyResponseDraftStore
{
    private static string Key(string slug) => $"survey_draft:{slug}";

    public static void Save(HttpContext context, string slug, SurveySubmitInput input) =>
        context.Session.SetString(Key(slug), JsonSerializer.Serialize(input));

    public static SurveySubmitInput? Load(HttpContext context, string slug)
    {
        var json = context.Session.GetString(Key(slug));
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<SurveySubmitInput>(json);
    }

    public static void Clear(HttpContext context, string slug) =>
        context.Session.Remove(Key(slug));
}
