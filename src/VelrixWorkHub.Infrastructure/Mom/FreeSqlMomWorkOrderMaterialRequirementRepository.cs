using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomWorkOrderMaterialRequirementRepository(IFreeSql fsql) : IMomWorkOrderMaterialRequirementRepository
{
    public IReadOnlyList<MomWorkOrderMaterialRequirement> List() => fsql.Select<MomWorkOrderMaterialRequirementRecord>()
        .OrderBy(x => x.WorkOrderId).OrderBy(x => x.LineNo).ToList().Select(ToDomain).ToArray();

    public void Add(MomWorkOrderMaterialRequirement item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(MomWorkOrderMaterialRequirement item)
    {
        var rows = fsql.Update<MomWorkOrderMaterialRequirementRecord>()
            .Set(x => x.IssuedQuantity, item.IssuedQuantity)
            .Set(x => x.ReturnedQuantity, item.ReturnedQuantity)
            .Where(x => x.Id == item.Id)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("工单用料不存在或已被删除。");
    }

    private static MomWorkOrderMaterialRequirement ToDomain(MomWorkOrderMaterialRequirementRecord x)
        => MomWorkOrderMaterialRequirement.Restore(x.Id, x.WorkOrderId, x.ManufacturingVersionId, x.LineNo, x.ComponentProductId, x.RequiredQuantity, x.IssuedQuantity, x.ReturnedQuantity, x.OtherInfo);

    private static MomWorkOrderMaterialRequirementRecord ToRecord(MomWorkOrderMaterialRequirement x) => new()
    {
        Id = x.Id, WorkOrderId = x.WorkOrderId, ManufacturingVersionId = x.ManufacturingVersionId, LineNo = x.LineNo,
        ComponentProductId = x.ComponentProductId, RequiredQuantity = x.RequiredQuantity, IssuedQuantity = x.IssuedQuantity,
        ReturnedQuantity = x.ReturnedQuantity, OtherInfo = x.OtherInfo
    };
}
