namespace RotationDating.Web.Models;

public class SitePost : ISiteSortable
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int SortOrder { get; set; }
}
