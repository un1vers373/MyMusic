namespace MyMusic.Services;

public class StorageOptions
{
    public string Provider { get; set; } = "Local";
    public string LocalPath { get; set; } = "storage";
    public string? S3Endpoint { get; set; }
    public string? S3Bucket { get; set; }
    public string? S3AccessKey { get; set; }
    public string? S3SecretKey { get; set; }
    public string S3Region { get; set; } = "auto";
    public bool S3ForcePathStyle { get; set; } = true;
}

public class UploadOptions
{
    public int MaxAudioMb { get; set; } = 60;
    public int MaxCoverMb { get; set; } = 5;
}
