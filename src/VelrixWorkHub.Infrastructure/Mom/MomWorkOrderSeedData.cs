using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public static class MomWorkOrderSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        if (fsql.Select<MomWorkOrderRecord>().Any()) return;
        var product = fsql.Select<VelrixWorkHub.Infrastructure.Products.ProductRecord>().Where(x => x.Status == ProductStatus.Active).First();
        if (product is null) return;
        var workCenter = fsql.Select<MomWorkCenterRecord>().Where(x => x.Code == "WC-ASSEMBLY-01" && x.Status == MomMasterDataStatus.Active).First();
        var today = DateOnly.FromDateTime(DateTime.Today);
        fsql.Insert(new MomWorkOrderRecord
        {
            Id = Guid.CreateVersion7(), WorkOrderNo = "MO-20260807-001", ProductId = product.Id, WorkCenterId = workCenter?.Id,
            PlannedStart = today.ToDateTime(TimeOnly.MinValue), PlannedEnd = today.AddDays(14).ToDateTime(TimeOnly.MinValue),
            PlannedQuantity = 10, CompletedQuantity = 0, Status = MomWorkOrderStatus.Draft,
            SourceKind = MomWorkOrderSourceKind.Planning, SourceDocumentNo = "PLAN-20260807-001", OtherInfo = "{}"
        }).ExecuteAffrows();
    }
}
