namespace ZIONShop.Products.Application.Options;

public class ImageStorageOptions
{
    public const string SectionName = "ImageStorage";
    public const string UploadsFolderName = "uploads";

    public string? PublicBaseUrl { get; set; }

    // Empty = ContentRoot/App_Data/uploads. Absolute paths honored as-is.
    public string? LocalRootPath { get; set; }
}
