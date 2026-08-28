namespace RotationDating.Web.Services;

public sealed class SiteUploadService
{
    public const string RequestPath = "/uploads/site";
    public const long MaxBytes = 8 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly string _directory;

    public SiteUploadService(SiteStorageOptions storage)
    {
        _directory = Path.Combine(storage.RootPath, "uploads", "site");
        Directory.CreateDirectory(_directory);
    }

    public async Task<(string? Url, string? Error)> TrySaveAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return (null, null);

        if (file.Length > MaxBytes)
            return (null, "size");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            return (null, "type");

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.Combine(_directory, fileName);
        await using var stream = File.Create(path);
        await file.CopyToAsync(stream);
        return ($"{RequestPath}/{fileName}", null);
    }

    public async Task<(List<string> Urls, string? Error)> TrySaveManyAsync(IEnumerable<IFormFile>? files)
    {
        var urls = new List<string>();
        string? firstError = null;
        if (files is null)
            return (urls, null);

        foreach (var file in files)
        {
            var (url, error) = await TrySaveAsync(file);
            firstError ??= error;
            if (!string.IsNullOrWhiteSpace(url))
                urls.Add(url);
        }
        return (urls, firstError);
    }

    public async Task<(string Url, string? Error)> ResolveImageAsync(IFormFile? file, string? urlField, string existing)
    {
        var (uploaded, error) = await TrySaveAsync(file);
        if (error is not null)
            return (existing, error);
        if (!string.IsNullOrWhiteSpace(uploaded))
            return (uploaded, null);
        return (urlField?.Trim() ?? existing, null);
    }
}
