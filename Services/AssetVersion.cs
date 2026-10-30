using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace RotationDating.Web.Services;

// site.js?v=해시 형태로 주소를 바꿔서, 배포 후에도 휴대폰이 예전 파일을 계속 쓰는 일을 막는다.
public sealed class AssetVersion
{
    private readonly string _webRoot;
    private readonly ConcurrentDictionary<string, string> _urls = new();

    public AssetVersion(string webRoot)
    {
        _webRoot = webRoot;
    }

    public string Url(string path) => _urls.GetOrAdd(path, Compute);

    private string Compute(string path)
    {
        var file = Path.Combine(_webRoot, path);
        if (!File.Exists(file))
            return path;

        using var stream = File.OpenRead(file);
        var hash = Convert.ToHexString(SHA256.HashData(stream))[..12].ToLowerInvariant();
        return $"{path}?v={hash}";
    }
}
