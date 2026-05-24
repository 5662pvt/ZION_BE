namespace ZIONShop.Auth.Application.Interfaces;

public interface IOtpHasher
{
    string GenerateCode();
    string Hash(string code);
}
