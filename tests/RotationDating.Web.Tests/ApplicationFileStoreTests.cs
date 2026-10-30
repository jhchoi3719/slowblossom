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

    private static IFormFile MakeFile(string name, long size)
    {
        var stream = new MemoryStream(new byte[size]);
        return new FormFile(stream, 0, size, "file", name);
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
        var (_, error) = await _store.TrySaveAsync(MakeFile("a.gif", 10));
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
