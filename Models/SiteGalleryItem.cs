namespace RotationDating.Web.Models;

public class SiteGalleryItem : ISiteSortable
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>추가 사진 주소. 한 줄에 하나씩 저장합니다.</summary>
    public string ExtraImageUrls { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public IReadOnlyList<string> ExtraImageList => SplitUrls(ExtraImageUrls);

    /// <summary>대표 사진을 첫 장으로 하는 전체 사진 목록입니다.</summary>
    public IReadOnlyList<string> AllImageUrls
    {
        get
        {
            var urls = new List<string>();
            if (!string.IsNullOrWhiteSpace(ImageUrl))
                urls.Add(ImageUrl.Trim());
            foreach (var url in ExtraImageList)
            {
                if (!urls.Contains(url, StringComparer.Ordinal))
                    urls.Add(url);
            }
            return urls;
        }
    }

    public static IReadOnlyList<string> SplitUrls(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
