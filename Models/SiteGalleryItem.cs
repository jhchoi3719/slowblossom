namespace RotationDating.Web.Models;

public class SiteGalleryItem : ISiteSortable
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
