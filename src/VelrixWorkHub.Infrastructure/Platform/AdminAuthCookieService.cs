using Microsoft.AspNetCore.DataProtection;

namespace AdminBlazor.Services;

public sealed class AdminAuthCookieService
{
    private readonly IDataProtector _protector;

    public AdminAuthCookieService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("AdminBlazor.AuthCookie.v1");
    }

    public string Protect(Guid userId, int authVersion, DateTimeOffset expiresAt)
    {
        return _protector.Protect($"{userId:N}|{authVersion}|{expiresAt.ToUnixTimeSeconds()}");
    }

    public bool TryGetSession(string? protectedValue, out AdminAuthSession session)
    {
        session = default;
        if (string.IsNullOrWhiteSpace(protectedValue))
            return false;

        try
        {
            var values = _protector.Unprotect(protectedValue).Split('|', StringSplitOptions.TrimEntries);
            if (values.Length != 3
                || !Guid.TryParseExact(values[0], "N", out var userId)
                || !int.TryParse(values[1], out var authVersion)
                || authVersion < 0
                || !long.TryParse(values[2], out var expiryTimestamp)
                || DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp) <= DateTimeOffset.UtcNow)
                return false;

            session = new AdminAuthSession(userId, authVersion);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public readonly record struct AdminAuthSession(Guid UserId, int AuthVersion);
