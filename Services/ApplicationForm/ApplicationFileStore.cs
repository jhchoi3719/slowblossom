namespace RotationDating.Web.Services.ApplicationForm;

// 신청서 서류 사진: 공개 /uploads 밖에 저장하고 관리자 엔드포인트로만 내려준다.
public sealed class ApplicationFileStore
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Render 디스크(1GB)가 꽉 차면 SQLite까지 멈추므로, 여유 공간이 이보다 적으면 사진을 받지 않는다.
    public const long DefaultMinFreeBytes = 150L * 1024 * 1024;

    private readonly string _directory;
    private readonly long _minFreeBytes;

    public ApplicationFileStore(SiteStorageOptions storage) : this(storage, DefaultMinFreeBytes)
    {
    }

    public ApplicationFileStore(SiteStorageOptions storage, long minFreeBytes)
    {
        _directory = Path.Combine(storage.RootPath, "private", "applications");
        _minFreeBytes = minFreeBytes;
        Directory.CreateDirectory(_directory);
    }

    private bool HasRoom()
    {
        try
        {
            return new DriveInfo(Path.GetFullPath(_directory)).AvailableFreeSpace >= _minFreeBytes;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    public async Task<(string? FileName, string? Error)> TrySaveAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return (null, null);

        if (file.Length > MaxBytes)
            return (null, "size");

        if (!HasRoom())
            return (null, "disk");

        // 파일 이름 대신 실제 내용(앞부분 바이트)으로 jpg/png인지 판단한다.
        var header = new byte[8];
        await using (var peek = file.OpenReadStream())
            await peek.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        var extension = DetectExtension(header);
        if (extension is null)
            return (null, "type");

        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = File.Create(Path.Combine(_directory, fileName));
        await file.CopyToAsync(stream);
        return (fileName, null);
    }

    private static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegMagic))
            return ".jpg";
        if (header.StartsWith(PngMagic))
            return ".png";
        return null;
    }

    public string? GetPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return null;

        var path = Path.Combine(_directory, fileName);
        return File.Exists(path) ? path : null;
    }

    public static string? FileNameOf(Models.ParticipantApplication app, string kind) => kind switch
    {
        "photo" => app.PhotoFile,
        "employment" => app.EmploymentProofFile,
        "marriage" => app.MarriageCertFile,
        _ => null
    };

    public void DeleteFilesOf(Models.ParticipantApplication app)
    {
        Delete(app.PhotoFile);
        Delete(app.EmploymentProofFile);
        Delete(app.MarriageCertFile);
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
