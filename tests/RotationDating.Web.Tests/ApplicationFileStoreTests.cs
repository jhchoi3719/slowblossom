using Microsoft.AspNetCore.Http;
using RotationDating.Web.Services;
using RotationDating.Web.Services.ApplicationForm;

namespace RotationDating.Web.Tests;

public class ApplicationFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sb-test-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationFileStore _store;

    public ApplicationFileStoreTests()
    {
        _store = new ApplicationFileStore(new SiteStorageOptions { RootPath = _root });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static IFormFile MakeFile(string name, long size, byte[]? header = null)
    {
        var bytes = new byte[size];
        header ??= name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? PngHeader : JpegHeader;
        if (size > 0)
            Array.Copy(header, bytes, Math.Min(header.Length, bytes.Length));
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, size, "file", name);
    }

    [Fact]
    public async Task Refuses_to_save_when_disk_is_nearly_full()
    {
        var store = new ApplicationFileStore(new SiteStorageOptions { RootPath = _root }, minFreeBytes: long.MaxValue);
        var (fileName, error) = await store.TrySaveAsync(MakeFile("a.jpg", 100));
        Assert.Equal("disk", error);
        Assert.Null(fileName);
    }

    [Fact]
    public async Task Non_image_bytes_named_jpg_are_rejected()
    {
        var (fileName, error) = await _store.TrySaveAsync(MakeFile("fake.jpg", 100, header: [0x50, 0x4B, 0x03, 0x04]));
        Assert.Equal("type", error);
        Assert.Null(fileName);
    }

    [Fact]
    public async Task Png_bytes_named_jpg_are_accepted_with_png_extension()
    {
        var (fileName, error) = await _store.TrySaveAsync(MakeFile("shot.jpg", 100, header: PngHeader));
        Assert.Null(error);
        Assert.EndsWith(".png", fileName);
    }

    [Fact]
    public async Task Jpeg_without_extension_is_accepted()
    {
        var (fileName, error) = await _store.TrySaveAsync(MakeFile("image", 100, header: JpegHeader));
        Assert.Null(error);
        Assert.EndsWith(".jpg", fileName);
    }

    [Fact]
    public async Task Saved_file_is_outside_public_uploads_and_resolvable()
    {
        var (fileName, error) = await _store.TrySaveAsync(MakeFile("me.JPG", 1024));
        Assert.Null(error);
        Assert.EndsWith(".jpg", fileName);
        var path = _store.GetPath(fileName);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.DoesNotContain(Path.Combine(_root, "uploads"), path);
    }

    [Fact]
    public async Task Empty_or_missing_file_is_not_an_error()
    {
        Assert.Equal((null, null), await _store.TrySaveAsync(null));
        Assert.Equal((null, null), await _store.TrySaveAsync(MakeFile("a.jpg", 0)));
    }

    [Fact]
    public async Task Over_10mb_is_rejected()
    {
        var (_, error) = await _store.TrySaveAsync(MakeFile("big.jpg", ApplicationFileStore.MaxBytes + 1));
        Assert.Equal("size", error);
    }

    [Fact]
    public async Task Gif_is_rejected()
    {
        var (_, error) = await _store.TrySaveAsync(MakeFile("a.gif", 10, header: "GIF89a"u8.ToArray()));
        Assert.Equal("type", error);
    }

    [Theory]
    [InlineData("../x.jpg")]
    [InlineData(@"..\x.jpg")]
    [InlineData("sub/x.jpg")]
    [InlineData(null)]
    [InlineData("missing.jpg")]
    public void GetPath_rejects_traversal_and_missing(string? name)
    {
        Assert.Null(_store.GetPath(name));
    }

    [Fact]
    public async Task DeleteFilesOf_removes_all_three_documents()
    {
        var app = new RotationDating.Web.Models.ParticipantApplication
        {
            PhotoFile = (await _store.TrySaveAsync(MakeFile("a.jpg", 10))).FileName,
            EmploymentProofFile = (await _store.TrySaveAsync(MakeFile("b.jpg", 10))).FileName,
            MarriageCertFile = (await _store.TrySaveAsync(MakeFile("c.png", 10))).FileName
        };

        _store.DeleteFilesOf(app);

        Assert.Null(_store.GetPath(app.PhotoFile));
        Assert.Null(_store.GetPath(app.EmploymentProofFile));
        Assert.Null(_store.GetPath(app.MarriageCertFile));
    }

    [Theory]
    [InlineData("photo", "PhotoFile")]
    [InlineData("employment", "EmploymentProofFile")]
    [InlineData("marriage", "MarriageCertFile")]
    [InlineData("other", null)]
    public void FileNameOf_maps_kind_to_column(string kind, string? property)
    {
        var app = new RotationDating.Web.Models.ParticipantApplication
        {
            PhotoFile = "PhotoFile",
            EmploymentProofFile = "EmploymentProofFile",
            MarriageCertFile = "MarriageCertFile"
        };
        Assert.Equal(property, ApplicationFileStore.FileNameOf(app, kind));
    }

    [Fact]
    public async Task Delete_removes_file()
    {
        var (fileName, _) = await _store.TrySaveAsync(MakeFile("a.png", 10));
        _store.Delete(fileName);
        Assert.Null(_store.GetPath(fileName));
        _store.Delete("nope.jpg");
        _store.Delete(null);
    }
}
