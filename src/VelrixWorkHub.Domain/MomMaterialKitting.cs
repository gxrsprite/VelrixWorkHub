namespace VelrixWorkHub.Domain;

public enum MomMaterialRequirementStatus { Pending, PartiallyIssued, Issued }
public enum MomMaterialMovementKind { Issue, Return }

/// <summary>
/// 工单按已发布制造版本生成的用料快照。需求数量冻结，领料和退料只改变动作累计量。
/// </summary>
public sealed class MomWorkOrderMaterialRequirement
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid WorkOrderId { get; private set; }
    public Guid ManufacturingVersionId { get; private set; }
    public int LineNo { get; private set; }
    public Guid ComponentProductId { get; private set; }
    public decimal RequiredQuantity { get; private set; }
    public decimal IssuedQuantity { get; private set; }
    public decimal ReturnedQuantity { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public decimal NetIssuedQuantity => Math.Max(0, IssuedQuantity - ReturnedQuantity);
    public decimal RemainingQuantity => Math.Max(0, RequiredQuantity - NetIssuedQuantity);
    public MomMaterialRequirementStatus Status => NetIssuedQuantity <= 0
        ? MomMaterialRequirementStatus.Pending
        : RemainingQuantity > 0 ? MomMaterialRequirementStatus.PartiallyIssued : MomMaterialRequirementStatus.Issued;

    public MomWorkOrderMaterialRequirement(Guid workOrderId, Guid manufacturingVersionId, int lineNo,
        Guid componentProductId, decimal requiredQuantity, string? otherInfo = null)
    {
        Validate(workOrderId, manufacturingVersionId, lineNo, componentProductId, requiredQuantity);
        WorkOrderId = workOrderId;
        ManufacturingVersionId = manufacturingVersionId;
        LineNo = lineNo;
        ComponentProductId = componentProductId;
        RequiredQuantity = Round(requiredQuantity);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomWorkOrderMaterialRequirement Restore(Guid id, Guid workOrderId, Guid manufacturingVersionId,
        int lineNo, Guid componentProductId, decimal requiredQuantity, decimal issuedQuantity,
        decimal returnedQuantity, string? otherInfo)
    {
        var item = new MomWorkOrderMaterialRequirement(workOrderId, manufacturingVersionId, lineNo, componentProductId, requiredQuantity, otherInfo)
        {
            Id = id
        };
        if (issuedQuantity < 0 || returnedQuantity < 0 || returnedQuantity > issuedQuantity)
            throw new InvalidOperationException("工单用料领退料累计量无效。");
        if (issuedQuantity - returnedQuantity > item.RequiredQuantity)
            throw new InvalidOperationException("工单用料净领料量不能超过需求数量。");
        item.IssuedQuantity = Round(issuedQuantity);
        item.ReturnedQuantity = Round(returnedQuantity);
        return item;
    }

    public void Issue(decimal quantity)
    {
        quantity = Round(quantity);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "领料数量必须大于零。");
        if (quantity > RemainingQuantity) throw new InvalidOperationException("领料数量不能超过工单用料剩余需求。");
        IssuedQuantity = Round(IssuedQuantity + quantity);
    }

    public void Return(decimal quantity)
    {
        quantity = Round(quantity);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "退料数量必须大于零。");
        if (quantity > NetIssuedQuantity) throw new InvalidOperationException("退料数量不能超过工单用料净领料量。");
        ReturnedQuantity = Round(ReturnedQuantity + quantity);
    }

    /// <summary>事务回滚时恢复内存聚合快照，数据库事务仍由宿主负责回滚。</summary>
    public void RestoreMovementTotals(decimal issuedQuantity, decimal returnedQuantity)
    {
        if (issuedQuantity < 0 || returnedQuantity < 0 || returnedQuantity > issuedQuantity || issuedQuantity - returnedQuantity > RequiredQuantity)
            throw new InvalidOperationException("工单用料领退料累计量无效。");
        IssuedQuantity = Round(issuedQuantity);
        ReturnedQuantity = Round(returnedQuantity);
    }

    private static void Validate(Guid workOrderId, Guid manufacturingVersionId, int lineNo, Guid componentProductId, decimal requiredQuantity)
    {
        if (workOrderId == Guid.Empty) throw new ArgumentException("用料必须绑定制造工单。", nameof(workOrderId));
        if (manufacturingVersionId == Guid.Empty) throw new ArgumentException("用料必须绑定制造版本。", nameof(manufacturingVersionId));
        if (lineNo <= 0) throw new ArgumentOutOfRangeException(nameof(lineNo), "用料行号必须大于零。");
        if (componentProductId == Guid.Empty) throw new ArgumentException("用料必须绑定组件商品。", nameof(componentProductId));
        if (requiredQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(requiredQuantity), "工单用料需求数量必须大于零。");
    }

    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}

/// <summary>
/// 工单物料动作的 MOM 审计投影。实际库存余额仍由 ERP InventoryTransaction 权威维护。
/// </summary>
public sealed class MomMaterialMovement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RequirementId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? LocationId { get; private set; }
    public MomMaterialMovementKind Kind { get; private set; }
    public decimal Quantity { get; private set; }
    public string SourceNo { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string? Notes { get; private set; }
    public string? BatchNo { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string? SerialNo { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomMaterialMovement(Guid requirementId, Guid workOrderId, Guid productId, Guid warehouseId,
        MomMaterialMovementKind kind, decimal quantity, string sourceNo, DateOnly occurredOn,
        Guid? locationId = null, string? notes = null, string? batchNo = null, DateOnly? expiryDate = null,
        string? serialNo = null, string? otherInfo = null, Guid? id = null)
    {
        if (requirementId == Guid.Empty) throw new ArgumentException("物料动作必须绑定用料行。", nameof(requirementId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("物料动作必须绑定制造工单。", nameof(workOrderId));
        if (productId == Guid.Empty) throw new ArgumentException("物料动作必须绑定商品。", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("物料动作必须绑定仓库。", nameof(warehouseId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "物料动作数量必须大于零。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("物料动作流水号不能为空。", nameof(sourceNo));
        if (expiryDate is DateOnly expiry && expiry < occurredOn) throw new ArgumentException("保质期不能早于物料动作日期。", nameof(expiryDate));
        Id = id ?? Guid.CreateVersion7();
        RequirementId = requirementId;
        WorkOrderId = workOrderId;
        ProductId = productId;
        WarehouseId = warehouseId;
        LocationId = locationId;
        Kind = kind;
        Quantity = decimal.Round(quantity, 6, MidpointRounding.AwayFromZero);
        SourceNo = sourceNo.Trim();
        OccurredOn = occurredOn;
        Notes = Clean(notes);
        BatchNo = Clean(batchNo);
        ExpiryDate = expiryDate;
        SerialNo = Clean(serialNo);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid workOrderId, MomMaterialMovementKind kind, Guid movementId)
        => $"{(kind == MomMaterialMovementKind.Issue ? "MOI" : "MOR")}-{workOrderId:N}-{movementId:N}";

    public static MomMaterialMovement Restore(Guid id, Guid requirementId, Guid workOrderId, Guid productId,
        Guid warehouseId, Guid? locationId, MomMaterialMovementKind kind, decimal quantity, string sourceNo,
        DateOnly occurredOn, string? notes, string? batchNo, DateOnly? expiryDate, string? serialNo, string? otherInfo)
        => new(requirementId, workOrderId, productId, warehouseId, kind, quantity, sourceNo, occurredOn,
            locationId, notes, batchNo, expiryDate, serialNo, otherInfo, id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
