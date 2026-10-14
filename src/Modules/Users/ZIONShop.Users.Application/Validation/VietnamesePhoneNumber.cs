using System.Text.RegularExpressions;

namespace ZIONShop.Users.Application.Validation;

public static class VietnamesePhoneNumber
{
    private static readonly Regex Pattern = new(@"^(0|\+84)(3|5|7|8|9|2)[0-9]{8}$", RegexOptions.Compiled);

    public static bool IsValid(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return false;
        var normalized = phoneNumber.Replace(" ", "").Replace(".", "").Replace("-", "");
        return Pattern.IsMatch(normalized);
    }
}
