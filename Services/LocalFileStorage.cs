using Microsoft.Extensions.Options;

namespace MyMusic.Services;

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options, IWebHostEnvironment env)
    {
        var path = options.Value.LocalPath;
        _root = Path.GetFullPath(path, env.ContentRootPath);
        Directory.CreateDirectory(_root);
    }

    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage key.");
        return full;
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(fs, ct);
    }

    public Task<StoredFileInfo?> GetInfoAsync(string key, CancellationToken ct)
    {
        var info = new FileInfo(Resolve(key));
        return Task.FromResult(info.Exists ? new StoredFileInfo(info.Length) : null);
    }

    public Task<Stream> OpenReadAsync(string key, long offset, long length, CancellationToken ct)
    {
        var path = Resolve(key);
        if (!System.IO.File.Exists(path)) throw new FileNotFoundException("Storage object not found.", key);
        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        if (offset > 0) fs.Seek(offset, SeekOrigin.Begin);
        return Task.FromResult<Stream>(fs);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        return Task.CompletedTask;
    }
}
