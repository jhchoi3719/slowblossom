using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public sealed class SiteContentSnapshot
{
    public static SiteContentSnapshot Empty { get; } = new()
    {
        Settings = new Dictionary<string, string>(StringComparer.Ordinal),
        Sections = [],
        FeatureCards = [],
        GalleryItems = [],
        FaqItems = [],
        Posts = []
    };

    public required Dictionary<string, string> Settings { get; init; }
    public required List<SiteSection> Sections { get; init; }
    public required List<SiteFeatureCard> FeatureCards { get; init; }
    public required List<SiteGalleryItem> GalleryItems { get; init; }
    public required List<SiteFaqItem> FaqItems { get; init; }
    public required List<SitePost> Posts { get; init; }

    public string Get(string key, string fallback = "") =>
        Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    public IReadOnlyList<string> AboutItems =>
        Get(SiteContentKeys.AboutList)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<string> GalleryTabs
    {
        get
        {
            var tabs = new List<string> { "전체" };
            foreach (var category in GalleryItems.Select(item => item.Category).Distinct())
            {
                if (!string.IsNullOrWhiteSpace(category))
                    tabs.Add(category);
            }
            return tabs;
        }
    }

    public static string CssUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";
        return url.Replace("\\", "/", StringComparison.Ordinal).Replace("'", "%27", StringComparison.Ordinal);
    }
}
