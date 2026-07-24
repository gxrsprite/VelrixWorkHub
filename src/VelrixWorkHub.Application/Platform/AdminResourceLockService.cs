using System.Collections.Concurrent;

namespace AdminBlazor.Services;

public sealed class AdminResourceLockService
{
    private readonly ConcurrentDictionary<string, AdminResourceLockInfo> _locks = new();

    public AdminResourceLockResult TryAcquire(string resourceKey, string owner, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            throw new ArgumentException("Resource key is required.", nameof(resourceKey));
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("Owner is required.", nameof(owner));
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be greater than zero.");

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(ttl);
        var normalizedKey = NormalizeKey(resourceKey);

        while (true)
        {
            if (!_locks.TryGetValue(normalizedKey, out var existing) || existing is null)
            {
                var created = new AdminResourceLockInfo(normalizedKey, owner, now, expiresAt);
                if (_locks.TryAdd(normalizedKey, created))
                    return AdminResourceLockResult.Acquired(created);

                continue;
            }

            if (existing.ExpiresAt <= now)
            {
                var created = new AdminResourceLockInfo(normalizedKey, owner, now, expiresAt);
                if (_locks.TryUpdate(normalizedKey, created, existing))
                    return AdminResourceLockResult.Acquired(created);

                continue;
            }

            if (string.Equals(existing.Owner, owner, StringComparison.Ordinal))
            {
                var renewed = existing with { ExpiresAt = expiresAt };
                if (_locks.TryUpdate(normalizedKey, renewed, existing))
                    return AdminResourceLockResult.Acquired(renewed);

                continue;
            }

            return AdminResourceLockResult.Denied(existing);
        }
    }

    public bool Release(string resourceKey, string owner)
    {
        var normalizedKey = NormalizeKey(resourceKey);
        if (!_locks.TryGetValue(normalizedKey, out var existing))
            return false;

        return string.Equals(existing.Owner, owner, StringComparison.Ordinal)
            && _locks.TryRemove(new KeyValuePair<string, AdminResourceLockInfo>(normalizedKey, existing));
    }

    public AdminResourceLockInfo? Get(string resourceKey)
    {
        var normalizedKey = NormalizeKey(resourceKey);
        if (!_locks.TryGetValue(normalizedKey, out var existing))
            return null;

        if (existing.ExpiresAt > DateTimeOffset.UtcNow)
            return existing;

        _locks.TryRemove(new KeyValuePair<string, AdminResourceLockInfo>(normalizedKey, existing));
        return null;
    }

    private static string NormalizeKey(string resourceKey)
    {
        return resourceKey.Trim();
    }
}

public sealed record AdminResourceLockInfo(string ResourceKey, string Owner, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public sealed record AdminResourceLockResult(bool Success, AdminResourceLockInfo? Lock, AdminResourceLockInfo? ExistingLock)
{
    public static AdminResourceLockResult Acquired(AdminResourceLockInfo info) => new(true, info, null);

    public static AdminResourceLockResult Denied(AdminResourceLockInfo existing) => new(false, null, existing);
}
