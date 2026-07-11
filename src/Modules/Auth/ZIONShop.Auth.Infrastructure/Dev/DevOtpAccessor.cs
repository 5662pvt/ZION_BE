using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using ZIONShop.Auth.Application.Interfaces;

namespace ZIONShop.Auth.Infrastructure.Dev;

public class DevOtpAccessor : IDevOtpAccessor
{
    private readonly IWebHostEnvironment _env;

    public DevOtpAccessor(IWebHostEnvironment env) => _env = env;

    public string? RevealIfDevelopment(string otp) =>
        _env.IsDevelopment() ? otp : null;
}
