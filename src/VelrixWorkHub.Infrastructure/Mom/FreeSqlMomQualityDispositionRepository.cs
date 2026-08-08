using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomQualityDispositionRepository(IFreeSql fsql) : IMomQualityDispositionRepository
{
    public IReadOnlyList<MomQualityDisposition> List() => fsql.Select<MomQualityDispositionRecord>().OrderByDescending(x => x.CreatedOn).OrderByDescending(x => x.SourceNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomQualityDisposition item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomQualityDisposition item)
    {
        var rows = fsql.Update<MomQualityDispositionRecord>().Set(x => x.Status, item.Status).Set(x => x.CompletedOn, item.CompletedOn)
            .Set(x => x.CompletedBy, item.CompletedBy).Set(x => x.Notes, item.Notes).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("不合格处置不存在或已被删除。");
    }

    private static MomQualityDisposition ToDomain(MomQualityDispositionRecord x) => MomQualityDisposition.Restore(x.Id, x.NonconformanceId, x.Action, x.Quantity,
        x.TargetWorkOrderId, x.TargetOperationId, x.SourceNo, x.Status, x.CreatedOn, x.CompletedOn, x.CompletedBy, x.Notes, x.OtherInfo);
    private static MomQualityDispositionRecord ToRecord(MomQualityDisposition x) => new()
    {
        Id = x.Id, NonconformanceId = x.NonconformanceId, Action = x.Action, Quantity = x.Quantity, TargetWorkOrderId = x.TargetWorkOrderId,
        TargetOperationId = x.TargetOperationId, SourceNo = x.SourceNo, Status = x.Status, CreatedOn = x.CreatedOn, CompletedOn = x.CompletedOn,
        CompletedBy = x.CompletedBy, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
