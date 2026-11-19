namespace MyMusic.Services;

/// <summary>Maps short, friendly environment variables (documented in README) to configuration keys.</summary>
public static class EnvironmentConfig
{
    private static readonly (string Env, string Key)[] Map =
    [
        ("STORAGE_PROVIDER", "Storage:Provider"),
        ("STORAGE_LOCAL_PATH", "Storage:LocalPath"),
        ("S3_ENDPOINT", "Storage:S3Endpoint"),
        ("S3_BUCKET", "Storage:S3Bucket"),
        ("S3_ACCESS_KEY", "Storage:S3AccessKey"),
        ("S3_SECRET_KEY", "Storage:S3SecretKey"),
        ("S3_REGION", "Storage:S3Region"),
        ("S3_FORCE_PATH_STYLE", "Storage:S3ForcePathStyle"),
        ("MAX_UPLOAD_MB", "Upload:MaxAudioMb"),
        ("MAX_COVER_MB", "Upload:MaxCoverMb"),
        ("ALLOW_REGISTRATION", "Auth:AllowRegistration"),
        ("SQLITE_PATH", "Database:SqlitePath"),
    ];

    public static void Apply(ConfigurationManager config)
    {
        var values = new Dictionary<string, string?>();
        foreach (var (env, key) in Map)
        {
            var v = Environment.GetEnvironmentVariable(env);
            if (!string.IsNullOrWhiteSpace(v)) values[key] = v.Trim();
        }
        if (values.Count > 0) config.AddInMemoryCollection(values);
    }
}
