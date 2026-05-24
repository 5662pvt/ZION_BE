using System.Security.Cryptography;
using System.Text;
using ZIONShop.Auth.Application.Interfaces;

namespace ZIONShop.Auth.Application.Services;

public class OtpHasher : IOtpHasher
{
    public string GenerateCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    public string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToHexString(bytes);
    }
}
