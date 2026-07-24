using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Overtime;

public interface IOaOvertimeConversionRepository
{
    IReadOnlyList<OaOvertimeConversion> List(Guid? userId = null);
    OaOvertimeConversion? Get(Guid id);
    OaOvertimeConversion? GetByOvertimeRequest(Guid overtimeRequestId);
    void Add(OaOvertimeConversion item);
    void Update(OaOvertimeConversion item);
}

public sealed class OvertimeConversionService(
    IOaOvertimeConversionRepository conversions,
    LeaveBalanceService leaveBalances)
{
    public IReadOnlyList<OaOvertimeConversion> ListMine(Guid userId) => conversions.List(userId).OrderByDescending(x => x.CreatedAt).ToArray();
    public IReadOnlyList<OaOvertimeConversion> ListPendingFinanceProcessing()
        => conversions.List().Where(x => x.Type == OaOvertimeConversionType.FinanceManual && x.FinanceProcessingStatus == OaOvertimeFinanceProcessingStatus.Pending)
            .OrderBy(x => x.CreatedAt).ToArray();
    public IReadOnlyList<OaOvertimeConversion> ListFinanceProcessing()
        => conversions.List().Where(x => x.Type == OaOvertimeConversionType.FinanceManual).OrderByDescending(x => x.CreatedAt).ToArray();
    public OaOvertimeConversion? GetByOvertimeRequest(Guid overtimeRequestId) => conversions.GetByOvertimeRequest(overtimeRequestId);

    public OaOvertimeConversion Convert(OaOvertimeRequest overtime, Guid actorUserId, OaOvertimeConversionType type)
    {
        if (actorUserId == Guid.Empty || overtime.UserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能兑换其他员工的加班申请。");
        if (overtime.Status != OaOvertimeRequestStatus.Approved) throw new InvalidOperationException("只有已批准加班申请可以兑换。");
        if (DateTime.Now < overtime.EndAt) throw new InvalidOperationException("加班尚未结束，不能申请调休或登记财务处理。");
        if (DateTime.Now > overtime.EndAt.AddDays(30)) throw new InvalidOperationException("加班结束超过 30 天，不能申请调休或登记财务处理。");
        if (conversions.GetByOvertimeRequest(overtime.Id) is not null) throw new InvalidOperationException("该加班申请已兑换，不能重复选择调休或加班费。");
        if (type == OaOvertimeConversionType.CompensatoryLeave)
            leaveBalances.GrantOvertimeCompensatory(overtime.UserId, overtime.EndAt.Year, overtime.DurationHours);
        var item = new OaOvertimeConversion(overtime.Id, overtime.UserId, type, overtime.DurationHours, DateTime.Now);
        conversions.Add(item);
        return item;
    }

    public void MarkFinanceProcessed(Guid conversionId, string actor, string? note, bool canProcess)
    {
        if (!canProcess) throw new UnauthorizedAccessException("当前用户没有处理加班费登记的权限。");
        var item = conversions.Get(conversionId) ?? throw new InvalidOperationException("加班费财务处理记录不存在。");
        item.MarkFinanceProcessed(actor, note);
        conversions.Update(item);
    }
}
