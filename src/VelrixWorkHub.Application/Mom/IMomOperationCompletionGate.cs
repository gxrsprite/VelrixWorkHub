namespace VelrixWorkHub.Application.Mom;

/// <summary>工单完工前的工序状态门禁。</summary>
public interface IMomOperationCompletionGate
{
    void EnsureWorkOrderCanComplete(Guid workOrderId);
}
