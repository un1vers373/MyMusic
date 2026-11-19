using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace MyMusic.Services;

/// <summary>Works with AWS S3 and S3-compatible services (Cloudflare R2, Backblaze B2, MinIO, Wasabi...).</summary>
public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.S3Bucket) || string.IsNullOrWhiteSpace(o.S3AccessKey) ||
            string.IsNullOrWhiteSpace(o.S3SecretKey))
            throw new InvalidOperationException(
                "STORAGE_PROVIDER=S3 requires S3_BUCKET, S3_ACCESS_KEY and S3_SECRET_KEY (and S3_ENDPOINT for non-AWS services).");

        _bucket = o.S3Bucket!;

        var cfg = new AmazonS3Config
        {
            ForcePathStyle = o.S3ForcePathStyle,
            // Some S3-compatible providers reject the newer default checksum behaviour of the SDK.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        if (!string.IsNullOrWhiteSpace(o.S3Endpoint))
        {
            cfg.ServiceURL = o.S3Endpoint!.TrimEnd('/');
            cfg.AuthenticationRegion = string.IsNullOrWhiteSpace(o.S3Region) ? "auto" : o.S3Region;
        }
        else
        {
            cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(
                string.IsNullOrWhiteSpace(o.S3Region) || o.S3Region == "auto" ? "us-east-1" : o.S3Region);
        }

        _s3 = new AmazonS3Client(new BasicAWSCredentials(o.S3AccessKey, o.S3SecretKey), cfg);
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, ct);
    }

    public async Task<StoredFileInfo?> GetInfoAsync(string key, CancellationToken ct)
    {
        try
        {
            var r = await _s3.GetObjectMetadataAsync(_bucket, key, ct);
            return new StoredFileInfo(r.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Stream> OpenReadAsync(string key, long offset, long length, CancellationToken ct)
    {
        try
        {
            var r = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                ByteRange = new ByteRange(offset, offset + length - 1),
            }, ct);
            return r.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("Storage object not found.", key);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await _s3.DeleteObjectAsync(_bucket, key, ct);
    }
}
