namespace VelrixWorkHub.Domain;

public enum MomQualityInspectionType { Iqc, Ipqc, Fqc, Sqc }
public enum MomQualityInspectionStatus { Pending, Passed, Failed, Cancelled }

/// <summary>
/// MOM 质量检验记录。首版统一保存 IQC/IPQC/FQC/SQC 类型，IPQC 可绑定工序并作为工序完工门禁。
/// </summary>
public sealed class MomQualityInspection
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid WorkOrderId { get; private set; }
    public Guid? OperationId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? StandardId { get; private set; }
    public string? StandardCode { get; private set; }
    public string? StandardVersion { get; private set; }
    public string StandardSnapshotJson { get; private set; } = "{}";
    public MomQualityInspectionType InspectionType { get; private set; }
    public string InspectionNo { get; private set; } = string.Empty;
    public string? BatchNo { get; private set; }
    public string? SerialNo { get; private set; }
    public decimal SampleQuantity { get; private set; }
    public decimal AcceptedQuantity { get; private set; }
    public decimal RejectedQuantity { get; private set; }
    public MomQualityInspectionStatus Status { get; private set; }
    public string? Inspector { get; private set; }
    public DateTime? InspectedOn { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomQualityInspection(Guid workOrderId, MomQualityInspectionType inspectionType, Guid? operationId,
        Guid? productId, string? batchNo, string? serialNo, decimal sampleQuantity, DateTime createdOn,
        string? notes = null, string? otherInfo = null, Guid? id = null, string? inspectionNo = null,
        Guid? standardId = null, string? standardCode = null, string? standardVersion = null, string? standardSnapshotJson = null)
    {
        Validate(workOrderId, inspectionType, operationId, productId, sampleQuantity);
        Id = id ?? Guid.CreateVersion7();
        WorkOrderId = workOrderId;
        InspectionType = inspectionType;
        OperationId = operationId;
        ProductId = productId;
        StandardId = standardId;
        StandardCode = Clean(standardCode);
        StandardVersion = Clean(standardVersion);
        if (standardId is null && (StandardCode is not null || StandardVersion is not null || !string.IsNullOrWhiteSpace(standardSnapshotJson)))
            throw new ArgumentException("没有质量标准引用时不能保存标准快照。", nameof(standardId));
        if (standardId is not null && (StandardCode is null || StandardVersion is null))
            throw new ArgumentException("质量检验必须保存质量标准编码和版本。", nameof(standardId));
        StandardSnapshotJson = JsonObjectValue.Normalize(standardSnapshotJson, nameof(standardSnapshotJson));
        BatchNo = Clean(batchNo);
        SerialNo = Clean(serialNo);
        SampleQuantity = Round(sampleQuantity);
        CreatedOn = createdOn;
        InspectionNo = string.IsNullOrWhiteSpace(inspectionNo) ? BuildInspectionNo(Id) : inspectionNo.Trim();
        Notes = Clean(notes);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
        Status = MomQualityInspectionStatus.Pending;
    }

    public static MomQualityInspection Restore(Guid id, Guid workOrderId, MomQualityInspectionType inspectionType,
        Guid? operationId, Guid? productId, string? batchNo, string? serialNo, decimal sampleQuantity,
        decimal acceptedQuantity, decimal rejectedQuantity, MomQualityInspectionStatus status, string inspectionNo,
        string? inspector, DateTime? inspectedOn, DateTime createdOn, string? notes, string? otherInfo,
        Guid? standardId, string? standardCode, string? standardVersion, string? standardSnapshotJson)
    {
        var item = new MomQualityInspection(workOrderId, inspectionType, operationId, productId, batchNo, serialNo,
            sampleQuantity, createdOn, notes, otherInfo, id, inspectionNo, standardId, standardCode, standardVersion, standardSnapshotJson);
        item.RestoreResult(status, acceptedQuantity, rejectedQuantity, inspector, inspectedOn);
        return item;
    }

    public void RecordResult(decimal acceptedQuantity, decimal rejectedQuantity, string inspector, DateTime inspectedOn)
    {
        if (Status != MomQualityInspectionStatus.Pending) throw new InvalidOperationException("只有待检质量记录可以登记结果。");
        if (string.IsNullOrWhiteSpace(inspector)) throw new ArgumentException("质检员不能为空。", nameof(inspector));
        var accepted = Round(acceptedQuantity);
        var rejected = Round(rejectedQuantity);
        if (accepted < 0 || rejected < 0 || Round(accepted + rejected) != SampleQuantity)
            throw new InvalidOperationException("合格数量与不合格数量之和必须等于抽检数量。");
        AcceptedQuantity = accepted;
        RejectedQuantity = rejected;
        Inspector = inspector.Trim();
        InspectedOn = inspectedOn;
        Status = rejected > 0 ? MomQualityInspectionStatus.Failed : MomQualityInspectionStatus.Passed;
    }

    public void Cancel()
    {
        if (Status != MomQualityInspectionStatus.Pending) throw new InvalidOperationException("只有待检质量记录可以取消。");
        Status = MomQualityInspectionStatus.Cancelled;
    }

    public void UpdateNotes(string? notes) => Notes = Clean(notes);

    public void RestoreResult(MomQualityInspectionStatus status, decimal acceptedQuantity, decimal rejectedQuantity,
        string? inspector, DateTime? inspectedOn)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status), "质量检验状态无效。");
        var accepted = Round(acceptedQuantity);
        var rejected = Round(rejectedQuantity);
        if (accepted < 0 || rejected < 0 || accepted + rejected > SampleQuantity)
            throw new InvalidOperationException("质量检验结果数量无效。");
        if (status == MomQualityInspectionStatus.Pending && (accepted != 0 || rejected != 0 || inspector is not null || inspectedOn is not null))
            throw new InvalidOperationException("待检质量记录不能带有检验结果。");
        if (status == MomQualityInspectionStatus.Passed && (accepted + rejected != SampleQuantity || rejected != 0))
            throw new InvalidOperationException("通过的质量检验结果无效。");
        if (status == MomQualityInspectionStatus.Failed && (accepted + rejected != SampleQuantity || rejected <= 0))
            throw new InvalidOperationException("不通过的质量检验结果无效。");
        AcceptedQuantity = accepted;
        RejectedQuantity = rejected;
        Inspector = Clean(inspector);
        InspectedOn = inspectedOn;
        Status = status;
    }

    public static string BuildInspectionNo(Guid id) => $"MQI-{id:N}";

    private static void Validate(Guid workOrderId, MomQualityInspectionType inspectionType, Guid? operationId,
        Guid? productId, decimal sampleQuantity)
    {
        if (workOrderId == Guid.Empty) throw new ArgumentException("质量检验必须绑定制造工单。", nameof(workOrderId));
        if (!Enum.IsDefined(inspectionType)) throw new ArgumentOutOfRangeException(nameof(inspectionType), "质量检验类型无效。");
        if (inspectionType == MomQualityInspectionType.Ipqc && (!operationId.HasValue || operationId.Value == Guid.Empty))
            throw new ArgumentException("IPQC 质量检验必须绑定工序。", nameof(operationId));
        if (inspectionType is MomQualityInspectionType.Iqc or MomQualityInspectionType.Fqc or MomQualityInspectionType.Sqc
            && (!productId.HasValue || productId.Value == Guid.Empty))
            throw new ArgumentException("该质量检验类型必须绑定商品。", nameof(productId));
        if (sampleQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(sampleQuantity), "抽检数量必须大于零。");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
