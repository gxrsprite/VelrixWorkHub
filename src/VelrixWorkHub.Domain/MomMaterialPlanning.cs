namespace VelrixWorkHub.Domain;

public enum MomMaterialPlanningRunStatus { Simulated, Confirmed, Cancelled }
public enum MomMaterialPlanningRecommendation { None, Purchase, Production }

/// <summary>
/// MRP 计算批次。确认只冻结本次输入快照，不直接改写采购、生产或库存业务单据。
/// </summary>
public sealed class MomMaterialPlanningRun
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string PlanNo { get; private set; } = string.Empty;
    public DateOnly ReferenceDate { get; private set; }
    public DateOnly HorizonDate { get; private set; }
    public MomMaterialPlanningRunStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomMaterialPlanningRun(string planNo, DateOnly referenceDate, DateOnly horizonDate, string? otherInfo = null)
    {
        Edit(planNo, referenceDate, horizonDate, otherInfo);
        Status = MomMaterialPlanningRunStatus.Simulated;
    }

    public static MomMaterialPlanningRun Restore(Guid id, string planNo, DateOnly referenceDate, DateOnly horizonDate,
        MomMaterialPlanningRunStatus status, string? otherInfo)
    {
        var item = new MomMaterialPlanningRun(planNo, referenceDate, horizonDate, otherInfo) { Id = id, Status = status };
        return item;
    }

    public void Edit(string planNo, DateOnly referenceDate, DateOnly horizonDate, string? otherInfo = null)
    {
        if (Status != MomMaterialPlanningRunStatus.Simulated) throw new InvalidOperationException("只有模拟中的 MRP 批次可以编辑。");
        if (string.IsNullOrWhiteSpace(planNo)) throw new ArgumentException("MRP 批次号不能为空。", nameof(planNo));
        if (horizonDate < referenceDate) throw new ArgumentException("计划展望日不能早于基准日。", nameof(horizonDate));
        PlanNo = planNo.Trim(); ReferenceDate = referenceDate; HorizonDate = horizonDate; OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Confirm()
    {
        if (Status != MomMaterialPlanningRunStatus.Simulated) throw new InvalidOperationException("只有模拟中的 MRP 批次可以确认。");
        Status = MomMaterialPlanningRunStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status != MomMaterialPlanningRunStatus.Simulated) throw new InvalidOperationException("只有模拟中的 MRP 批次可以取消。");
        Status = MomMaterialPlanningRunStatus.Cancelled;
    }
}

/// <summary>
/// MRP 计划批次中的商品供需快照和建议。数量为该批次计算时的冻结值。
/// </summary>
public sealed class MomMaterialPlanningLine
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid PlanningRunId { get; private set; }
    public int LineNo { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal DemandQuantity { get; private set; }
    public decimal OnHandQuantity { get; private set; }
    public decimal PurchaseInTransitQuantity { get; private set; }
    public decimal OpenWorkOrderQuantity { get; private set; }
    public decimal SupplyQuantity => OnHandQuantity + PurchaseInTransitQuantity + OpenWorkOrderQuantity;
    public decimal ShortageQuantity => Math.Max(0, DemandQuantity - SupplyQuantity);
    public MomMaterialPlanningRecommendation Recommendation { get; private set; }
    public decimal RecommendationQuantity { get; private set; }
    public Guid? ManufacturingVersionId { get; private set; }
    public string SourceSummary { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";

    public MomMaterialPlanningLine(Guid planningRunId, int lineNo, Guid productId, decimal demandQuantity,
        decimal onHandQuantity, decimal purchaseInTransitQuantity, decimal openWorkOrderQuantity,
        MomMaterialPlanningRecommendation recommendation, decimal recommendationQuantity,
        Guid? manufacturingVersionId, string sourceSummary, string? otherInfo = null)
    {
        if (planningRunId == Guid.Empty) throw new ArgumentException("MRP 明细必须绑定计划批次。", nameof(planningRunId));
        if (lineNo <= 0) throw new ArgumentOutOfRangeException(nameof(lineNo), "MRP 明细行号必须大于 0。");
        if (productId == Guid.Empty) throw new ArgumentException("MRP 明细必须绑定商品。", nameof(productId));
        if (demandQuantity < 0 || onHandQuantity < 0 || purchaseInTransitQuantity < 0 || openWorkOrderQuantity < 0) throw new ArgumentOutOfRangeException(nameof(demandQuantity), "MRP 数量不能为负数。");
        if (recommendationQuantity < 0) throw new ArgumentOutOfRangeException(nameof(recommendationQuantity), "MRP 建议数量不能为负数。");
        if (recommendation == MomMaterialPlanningRecommendation.None && recommendationQuantity != 0) throw new ArgumentException("无建议时建议数量必须为零。", nameof(recommendationQuantity));
        if (recommendation != MomMaterialPlanningRecommendation.None && recommendationQuantity == 0) throw new ArgumentException("存在采购或生产建议时建议数量必须大于零。", nameof(recommendationQuantity));
        if (string.IsNullOrWhiteSpace(sourceSummary)) throw new ArgumentException("MRP 来源摘要不能为空。", nameof(sourceSummary));
        PlanningRunId = planningRunId; LineNo = lineNo; ProductId = productId; DemandQuantity = demandQuantity; OnHandQuantity = onHandQuantity; PurchaseInTransitQuantity = purchaseInTransitQuantity; OpenWorkOrderQuantity = openWorkOrderQuantity; Recommendation = recommendation; RecommendationQuantity = recommendationQuantity; ManufacturingVersionId = manufacturingVersionId; SourceSummary = sourceSummary.Trim(); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static MomMaterialPlanningLine Restore(Guid id, Guid planningRunId, int lineNo, Guid productId, decimal demandQuantity,
        decimal onHandQuantity, decimal purchaseInTransitQuantity, decimal openWorkOrderQuantity,
        MomMaterialPlanningRecommendation recommendation, decimal recommendationQuantity,
        Guid? manufacturingVersionId, string sourceSummary, string? otherInfo)
    {
        var item = new MomMaterialPlanningLine(planningRunId, lineNo, productId, demandQuantity, onHandQuantity, purchaseInTransitQuantity, openWorkOrderQuantity, recommendation, recommendationQuantity, manufacturingVersionId, sourceSummary, otherInfo) { Id = id };
        return item;
    }
}
