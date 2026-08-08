namespace VelrixWorkHub.Domain;

/// <summary>
/// 制造版本中的工序路线与标准工时。发布前维护，工单生成工序时冻结为执行快照。
/// </summary>
public sealed class MomManufacturingOperationStandard
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid ManufacturingVersionId { get; private set; }
    public int OperationSequence { get; private set; }
    public string OperationCode { get; private set; } = string.Empty;
    public string OperationName { get; private set; } = string.Empty;
    public Guid WorkCenterId { get; private set; }
    public decimal SetupHours { get; private set; }
    public decimal RunHoursPerUnit { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomManufacturingOperationStandard(Guid manufacturingVersionId, int operationSequence, string operationCode,
        string operationName, Guid workCenterId, decimal setupHours, decimal runHoursPerUnit, string? otherInfo = null)
    {
        Edit(manufacturingVersionId, operationSequence, operationCode, operationName, workCenterId, setupHours, runHoursPerUnit, otherInfo);
    }

    public static MomManufacturingOperationStandard Restore(Guid id, Guid manufacturingVersionId, int operationSequence,
        string operationCode, string operationName, Guid workCenterId, decimal setupHours, decimal runHoursPerUnit, string? otherInfo)
        => new(manufacturingVersionId, operationSequence, operationCode, operationName, workCenterId, setupHours, runHoursPerUnit, otherInfo) { Id = id };

    public void Edit(Guid manufacturingVersionId, int operationSequence, string operationCode, string operationName,
        Guid workCenterId, decimal setupHours, decimal runHoursPerUnit, string? otherInfo = null)
    {
        if (manufacturingVersionId == Guid.Empty) throw new ArgumentException("工序标准必须绑定制造版本。", nameof(manufacturingVersionId));
        if (operationSequence < 0) throw new ArgumentOutOfRangeException(nameof(operationSequence), "工序顺序不能为负数。");
        if (string.IsNullOrWhiteSpace(operationCode)) throw new ArgumentException("工序编码不能为空。", nameof(operationCode));
        if (string.IsNullOrWhiteSpace(operationName)) throw new ArgumentException("工序名称不能为空。", nameof(operationName));
        if (workCenterId == Guid.Empty) throw new ArgumentException("工序标准必须绑定工作中心。", nameof(workCenterId));
        if (setupHours < 0) throw new ArgumentOutOfRangeException(nameof(setupHours), "准备工时不能为负数。");
        if (runHoursPerUnit < 0) throw new ArgumentOutOfRangeException(nameof(runHoursPerUnit), "单位运行工时不能为负数。");
        if (setupHours == 0 && runHoursPerUnit == 0) throw new ArgumentException("准备工时和单位运行工时不能同时为零。");
        ManufacturingVersionId = manufacturingVersionId; OperationSequence = operationSequence;
        OperationCode = operationCode.Trim(); OperationName = operationName.Trim(); WorkCenterId = workCenterId;
        SetupHours = Round(setupHours); RunHoursPerUnit = Round(runHoursPerUnit);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public decimal StandardHoursFor(decimal plannedQuantity)
    {
        if (plannedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(plannedQuantity), "计划数量必须大于零。");
        return Round(SetupHours + RunHoursPerUnit * plannedQuantity);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
