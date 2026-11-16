using Microsoft.EntityFrameworkCore;
using RotationDating.Web.Data;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services.LoveLibrary;

// 전자책 PDF는 공개 폴더 밖({DATA_DIR}/private/ebooks)에 두고, 설문을 마친 사람에게만 내려준다.
public sealed class EBookStore
{
    public const long MaxBytes = 60 * 1024 * 1024;
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();

    private readonly string _directory;

    public EBookStore(SiteStorageOptions storage)
    {
        _directory = Path.Combine(storage.RootPath, "private", "ebooks");
        Directory.CreateDirectory(_directory);
    }

    public string? GetPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName))
            return null;
        var path = Path.Combine(_directory, fileName);
        return File.Exists(path) ? path : null;
    }

    // 저장한 파일 이름, PDF가 아니거나 너무 크면 null
    public async Task<string?> SaveAsync(IFormFile file)
    {
        if (file.Length is 0 or > MaxBytes)
            return null;

        await using var input = file.OpenReadStream();
        var head = new byte[PdfMagic.Length];
        var read = await input.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false);
        if (read < head.Length || !head.AsSpan().SequenceEqual(PdfMagic))
            return null;

        var name = $"{Guid.NewGuid():N}.pdf";
        await using var output = File.Create(Path.Combine(_directory, name));
        await output.WriteAsync(head);
        await input.CopyToAsync(output);
        return name;
    }

    // 지운 책이나 교체된 예전 PDF는 바로 지우지 않고 trash 폴더로 옮겨 둔다.
    public void Delete(string fileName)
    {
        var path = GetPath(fileName);
        if (path is null)
            return;
        var trash = Path.Combine(_directory, "trash");
        Directory.CreateDirectory(trash);
        File.Move(path, Path.Combine(trash, $"{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}"), overwrite: true);
    }

    // 처음 배포할 때 전자책이 하나도 없으면 저장소에 함께 넣어 둔 PDF로 채운다.
    public async Task SeedAsync(AppDbContext db, string seedDirectory)
    {
        if (await db.EBooks.AnyAsync() || !Directory.Exists(seedDirectory))
            return;

        var seeds = new[]
        {
            (File: "love-start-guide.pdf", Title: "연애 시작 가이드", Subtitle: "첫 만남부터 연애 초기 3개월까지, 남자 마음 · 여자 마음", Audience: LoveSurveyOptions.BeforeDating),
            (File: "mbti-love-guide.pdf", Title: "MBTI 연애 가이드", Subtitle: "\"그 사람은 왜 저럴까\"를 16가지 유형으로 읽어요", Audience: LoveSurveyOptions.Common)
        };

        var order = 0;
        foreach (var seed in seeds)
        {
            var source = Path.Combine(seedDirectory, seed.File);
            if (!File.Exists(source))
                continue;

            var name = $"{Guid.NewGuid():N}.pdf";
            File.Copy(source, Path.Combine(_directory, name));
            db.EBooks.Add(new EBook
            {
                Title = seed.Title,
                Subtitle = seed.Subtitle,
                Audience = seed.Audience,
                FileName = name,
                SortOrder = order++
            });
        }

        await db.SaveChangesAsync();
    }
}
