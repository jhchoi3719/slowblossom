namespace RotationDating.Web.Services.ApplicationForm;

// 신청서 서류 사진: 공개 /uploads 밖에 저장하고 관리자 엔드포인트로만 내려준다.
public sealed class ApplicationFileStore
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    private readonly string _directory;

    public ApplicationFileStore(SiteStorageOptions storage)
    {
        _directory = Path.Combine(storage.RootPath, "private", "applications");
        Directory.CreateDirectory(_directory);
    }

    public async Task<(string? FileName, string? Error)> TrySaveAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return (null, null);

        if (file.Length > MaxBytes)
            return (null, "size");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            return (null, "type");

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using var stream = File.Create(Path.Combine(_directory, fileName));
        await file.CopyToAsync(stream);
        return (fileName, null);
    }

    public string? GetPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return null;

        var path = Path.Combine(_directory, fileName);
        return File.Exists(path) ? path : null;
    }

    public void Delete(string? fileName)
    {
        var path = GetPath(fileName);
        if (path is null)
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // 지우지 못해도 신청서 삭제는 계속한다.
        }
    }
}
