namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityInspectionGate
{
    void EnsureOperationCanComplete(Guid operationId);
    void EnsureWorkOrderCanComplete(Guid workOrderId);
}
