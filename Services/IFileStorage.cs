namespace MyMusic.Services;

public record StoredFileInfo(long Length);

/// <summary>Abstraction over where audio files and covers live (local disk or S3-compatible storage).</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct);

    /// <summary>Returns null when the object does not exist.</summary>
    Task<StoredFileInfo?> GetInfoAsync(string key, CancellationToken ct);

    /// <summary>Opens a stream starting at <paramref name="offset"/>. Throws FileNotFoundException if missing.</summary>
    Task<Stream> OpenReadAsync(string key, long offset, long length, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}
