namespace VelrixWorkHub.Domain;

/// <summary>
/// 采购订单与 IQC/SQC 检验的受控关联。关联本身是配置事实，检验当前结果仍以 MomQualityInspection 为准。
/// </summary>
public sealed class MomQualityReceiptInspection
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid PurchaseOrderId { get; private set; }
    public Guid InspectionId { get; private set; }
    public Guid ProductId { get; private set; }
    public MomQualityInspectionType InspectionType { get; private set; }
    public string InspectionNo { get; private set; } = string.Empty;
    public string? BatchNo { get; private set; }
    public DateTime LinkedOn { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomQualityReceiptInspection(Guid purchaseOrderId, MomQualityInspection inspection, DateTime linkedOn,
        string? otherInfo = null, Guid? id = null)
    {
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("采购订单不能为空。", nameof(purchaseOrderId));
        ArgumentNullException.ThrowIfNull(inspection);
        if (inspection.InspectionType is not (MomQualityInspectionType.Iqc or MomQualityInspectionType.Sqc))
            throw new InvalidOperationException("采购收货质量关联只能使用 IQC 或 SQC 检验。");
        if (inspection.ProductId is not Guid productId || productId == Guid.Empty)
            throw new InvalidOperationException("采购收货质量关联必须绑定检验商品。");
        if (inspection.Status == MomQualityInspectionStatus.Cancelled)
            throw new InvalidOperationException("已取消质量检验不能关联采购收货。");
        Id = id ?? Guid.CreateVersion7(); PurchaseOrderId = purchaseOrderId; InspectionId = inspection.Id; ProductId = productId;
        InspectionType = inspection.InspectionType; InspectionNo = inspection.InspectionNo; BatchNo = Clean(inspection.BatchNo); LinkedOn = linkedOn;
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomQualityReceiptInspection Restore(Guid id, Guid purchaseOrderId, Guid inspectionId, Guid productId,
        MomQualityInspectionType inspectionType, string inspectionNo, string? batchNo, DateTime linkedOn, string? otherInfo)
    {
        var inspection = new MomQualityInspection(Guid.CreateVersion7(), inspectionType, null, productId, batchNo, null, 1, linkedOn,
            inspectionNo: inspectionNo);
        return new MomQualityReceiptInspection(purchaseOrderId, inspection, linkedOn, otherInfo, id);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
