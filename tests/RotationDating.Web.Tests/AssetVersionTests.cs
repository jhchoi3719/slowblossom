using RotationDating.Web.Services;

namespace RotationDating.Web.Tests;

public class AssetVersionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sb-assets-" + Guid.NewGuid().ToString("N"));

    public AssetVersionTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Url_changes_when_file_content_changes()
    {
        File.WriteAllText(Path.Combine(_root, "site.js"), "old");
        var before = new AssetVersion(_root).Url("site.js");

        File.WriteAllText(Path.Combine(_root, "site.js"), "new");
        var after = new AssetVersion(_root).Url("site.js");

        Assert.StartsWith("site.js?v=", before);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Url_is_stable_for_same_content()
    {
        File.WriteAllText(Path.Combine(_root, "app.css"), "body{}");
        var versions = new AssetVersion(_root);
        Assert.Equal(versions.Url("app.css"), versions.Url("app.css"));
    }

    [Fact]
    public void Missing_file_returns_plain_path()
    {
        Assert.Equal("nope.js", new AssetVersion(_root).Url("nope.js"));
    }
}
