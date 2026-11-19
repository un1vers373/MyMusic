namespace MyMusic.Services;

public record AudioMetadata(
    string? Title, string? Artist, string? Album, int DurationSeconds, byte[]? Cover);

/// <summary>Validates MP3 files and reads their ID3 tags / duration / embedded cover.</summary>
public static class AudioInspector
{
    /// <summary>Checks the first bytes: an ID3 tag or an MPEG frame sync.</summary>
    public static bool LooksLikeMp3(Stream s)
    {
        s.Position = 0;
        var b = new byte[3];
        var n = s.Read(b, 0, 3);
        s.Position = 0;
        if (n < 3) return false;
        if (b[0] == 'I' && b[1] == 'D' && b[2] == '3') return true;
        return b[0] == 0xFF && (b[1] & 0xE0) == 0xE0;
    }

    /// <summary>Returns null if the file cannot be parsed as audio.</summary>
    public static AudioMetadata? TryRead(Stream s, string fileName)
    {
        try
        {
            s.Position = 0;
            using var f = TagLib.File.Create(new StreamAbstraction(fileName, s), "taglib/mp3", TagLib.ReadStyle.Average);
            var seconds = (int)Math.Round(f.Properties.Duration.TotalSeconds);
            if (seconds <= 0) return null;

            var tag = f.Tag;
            var pic = tag.Pictures.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover)
                      ?? tag.Pictures.FirstOrDefault();
            var cover = pic?.Data?.Data;

            return new AudioMetadata(
                tag.Title,
                tag.FirstPerformer ?? tag.FirstAlbumArtist,
                tag.Album,
                seconds,
                cover is { Length: > 0 } ? cover : null);
        }
        catch
        {
            return null;
        }
        finally
        {
            s.Position = 0;
        }
    }

    /// <summary>Detects JPEG / PNG / WebP by magic bytes. Returns (contentType, extension) or null.</summary>
    public static (string ContentType, string Ext)? SniffImage(byte[] d)
    {
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) return ("image/png", ".png");
        if (d.Length >= 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' &&
            d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return ("image/webp", ".webp");
        return null;
    }

    private sealed class StreamAbstraction(string name, Stream stream) : TagLib.File.IFileAbstraction
    {
        public string Name => name;
        public Stream ReadStream => stream;
        public Stream WriteStream => throw new NotSupportedException();
        public void CloseStream(Stream s) => s.Position = 0; // the caller owns the stream
    }
}
