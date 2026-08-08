using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomMaterialMovementRepository(IFreeSql fsql) : IMomMaterialMovementRepository
{
    public IReadOnlyList<MomMaterialMovement> List() => fsql.Select<MomMaterialMovementRecord>()
        .OrderByDescending(x => x.OccurredOn).OrderByDescending(x => x.SourceNo).ToList().Select(ToDomain).ToArray();

    public void Add(MomMaterialMovement item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomMaterialMovement ToDomain(MomMaterialMovementRecord x) => MomMaterialMovement.Restore(
        x.Id, x.RequirementId, x.WorkOrderId, x.ProductId, x.WarehouseId, x.LocationId, x.Kind, x.Quantity, x.SourceNo,
        DateOnly.FromDateTime(x.OccurredOn), x.Notes, x.BatchNo, x.ExpiryDate is null ? null : DateOnly.FromDateTime(x.ExpiryDate.Value), x.SerialNo, x.OtherInfo);

    private static MomMaterialMovementRecord ToRecord(MomMaterialMovement x) => new()
    {
        Id = x.Id, RequirementId = x.RequirementId, WorkOrderId = x.WorkOrderId, ProductId = x.ProductId, WarehouseId = x.WarehouseId,
        LocationId = x.LocationId, Kind = x.Kind, Quantity = x.Quantity, SourceNo = x.SourceNo, OccurredOn = x.OccurredOn.ToDateTime(TimeOnly.MinValue),
        Notes = x.Notes, BatchNo = x.BatchNo, ExpiryDate = x.ExpiryDate?.ToDateTime(TimeOnly.MinValue), SerialNo = x.SerialNo, OtherInfo = x.OtherInfo
    };
}
