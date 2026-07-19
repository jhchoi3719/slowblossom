namespace RotationDating.Web.Models;

public class VenueConsentSetting
{
    public int Id { get; set; }
    public string VenueKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Title { get; set; } = "참가자 확인 및 동의";
    public string Content { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
