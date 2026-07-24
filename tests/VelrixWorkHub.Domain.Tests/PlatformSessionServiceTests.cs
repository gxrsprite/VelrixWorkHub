using AdminBlazor.Services;
using Microsoft.AspNetCore.DataProtection;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PlatformSessionServiceTests
{
    [Fact]
    public void AuthCookie_RoundTripsAndRejectsExpiredOrMalformedValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"velrix-auth-cookie-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            var provider = DataProtectionProvider.Create(new DirectoryInfo(path), builder =>
                builder.SetApplicationName("VelrixWorkHub.Tests"));
            var service = new AdminAuthCookieService(provider);
            var userId = Guid.CreateVersion7();

            var protectedValue = service.Protect(userId, 7, DateTimeOffset.UtcNow.AddMinutes(5));
            Assert.True(service.TryGetSession(protectedValue, out var session));
            Assert.Equal(userId, session.UserId);
            Assert.Equal(7, session.AuthVersion);

            var expiredValue = service.Protect(userId, 7, DateTimeOffset.UtcNow.AddMinutes(-1));
            Assert.False(service.TryGetSession(expiredValue, out _));
            Assert.False(service.TryGetSession("not-a-cookie", out _));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
