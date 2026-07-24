namespace VelrixWorkHub.Application.Notifications;

public sealed record NotificationFailureAuditEntry(
    Guid FailureId,
    string Action,
    string Actor,
    string Details,
    DateTime OccurredAt)
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public interface INotificationFailureAuditRepository
{
    IReadOnlyList<NotificationFailureAuditEntry> List(Guid? failureId = null, int take = 100);
    void Add(NotificationFailureAuditEntry entry);
}
