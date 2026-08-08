using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomAcceptanceChecklistRepository(IFreeSql fsql) : IMomAcceptanceChecklistRepository
{
    public IReadOnlyList<MomAcceptanceChecklistItem> List(Guid? acceptanceId = null)
    {
        var query = fsql.Select<MomAcceptanceChecklistItemRecord>();
        if (acceptanceId is Guid selected) query = query.Where(x => x.AcceptanceId == selected);
        return query.OrderBy(x => x.LineNo).ToList().Select(ToDomain).ToArray();
    }

    public void Add(MomAcceptanceChecklistItem item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomAcceptanceChecklistItem item) => fsql.Update<MomAcceptanceChecklistItemRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows();
    public void Remove(Guid id) => fsql.Delete<MomAcceptanceChecklistItemRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static MomAcceptanceChecklistItem ToDomain(MomAcceptanceChecklistItemRecord x) => MomAcceptanceChecklistItem.Restore(x.Id, x.AcceptanceId, x.LineNo,
        x.ItemCode, x.ItemName, x.Requirement, x.Result, x.Remark, x.CheckedBy, x.CheckedOn, x.OtherInfo);

    private static MomAcceptanceChecklistItemRecord ToRecord(MomAcceptanceChecklistItem x) => new()
    {
        Id = x.Id, AcceptanceId = x.AcceptanceId, LineNo = x.LineNo, ItemCode = x.ItemCode, ItemName = x.ItemName,
        Requirement = x.Requirement, Result = x.Result, Remark = x.Remark, CheckedBy = x.CheckedBy, CheckedOn = x.CheckedOn, OtherInfo = x.OtherInfo
    };
}
