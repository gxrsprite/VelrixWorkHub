using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomQualityReceiptInspectionRepository(IFreeSql fsql) : IMomQualityReceiptInspectionRepository
{
    public IReadOnlyList<MomQualityReceiptInspection> List() => fsql.Select<MomQualityReceiptInspectionRecord>().OrderByDescending(x => x.LinkedOn).OrderByDescending(x => x.InspectionNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomQualityReceiptInspection item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomQualityReceiptInspection ToDomain(MomQualityReceiptInspectionRecord x) => MomQualityReceiptInspection.Restore(x.Id, x.PurchaseOrderId, x.InspectionId, x.ProductId, x.InspectionType, x.InspectionNo, x.BatchNo, x.LinkedOn, x.OtherInfo);
    private static MomQualityReceiptInspectionRecord ToRecord(MomQualityReceiptInspection x) => new()
    {
        Id = x.Id, PurchaseOrderId = x.PurchaseOrderId, InspectionId = x.InspectionId, ProductId = x.ProductId,
        InspectionType = x.InspectionType, InspectionNo = x.InspectionNo, BatchNo = x.BatchNo, LinkedOn = x.LinkedOn, OtherInfo = x.OtherInfo
    };
}
