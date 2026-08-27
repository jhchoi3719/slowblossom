namespace RotationDating.Web.Models;

public interface ISiteSortable
{
    int Id { get; }
    int SortOrder { get; set; }
}
