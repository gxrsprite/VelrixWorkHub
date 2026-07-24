using FreeSql;
using VelrixWorkHub.Application.Notifications;

namespace VelrixWorkHub.Infrastructure.Notifications;

public sealed class FreeSqlNotificationFailureAuditRepository(IFreeSql fsql) : INotificationFailureAuditRepository
{
    public IReadOnlyList<NotificationFailureAuditEntry> List(Guid? failureId = null, int take = 100)
        => fsql.Select<NotificationFailureAuditRecord>()
            .WhereIf(failureId.HasValue, x => x.FailureId == failureId!.Value)
            .OrderByDescending(x => x.OccurredAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToList()
            .Select(ToApplication)
            .ToArray();

    public void Add(NotificationFailureAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        fsql.Insert(new NotificationFailureAuditRecord
        {
            Id = entry.Id,
            FailureId = entry.FailureId,
            Action = entry.Action.Trim(),
            Actor = entry.Actor.Trim(),
            Details = entry.Details.Trim(),
            OccurredAt = entry.OccurredAt
        }).ExecuteAffrows();
    }

    private static NotificationFailureAuditEntry ToApplication(NotificationFailureAuditRecord item)
        => new(item.FailureId, item.Action, item.Actor, item.Details, item.OccurredAt) { Id = item.Id };
}
