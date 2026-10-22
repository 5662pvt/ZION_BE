namespace ZIONShop.Products.Infrastructure.Storage;

/// <summary>
/// Detects an image's real format from its leading bytes rather than trusting the client-supplied
/// Content-Type or filename extension -- both are attacker-controlled on upload (e.g. an SVG/HTML file
/// with a spoofed "image/png" Content-Type and a ".svg"/".html" filename, served back same-origin as
/// the client's extension would dictate, becomes a stored-XSS vector against whoever opens it).
/// </summary>
public static class ImageSignature
{
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return "image/png";

        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            return "image/webp";

        return null;
    }

    public static string ExtensionFor(string detectedContentType) => detectedContentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(detectedContentType), detectedContentType, "Unknown detected image content type")
    };

    /// <summary>Folder names are used as path/URL segments for both local disk storage and the Cloudinary
    /// folder option -- restrict to a safe subset so a caller-supplied value can never escape the uploads
    /// root or inject a path separator.</summary>
    public static bool IsValidFolderName(string folder) =>
        !string.IsNullOrWhiteSpace(folder) && folder.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
}
