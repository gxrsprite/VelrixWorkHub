using FreeSql;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Notifications;

public sealed class FreeSqlNotificationRepository(IFreeSql fsql) : INotificationRepository
{
    public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false)
    {
        var query = fsql.Select<NotificationRecord>().Where(x => x.Recipient == recipient.Trim());
        if (unreadOnly) query = query.Where(x => x.ReadAt == null);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public NotificationPage ListPage(string recipient, int pageIndex, int pageSize, bool unreadOnly = false)
    {
        var query = fsql.Select<NotificationRecord>().Where(x => x.Recipient == recipient.Trim());
        if (unreadOnly) query = query.Where(x => x.ReadAt == null);

        var total = checked((int)query.Count());
        var pageCount = total == 0 ? 0 : (total + pageSize - 1) / pageSize;
        var effectivePage = pageCount == 0 ? 1 : Math.Min(pageIndex, pageCount);
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((effectivePage - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .Select(ToDomain)
            .ToArray();
        return new(items, total, effectivePage, pageSize, pageCount);
    }

    public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey)
        => fsql.Select<NotificationRecord>().Where(x => x.Recipient == recipient.Trim() && x.DedupeKey == dedupeKey.Trim()).ToList().Select(ToDomain).FirstOrDefault();

    public void Add(WorkNotification notification) => fsql.Insert(ToRecord(notification)).ExecuteAffrows();

    public bool TryAdd(WorkNotification notification)
    {
        var parameters = new
        {
            Id = notification.Id,
            Recipient = notification.Recipient,
            Kind = notification.Kind.ToString(),
            Title = notification.Title,
            Content = notification.Content,
            Href = notification.Href,
            DedupeKey = notification.DedupeKey,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
        var affected = fsql.Ado.DataType switch
        {
            DataType.PostgreSQL => fsql.Ado.ExecuteNonQuery("""
                INSERT INTO "OaNotification" ("Id", "Recipient", "Kind", "Title", "Content", "Href", "DedupeKey", "CreatedAt", "ReadAt")
                VALUES (@Id, @Recipient, @Kind, @Title, @Content, @Href, @DedupeKey, @CreatedAt, @ReadAt)
                ON CONFLICT ("Recipient", "DedupeKey") DO NOTHING;
                """, parameters),
            DataType.Sqlite => fsql.InsertOrUpdate<NotificationRecord>()
                .SetSource(ToRecord(notification))
                .IfExistsDoNothing()
                .ExecuteAffrows(),
            DataType.SqlServer => fsql.Ado.ExecuteNonQuery("""
                MERGE [OaNotification] WITH (HOLDLOCK) AS target
                USING (VALUES (@Id, @Recipient, @Kind, @Title, @Content, @Href, @DedupeKey, @CreatedAt, @ReadAt))
                    AS source ([Id], [Recipient], [Kind], [Title], [Content], [Href], [DedupeKey], [CreatedAt], [ReadAt])
                ON target.[Recipient] = source.[Recipient] AND target.[DedupeKey] = source.[DedupeKey]
                WHEN NOT MATCHED THEN
                    INSERT ([Id], [Recipient], [Kind], [Title], [Content], [Href], [DedupeKey], [CreatedAt], [ReadAt])
                    VALUES (source.[Id], source.[Recipient], source.[Kind], source.[Title], source.[Content], source.[Href], source.[DedupeKey], source.[CreatedAt], source.[ReadAt]);
                """, parameters),
            _ => throw new NotSupportedException($"通知 TryAdd 暂不支持数据库类型：{fsql.Ado.DataType}")
        };
        return affected == 1;
    }

    public void Update(WorkNotification notification)
    {
        var rows = fsql.Update<NotificationRecord>().Set(x => x.ReadAt, notification.ReadAt).Where(x => x.Id == notification.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("通知不存在或已被删除。");
    }

    public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds)
    {
        if (notificationIds.Count == 0) return 0;
        var ids = notificationIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        return ids.Length == 0
            ? 0
            : fsql.Delete<NotificationRecord>()
                .Where(x => x.Recipient == recipient.Trim() && ids.Contains(x.Id))
                .ExecuteAffrows();
    }

    public int Count(string recipient, bool unreadOnly = false)
    {
        var query = fsql.Select<NotificationRecord>().Where(x => x.Recipient == recipient.Trim());
        if (unreadOnly) query = query.Where(x => x.ReadAt == null);
        return checked((int)query.Count());
    }

    private static WorkNotification ToDomain(NotificationRecord x) => WorkNotification.Rehydrate(x.Id, x.Recipient, x.Kind, x.Title, x.Content, x.Href, x.DedupeKey, x.CreatedAt, x.ReadAt);

    private static NotificationRecord ToRecord(WorkNotification x) => new()
    {
        Id = x.Id, Recipient = x.Recipient, Kind = x.Kind, Title = x.Title, Content = x.Content,
        Href = x.Href, DedupeKey = x.DedupeKey, CreatedAt = x.CreatedAt, ReadAt = x.ReadAt
    };
}
