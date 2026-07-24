using FreeSql;
using System.Text.Json;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Notifications;

public sealed class FreeSqlNotificationFailureRepository(IFreeSql fsql) : INotificationFailureRepository
{
    public IReadOnlyList<PersistedNotificationFailure> ListPending(int take)
    {
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        var records = fsql.Select<NotificationFailureRecord>()
            .Where(x => x.Status == NotificationFailureStatus.Pending)
            .OrderBy(x => x.OccurredAt)
            .Take(take)
            .ToList();
        var result = new List<PersistedNotificationFailure>(records.Count);
        foreach (var record in records)
        {
            var item = ToApplication(record);
            if (item is not null) result.Add(item);
            else MarkInvalidPayload(record.Id);
        }
        return result;
    }

    public PersistedNotificationFailure? FindPending(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("通知失败记录标识不能为空。", nameof(id));
        var record = fsql.Select<NotificationFailureRecord>()
            .Where(x => x.Id == id && x.Status == NotificationFailureStatus.Pending && x.PayloadJson != null)
            .ToList()
            .FirstOrDefault();
        if (record is null) return null;
        var item = ToApplication(record);
        if (item is null) MarkInvalidPayload(record.Id);
        return item;
    }

    public bool TryClaim(Guid id, DateTime attemptedAt, TimeSpan lease)
    {
        if (id == Guid.Empty) throw new ArgumentException("通知失败记录标识不能为空。", nameof(id));
        if (lease <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lease));
        var cutoff = attemptedAt - lease;
        var rows = fsql.Update<NotificationFailureRecord>()
            .SetRaw("\"RetryCount\" = \"RetryCount\" + 1")
            .Set(x => x.LastRetryAt, attemptedAt)
            .Where(x => x.Id == id && x.Status == NotificationFailureStatus.Pending && (x.LastRetryAt == null || x.LastRetryAt < cutoff))
            .ExecuteAffrows();
        return rows == 1;
    }

    public void MarkRetryFailed(Guid id, string error, DateTime attemptedAt)
    {
        var rows = fsql.Update<NotificationFailureRecord>()
            .Set(x => x.Error, error.Length <= 2000 ? error : error[..2000])
            .Set(x => x.LastRetryAt, attemptedAt)
            .Where(x => x.Id == id && x.Status == NotificationFailureStatus.Pending)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("通知失败记录不存在或已处理。");
    }

    public void MarkResolved(Guid id, DateTime resolvedAt)
    {
        var rows = fsql.Update<NotificationFailureRecord>()
            .Set(x => x.Status, NotificationFailureStatus.Resolved)
            .Set(x => x.LastRetryAt, resolvedAt)
            .Set(x => x.ResolvedAt, resolvedAt)
            .Where(x => x.Id == id && x.Status == NotificationFailureStatus.Pending)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("通知失败记录不存在或已处理。");
    }

    private static PersistedNotificationFailure? ToApplication(NotificationFailureRecord record)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<NotificationDeliveryPayload>(record.PayloadJson!, JsonSerializationDefaults.CreateWeb());
            return payload is null ? null : new(record.Id, record.Operation, record.Recipient, record.DedupeKey, record.Error, record.OccurredAt, payload, record.Status, record.RetryCount, record.LastRetryAt, record.ResolvedAt);
        }
        catch (JsonException)
        {
            // 历史审计记录即使负载损坏也不应阻断其余待重试项。
            return null;
        }
    }

    private void MarkInvalidPayload(Guid id)
        => fsql.Update<NotificationFailureRecord>()
            .Set(x => x.Status, NotificationFailureStatus.InvalidPayload)
            .Set(x => x.Error, "通知失败记录缺少或包含无效重放负载，不能自动重试。")
            .Where(x => x.Id == id && x.Status == NotificationFailureStatus.Pending)
            .ExecuteAffrows();
}
