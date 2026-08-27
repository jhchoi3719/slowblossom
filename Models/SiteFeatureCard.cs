namespace RotationDating.Web.Models;

public class SiteFeatureCard : ISiteSortable
{
    public int Id { get; set; }
    public string Eyebrow { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Pill { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public string Variant { get; set; } = "store";
    public int SortOrder { get; set; }
}
