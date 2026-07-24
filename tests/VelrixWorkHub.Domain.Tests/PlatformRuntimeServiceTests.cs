using AdminBlazor.Services;
using VelrixWorkHub.Application.Platform;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PlatformRuntimeServiceTests
{
    [Fact]
    public void ResourceLock_IsExclusiveAndCanBeRenewedByOwner()
    {
        var service = new AdminResourceLockService();

        var acquired = service.TryAcquire(" order-1 ", "alice", TimeSpan.FromMinutes(1));
        var denied = service.TryAcquire("order-1", "bob", TimeSpan.FromMinutes(1));
        var renewed = service.TryAcquire("order-1", "alice", TimeSpan.FromMinutes(2));

        Assert.True(acquired.Success);
        Assert.False(denied.Success);
        Assert.Equal("alice", denied.ExistingLock?.Owner);
        Assert.True(renewed.Success);
        Assert.InRange(renewed.Lock!.ExpiresAt - renewed.Lock.CreatedAt, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2) + TimeSpan.FromMilliseconds(10));
        Assert.False(service.Release("order-1", "bob"));
        Assert.True(service.Release("order-1", "alice"));
        Assert.Null(service.Get("order-1"));
    }

    [Fact]
    public void LoginAttemptLimiter_BlocksAtThresholdAndResetClearsState()
    {
        var service = new LoginAttemptLimiter(new LoginAttemptLimiterOptions
        {
            MaxFailures = 2,
            FailureWindow = TimeSpan.FromMinutes(1),
            BlockDuration = TimeSpan.FromMinutes(5),
        });

        Assert.False(service.RegisterFailure("alice", out _));
        Assert.True(service.RegisterFailure("alice", out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
        Assert.True(service.IsBlocked("alice", out _));

        service.Reset("alice");

        Assert.False(service.IsBlocked("alice", out _));
    }

    [Fact]
    public void WorkingDayCalendar_UsesHolidayAndWorkdayOverrides()
    {
        var service = new WorkingDayCalendar();
        var saturday = new DateTime(2026, 7, 18);

        Assert.False(service.IsWorkingDay(saturday));

        service.ApplyOverride(saturday, isWorkday: true);
        Assert.True(service.IsWorkingDay(saturday));

        service.ApplyOverride(saturday, isWorkday: false);
        Assert.False(service.IsWorkingDay(saturday));

        service.RemoveOverride(saturday);
        Assert.False(service.IsWorkingDay(saturday));
    }

    [Fact]
    public async Task NotifyChangedService_NotifiesSubscribersWithEntityAndAction()
    {
        var service = new AdminNotifyChangedService();
        Type? entityType = null;
        string? action = null;
        object? source = null;
        service.Changed += args =>
        {
            entityType = args.EntityType;
            action = args.Action;
            source = args.Source;
            return Task.CompletedTask;
        };

        var marker = new object();
        await service.NotifyAsync(typeof(string), "updated", marker);

        Assert.Equal(typeof(string), entityType);
        Assert.Equal("updated", action);
        Assert.Same(marker, source);
    }

    [Theory]
    [InlineData("../private", "uploads")]
    [InlineData("C:\\private", "uploads")]
    [InlineData("images/2026", "images/2026")]
    [InlineData("images\\2026", "images/2026")]
    public void FileStoragePathPolicy_NormalizesToRelativeSafeDirectory(string input, string expected)
    {
        Assert.Equal(expected, FileStoragePathPolicy.NormalizeUploadDirectory(input));
    }
}
