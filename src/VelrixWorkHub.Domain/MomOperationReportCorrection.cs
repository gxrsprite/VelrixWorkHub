namespace VelrixWorkHub.Domain;

/// <summary>
/// 工序报工更正不可变记录。它只抵减原报工尚未更正的良品/不良品数量，不修改历史报工。
/// </summary>
public sealed class MomWorkOrderOperationReportCorrection
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ReportId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid WorkCenterId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal GoodQuantity { get; private set; }
    public decimal ScrapQuantity { get; private set; }
    public string SourceNo { get; private set; }
    public DateTime OccurredOn { get; private set; }
    public string Actor { get; private set; }
    public string? Notes { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomWorkOrderOperationReportCorrection(Guid reportId, Guid operationId, Guid workOrderId, Guid workCenterId,
        decimal goodQuantity, decimal scrapQuantity, string sourceNo, DateTime occurredOn, string actor,
        string? notes = null, string? otherInfo = null, Guid? id = null)
    {
        if (reportId == Guid.Empty) throw new ArgumentException("报工更正必须绑定原报工。", nameof(reportId));
        if (operationId == Guid.Empty) throw new ArgumentException("报工更正必须绑定工序。", nameof(operationId));
        if (workOrderId == Guid.Empty) throw new ArgumentException("报工更正必须绑定制造工单。", nameof(workOrderId));
        if (workCenterId == Guid.Empty) throw new ArgumentException("报工更正必须绑定工作中心。", nameof(workCenterId));
        var good = Round(goodQuantity); var scrap = Round(scrapQuantity); var quantity = Round(good + scrap);
        if (good < 0 || scrap < 0 || quantity <= 0) throw new ArgumentOutOfRangeException(nameof(goodQuantity), "报工更正数量必须大于零，良品和不良品更正数量不能为负数。");
        if (string.IsNullOrWhiteSpace(sourceNo)) throw new ArgumentException("报工更正流水号不能为空。", nameof(sourceNo));
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("报工更正操作者不能为空。", nameof(actor));
        Id = id ?? Guid.CreateVersion7(); ReportId = reportId; OperationId = operationId; WorkOrderId = workOrderId; WorkCenterId = workCenterId;
        Quantity = quantity; GoodQuantity = good; ScrapQuantity = scrap; SourceNo = sourceNo.Trim(); OccurredOn = occurredOn; Actor = actor.Trim();
        Notes = Clean(notes); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public static string BuildSourceNo(Guid reportId, Guid correctionId) => $"MORC-{reportId:N}-{correctionId:N}";

    public static MomWorkOrderOperationReportCorrection Restore(Guid id, Guid reportId, Guid operationId, Guid workOrderId,
        Guid workCenterId, decimal goodQuantity, decimal scrapQuantity, string sourceNo, DateTime occurredOn, string actor,
        string? notes, string? otherInfo)
        => new(reportId, operationId, workOrderId, workCenterId, goodQuantity, scrapQuantity, sourceNo, occurredOn, actor, notes, otherInfo, id);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
