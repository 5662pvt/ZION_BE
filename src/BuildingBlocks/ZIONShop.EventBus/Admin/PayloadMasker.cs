using System.Text;
using System.Text.Json;

namespace ZIONShop.EventBus.Admin;

/// <summary>
/// Masks sensitive values before an outbox/DLQ payload is shown on an admin screen. Shared by
/// outbox message details and DLQ peek so both mask the exact same set of keys the exact same way.
/// A key is sensitive if it CONTAINS (case-insensitive) one of <see cref="SensitiveKeyFragments"/> --
/// matching "email" also catches "customerEmail", "RecipientEmail", etc.
/// </summary>
public static class PayloadMasker
{
    private const string Mask = "***";
    private const int NonJsonTruncateLength = 2048; // 2 KB cap for a payload that isn't valid JSON.

    private static readonly string[] SensitiveKeyFragments =
    {
        "email", "phone", "recipientname", "streetaddress", "address", "password", "token", "secret", "card"
    };

    /// <summary>Pretty-prints <paramref name="json"/> with every sensitive value replaced by "***". A
    /// payload that isn't valid JSON is returned truncated instead of thrown on.</summary>
    public static string MaskJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteMasked(document.RootElement, writer, propertyName: null);
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return Truncate(json, NonJsonTruncateLength);
        }
    }

    private static void WriteMasked(JsonElement element, Utf8JsonWriter writer, string? propertyName)
    {
        if (propertyName is not null && IsSensitiveKey(propertyName))
        {
            writer.WriteStringValue(Mask);
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteMasked(property.Value, writer, property.Name);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteMasked(item, writer, propertyName: null);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        foreach (var fragment in SensitiveKeyFragments)
        {
            if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
