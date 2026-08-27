namespace RotationDating.Web.Models;

public class SiteSection : ISiteSortable
{
    public int Id { get; set; }
    public string Heading { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ImageUrl1 { get; set; } = string.Empty;
    public string ImageUrl2 { get; set; } = string.Empty;
    public string ImageAlt1 { get; set; } = string.Empty;
    public string ImageAlt2 { get; set; } = string.Empty;
    public bool IsReversed { get; set; }
    public int SortOrder { get; set; }
}
