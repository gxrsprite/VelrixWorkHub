using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Notifications;

public sealed record PersistedExternalNotificationDelivery(
    Guid Id,
    ExternalNotificationMessage Message,
    ExternalNotificationDeliveryStatus Status,
    int RetryCount,
    DateTime? LastAttemptAt,
    DateTime? DeliveredAt,
    string? LastError,
    DateTime? NextAttemptAt = null);

public interface IExternalNotificationOutboxRepository
{
    bool TryAdd(ExternalNotificationMessage message);
    IReadOnlyList<PersistedExternalNotificationDelivery> ListPending(int take, DateTime? dueAt = null);
    bool TryClaim(Guid id, DateTime attemptedAt, TimeSpan lease);
    void MarkDelivered(Guid id, DateTime deliveredAt);
    void MarkFailed(Guid id, string error, DateTime attemptedAt);
    void MarkFailed(Guid id, string error, DateTime attemptedAt, DateTime nextAttemptAt) => MarkFailed(id, error, attemptedAt);
}

public sealed record ExternalNotificationOutboxDeliverySummary(int CandidateCount, int DeliveredCount, int FailedCount, int SkippedCount);
public sealed record ExternalNotificationOutboxSummary(int PendingCount, int DeferredCount, int FailedAttemptCount, int MaxRetryCount);
public sealed record ExternalNotificationOutboxChannelSummary(ExternalNotificationChannel Channel, int PendingCount, int DeferredCount, int FailedAttemptCount, int MaxRetryCount);

/// <summary>
/// 站外通知的持久化投递边界。入队与第三方网络调用分离，Provider 必须以 Message.DedupeKey 实现幂等。
/// </summary>
public sealed class ExternalNotificationOutboxService(
    IExternalNotificationOutboxRepository repository,
    IExternalNotificationRecipientResolver recipients,
    IEnumerable<IExternalNotificationChannelProvider> providers)
{
    private static readonly TimeSpan RetryLease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1),
        TimeSpan.FromHours(2), TimeSpan.FromHours(4), TimeSpan.FromHours(8), TimeSpan.FromHours(12)
    ];
    private readonly IReadOnlyDictionary<ExternalNotificationChannel, IExternalNotificationChannelProvider> providersByChannel = providers
        .GroupBy(provider => provider.Channel)
        .ToDictionary(group => group.Key, group => group.Single());

    public int Enqueue(WorkNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var queued = 0;
        foreach (var recipient in recipients.Resolve(notification)
                     .GroupBy(item => (item.Channel, Address: RecipientKey(item)))
                     .Select(group => group.First()))
        {
            if (repository.TryAdd(ExternalNotificationMessage.From(notification, recipient))) queued++;
        }
        return queued;
    }

    public ExternalNotificationOutboxSummary InspectPending(int take = 500, DateTime? asOf = null)
    {
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        var pending = repository.ListPending(take);
        var inspectedAt = asOf ?? DateTime.Now;
        return new(
            pending.Count,
            pending.Count(item => item.NextAttemptAt is not null && item.NextAttemptAt > inspectedAt),
            pending.Count(item => !string.IsNullOrWhiteSpace(item.LastError)),
            pending.Select(item => item.RetryCount).DefaultIfEmpty(0).Max());
    }

    public IReadOnlyList<ExternalNotificationOutboxChannelSummary> InspectChannels(int take = 500, DateTime? asOf = null)
    {
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        var inspectedAt = asOf ?? DateTime.Now;
        var pending = repository.ListPending(take);
        return Enum.GetValues<ExternalNotificationChannel>()
            .Select(channel =>
            {
                var items = pending.Where(item => item.Message.Channel == channel).ToArray();
                return new ExternalNotificationOutboxChannelSummary(
                    channel,
                    items.Length,
                    items.Count(item => item.NextAttemptAt is not null && item.NextAttemptAt > inspectedAt),
                    items.Count(item => !string.IsNullOrWhiteSpace(item.LastError)),
                    items.Select(item => item.RetryCount).DefaultIfEmpty(0).Max());
            })
            .ToArray();
    }

    public async Task<ExternalNotificationOutboxDeliverySummary> DeliverPendingAsync(int take = 50, DateTime? attemptedAt = null, CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        var attempted = attemptedAt ?? DateTime.Now;
        var pending = repository.ListPending(take, attempted);
        var delivered = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!providersByChannel.TryGetValue(item.Message.Channel, out var provider))
            {
                skipped++;
                continue;
            }
            if (!repository.TryClaim(item.Id, attempted, RetryLease))
            {
                skipped++;
                continue;
            }

            try
            {
                await provider.SendAsync(item.Message, cancellationToken);
                repository.MarkDelivered(item.Id, attempted);
                delivered++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                try { repository.MarkFailed(item.Id, TrimError(exception.Message), attempted, attempted.Add(GetRetryDelay(item.RetryCount + 1))); }
                catch { /* 状态更新失败不能中断同批其他外部通知。 */ }
                failed++;
            }
        }

        return new ExternalNotificationOutboxDeliverySummary(pending.Count, delivered, failed, skipped);
    }

    private static string RecipientKey(ExternalNotificationRecipient recipient) => recipient.Channel == ExternalNotificationChannel.Email ? recipient.Address.ToUpperInvariant() : recipient.Address;
    private static string TrimError(string error) => string.IsNullOrWhiteSpace(error) ? "外部通知渠道未提供错误信息。" : error.Length <= 2000 ? error : error[..2000];
    private static TimeSpan GetRetryDelay(int retryCount) => RetryDelays[Math.Clamp(retryCount, 1, RetryDelays.Length) - 1];
}
