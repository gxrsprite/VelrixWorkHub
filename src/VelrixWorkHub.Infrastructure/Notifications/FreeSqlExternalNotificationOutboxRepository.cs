using FreeSql;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Notifications;

public sealed class FreeSqlExternalNotificationOutboxRepository(IFreeSql fsql) : IExternalNotificationOutboxRepository
{
    public bool TryAdd(ExternalNotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var record = ToRecord(Guid.CreateVersion7(), message);
        var parameters = new { record.Id, record.NotificationId, Channel = record.Channel.ToString(), record.Address, Kind = record.Kind.ToString(), record.Title, record.Content, record.Href, record.DedupeKey, record.CreatedAt, Status = record.Status.ToString(), record.RetryCount, record.LastAttemptAt, record.DeliveredAt, record.LastError, record.NextAttemptAt };
        var affected = fsql.Ado.DataType switch
        {
            DataType.PostgreSQL => fsql.Ado.ExecuteNonQuery("""
                INSERT INTO "OaExternalNotificationOutbox" ("Id", "NotificationId", "Channel", "Address", "Kind", "Title", "Content", "Href", "DedupeKey", "CreatedAt", "Status", "RetryCount", "LastAttemptAt", "DeliveredAt", "LastError", "NextAttemptAt")
                VALUES (@Id, @NotificationId, @Channel, @Address, @Kind, @Title, @Content, @Href, @DedupeKey, @CreatedAt, @Status, @RetryCount, @LastAttemptAt, @DeliveredAt, @LastError, @NextAttemptAt)
                ON CONFLICT ("Channel", "Address", "DedupeKey") DO NOTHING;
                """, parameters),
            DataType.Sqlite => fsql.InsertOrUpdate<ExternalNotificationOutboxRecord>().SetSource(record).IfExistsDoNothing().ExecuteAffrows(),
            DataType.SqlServer => fsql.Ado.ExecuteNonQuery("""
                MERGE [OaExternalNotificationOutbox] WITH (HOLDLOCK) AS target
                USING (VALUES (@Id, @NotificationId, @Channel, @Address, @Kind, @Title, @Content, @Href, @DedupeKey, @CreatedAt, @Status, @RetryCount, @LastAttemptAt, @DeliveredAt, @LastError, @NextAttemptAt))
                    AS source ([Id], [NotificationId], [Channel], [Address], [Kind], [Title], [Content], [Href], [DedupeKey], [CreatedAt], [Status], [RetryCount], [LastAttemptAt], [DeliveredAt], [LastError], [NextAttemptAt])
                ON target.[Channel] = source.[Channel] AND target.[Address] = source.[Address] AND target.[DedupeKey] = source.[DedupeKey]
                WHEN NOT MATCHED THEN INSERT ([Id], [NotificationId], [Channel], [Address], [Kind], [Title], [Content], [Href], [DedupeKey], [CreatedAt], [Status], [RetryCount], [LastAttemptAt], [DeliveredAt], [LastError], [NextAttemptAt])
                    VALUES (source.[Id], source.[NotificationId], source.[Channel], source.[Address], source.[Kind], source.[Title], source.[Content], source.[Href], source.[DedupeKey], source.[CreatedAt], source.[Status], source.[RetryCount], source.[LastAttemptAt], source.[DeliveredAt], source.[LastError], source.[NextAttemptAt]);
                """, parameters),
            _ => throw new NotSupportedException($"站外通知 Outbox 暂不支持数据库类型：{fsql.Ado.DataType}")
        };
        return affected == 1;
    }

    public IReadOnlyList<PersistedExternalNotificationDelivery> ListPending(int take, DateTime? dueAt = null)
    {
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        return fsql.Select<ExternalNotificationOutboxRecord>()
            .Where(item => item.Status == ExternalNotificationDeliveryStatus.Pending && (dueAt == null || item.NextAttemptAt == null || item.NextAttemptAt <= dueAt))
            .OrderBy(item => item.CreatedAt)
            .Take(take)
            .ToList()
            .Select(ToApplication)
            .ToArray();
    }

    public bool TryClaim(Guid id, DateTime attemptedAt, TimeSpan lease)
    {
        if (id == Guid.Empty) throw new ArgumentException("站外通知 Outbox 标识不能为空。", nameof(id));
        if (lease <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lease));
        var cutoff = attemptedAt - lease;
        var rows = fsql.Update<ExternalNotificationOutboxRecord>()
            .SetRaw("\"RetryCount\" = \"RetryCount\" + 1")
            .Set(item => item.LastAttemptAt, attemptedAt)
            .Where(item => item.Id == id && item.Status == ExternalNotificationDeliveryStatus.Pending && (item.NextAttemptAt == null || item.NextAttemptAt <= attemptedAt) && (item.LastAttemptAt == null || item.LastAttemptAt <= cutoff))
            .ExecuteAffrows();
        return rows == 1;
    }

    public void MarkDelivered(Guid id, DateTime deliveredAt)
    {
        var rows = fsql.Update<ExternalNotificationOutboxRecord>()
            .Set(item => item.Status, ExternalNotificationDeliveryStatus.Delivered)
            .Set(item => item.DeliveredAt, deliveredAt)
            .Set(item => item.LastAttemptAt, deliveredAt)
            .Set(item => item.LastError, (string?)null)
            .Set(item => item.NextAttemptAt, (DateTime?)null)
            .Where(item => item.Id == id && item.Status == ExternalNotificationDeliveryStatus.Pending)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("站外通知 Outbox 不存在或已处理。");
    }

    public void MarkFailed(Guid id, string error, DateTime attemptedAt)
        => MarkFailed(id, error, attemptedAt, attemptedAt);

    public void MarkFailed(Guid id, string error, DateTime attemptedAt, DateTime nextAttemptAt)
    {
        var rows = fsql.Update<ExternalNotificationOutboxRecord>()
            .Set(item => item.LastError, error.Length <= 2000 ? error : error[..2000])
            .Set(item => item.LastAttemptAt, attemptedAt)
            .Set(item => item.NextAttemptAt, nextAttemptAt)
            .Where(item => item.Id == id && item.Status == ExternalNotificationDeliveryStatus.Pending)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("站外通知 Outbox 不存在或已处理。");
    }

    private static ExternalNotificationOutboxRecord ToRecord(Guid id, ExternalNotificationMessage message) => new()
    {
        Id = id, NotificationId = message.NotificationId, Channel = message.Channel, Address = message.Address, Kind = message.Kind,
        Title = message.Title, Content = message.Content, Href = message.Href, DedupeKey = message.DedupeKey,
        CreatedAt = message.CreatedAt, Status = ExternalNotificationDeliveryStatus.Pending
    };

    private static PersistedExternalNotificationDelivery ToApplication(ExternalNotificationOutboxRecord record) => new(
        record.Id,
        new ExternalNotificationMessage(record.NotificationId, record.Channel, record.Address, record.Kind, record.Title, record.Content, record.Href, record.DedupeKey, record.CreatedAt),
        record.Status, record.RetryCount, record.LastAttemptAt, record.DeliveredAt, record.LastError, record.NextAttemptAt);
}
