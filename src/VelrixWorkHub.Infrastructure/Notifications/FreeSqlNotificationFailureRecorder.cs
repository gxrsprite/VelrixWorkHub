using FreeSql;
using System.Text.Json;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Notifications;

/// <summary>通知主交易失败后的持久化失败记录；调用方仍负责吞掉记录器异常。</summary>
public sealed class FreeSqlNotificationFailureRecorder(IFreeSql fsql) : INotificationFailureRecorder
{
    public void Record(NotificationDeliveryFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        fsql.Insert(new NotificationFailureRecord
        {
            Id = Guid.CreateVersion7(),
            Operation = failure.Operation.Trim(),
            Recipient = failure.Recipient.Trim().ToLowerInvariant(),
            DedupeKey = failure.DedupeKey.Trim(),
            PayloadJson = failure.Payload is null ? null : JsonSerializer.Serialize(failure.Payload, JsonSerializationDefaults.CreateWeb()),
            Error = failure.Error.Length <= 2000 ? failure.Error : failure.Error[..2000],
            OccurredAt = failure.OccurredAt,
            Status = NotificationFailureStatus.Pending
        }).ExecuteAffrows();
    }
}
