namespace RotationDating.Web.Models;

public class SiteFaqItem : ISiteSortable
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
