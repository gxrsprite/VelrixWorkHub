using System.Collections.Concurrent;

namespace AdminBlazor.Services;

public sealed class LoginAttemptLimiterOptions
{
    public int MaxFailures { get; set; } = 5;
    public TimeSpan FailureWindow { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan BlockDuration { get; set; } = TimeSpan.FromMinutes(15);
}

public sealed class LoginAttemptLimiter
{
    private readonly LoginAttemptLimiterOptions _options;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public LoginAttemptLimiter() : this(new LoginAttemptLimiterOptions())
    {
    }

    public LoginAttemptLimiter(LoginAttemptLimiterOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxFailures, 1);
        if (options.FailureWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "FailureWindow must be greater than zero.");
        if (options.BlockDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "BlockDuration must be greater than zero.");

        _options = options;
    }

    public bool IsBlocked(string key, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!_entries.TryGetValue(key, out var entry))
            return false;

        lock (entry)
        {
            var now = DateTimeOffset.UtcNow;
            if (entry.BlockedUntil > now)
            {
                retryAfter = entry.BlockedUntil - now;
                return true;
            }

            if (entry.BlockedUntil != DateTimeOffset.MinValue)
                entry.BlockedUntil = DateTimeOffset.MinValue;
            return false;
        }
    }

    public bool RegisterFailure(string key, out TimeSpan retryAfter)
    {
        var entry = _entries.GetOrAdd(key, _ => new Entry());
        lock (entry)
        {
            var now = DateTimeOffset.UtcNow;
            entry.Failures.RemoveAll(at => now - at > _options.FailureWindow);
            entry.Failures.Add(now);
            if (entry.Failures.Count < _options.MaxFailures)
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            entry.Failures.Clear();
            entry.BlockedUntil = now.Add(_options.BlockDuration);
            retryAfter = _options.BlockDuration;
            return true;
        }
    }

    public void Reset(string key)
    {
        _entries.TryRemove(key, out _);
    }

    private sealed class Entry
    {
        public List<DateTimeOffset> Failures { get; } = new();
        public DateTimeOffset BlockedUntil { get; set; }
    }
}
