using Microsoft.Extensions.Logging;
using Shop.Modules.Identity.Application;

namespace Shop.Modules.Identity.Infrastructure.Services;

/// <summary>للتطوير بس: بيطبع الكود في الـ log بدل ما يبعت SMS.</summary>
public class LogOtpSender(ILogger<LogOtpSender> logger) : IOtpSender
{
    public Task SendAsync(string phone, string code)
    {
        logger.LogInformation("[DEV OTP] {Phone} => {Code}", phone, code);
        return Task.CompletedTask;
    }
}