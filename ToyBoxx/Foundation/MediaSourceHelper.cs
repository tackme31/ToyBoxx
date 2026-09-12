using System.IO;

namespace ToyBoxx.Foundation;

public static class MediaSourceHelper
{
    /// <summary>
    /// Resolves a local file path or a stream URL (http, https, rtsp, etc.) into an openable source string.
    /// </summary>
    public static string? Resolve(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var value = input.Trim().Trim('"');
        if (File.Exists(value))
        {
            return Path.GetFullPath(value);
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            // Keep the original string so that signed URLs are not altered by normalization
            return value;
        }

        return null;
    }

    /// <summary>
    /// Gets a display title from a media source (file name without extension, or the host name for URLs without a file name).
    /// </summary>
    public static string GetTitle(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return Path.GetFileNameWithoutExtension(source);
        }

        var title = Path.GetFileNameWithoutExtension(uri.LocalPath);
        return string.IsNullOrEmpty(title) && !uri.IsFile ? uri.Host : title;
    }
}
