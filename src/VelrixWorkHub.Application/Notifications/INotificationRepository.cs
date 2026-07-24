using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Notifications;

public interface INotificationRepository
{
    IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false);
    WorkNotification? FindByDedupeKey(string recipient, string dedupeKey);
    void Add(WorkNotification notification);
    /// <summary>以“接收人 + DedupeKey”原子插入；已存在时返回 false，不得用先查后写代替。</summary>
    bool TryAdd(WorkNotification notification);
    void Update(WorkNotification notification);
    int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds);

    int Count(string recipient, bool unreadOnly = false) => List(recipient, unreadOnly).Count;

    NotificationPage ListPage(string recipient, int pageIndex, int pageSize, bool unreadOnly = false)
    {
        var all = List(recipient, unreadOnly).OrderByDescending(x => x.CreatedAt).ToArray();
        var pageCount = all.Length == 0 ? 0 : (all.Length + pageSize - 1) / pageSize;
        var effectivePage = pageCount == 0 ? 1 : Math.Min(pageIndex, pageCount);
        return new(all.Skip((effectivePage - 1) * pageSize).Take(pageSize).ToArray(), all.Length, effectivePage, pageSize, pageCount);
    }
}
