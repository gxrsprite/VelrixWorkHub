using VelrixWorkHub.Domain;
using System.Text.Json;

namespace VelrixWorkHub.Application.SimpleForms;

public sealed record SimpleFormCompletionContext(Guid SubmissionId, string DefinitionCode, string EventCode, int FormVersionNumber, SimpleFormSubmissionStatus Status, string SchemaJson, string DataJson, Guid ApplicantUserId, string ApplicantName);

/// <summary>简单表单终态业务扩展点。实现必须按 SubmissionId 幂等，并只调用 Application 用例。</summary>
public interface ISimpleFormCompletionHandler
{
    string EventCode { get; }
    void Handle(SimpleFormCompletionContext context);
}

public sealed class NoopSimpleFormCompletionHandler : ISimpleFormCompletionHandler
{
    public string EventCode => "NONE";
    public void Handle(SimpleFormCompletionContext context) { }
}

public sealed record PersistedSimpleFormCompletionEvent(
    Guid Id,
    Guid SubmissionId,
    string EventCode,
    SimpleFormSubmissionStatus SubmissionStatus,
    string ContextJson,
    SimpleFormCompletionEventStatus Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAt,
    DateTime? DeliveredAt);

public interface ISimpleFormCompletionEventRepository
{
    bool TryAdd(PersistedSimpleFormCompletionEvent item);
    IReadOnlyList<PersistedSimpleFormCompletionEvent> ListPending(int take);
    void MarkDelivered(Guid id, DateTime deliveredAt);
    void MarkFailed(Guid id, string error, DateTime attemptedAt);
}

/// <summary>持久化完成事件并在提交后投递；失败留在 Outbox 供后台重试。</summary>
public sealed class SimpleFormCompletionOutboxService(
    ISimpleFormCompletionEventRepository repository,
    IEnumerable<ISimpleFormCompletionHandler> handlers)
{
    public void Enqueue(SimpleFormCompletionContext context)
    {
        var item = new PersistedSimpleFormCompletionEvent(
            Guid.CreateVersion7(), context.SubmissionId, context.EventCode + ":" + context.Status,
            context.Status, JsonSerializer.Serialize(context, JsonSerializationDefaults.CreateWeb()),
            SimpleFormCompletionEventStatus.Pending, 0, null, DateTime.Now, null);
        repository.TryAdd(item);
    }

    public int DispatchPending(int take = 50, DateTime? attemptedAt = null)
    {
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take));
        var delivered = 0;
        foreach (var item in repository.ListPending(take)) if (Dispatch(item, attemptedAt ?? DateTime.Now)) delivered++;
        return delivered;
    }

    private bool Dispatch(PersistedSimpleFormCompletionEvent item, DateTime attemptedAt)
    {
        try
        {
            var context = JsonSerializer.Deserialize<SimpleFormCompletionContext>(item.ContextJson, JsonSerializationDefaults.CreateWeb())
                ?? throw new InvalidOperationException("简单表单完成事件上下文无效。");
            var handler = handlers.SingleOrDefault(x => x.EventCode.Equals(context.EventCode, StringComparison.OrdinalIgnoreCase));
            if (handler is null) throw new InvalidOperationException($"表单完成事件“{context.EventCode}”没有唯一处理器。");
            handler.Handle(context);
            repository.MarkDelivered(item.Id, attemptedAt);
            return true;
        }
        catch (Exception ex)
        {
            repository.MarkFailed(item.Id, ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000], attemptedAt);
            return false;
        }
    }
}
