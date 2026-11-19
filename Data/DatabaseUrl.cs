namespace MyMusic.Data;


public static class DatabaseUrl
{
    public static string ToConnectionString(string value, string sslMode = "Prefer")
    {
        value = value.Trim();
        var isUrl = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
        if (!isUrl) return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var port = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={Quote(uri.Host)};Port={port};Database={Quote(database)};" +
               $"Username={Quote(user)};Password={Quote(password)};SSL Mode={sslMode};Trust Server Certificate=true";
    }

    private static string Quote(string v) => "'" + v.Replace("'", "''") + "'";
}
