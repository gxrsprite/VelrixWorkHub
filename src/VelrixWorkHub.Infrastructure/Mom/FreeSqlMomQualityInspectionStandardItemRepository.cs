using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomQualityInspectionStandardItemRepository(IFreeSql fsql) : IMomQualityInspectionStandardItemRepository
{
    public IReadOnlyList<MomQualityInspectionStandardItem> List() => fsql.Select<MomQualityInspectionStandardItemRecord>()
        .OrderBy(x => x.StandardId).OrderBy(x => x.LineNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomQualityInspectionStandardItem item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomQualityInspectionStandardItem item)
    {
        var rows = fsql.Update<MomQualityInspectionStandardItemRecord>().Set(x => x.LineNo, item.LineNo).Set(x => x.Code, item.Code)
            .Set(x => x.Name, item.Name).Set(x => x.Requirement, item.Requirement).Set(x => x.Unit, item.Unit)
            .Set(x => x.MinValue, item.MinValue).Set(x => x.MaxValue, item.MaxValue).Set(x => x.OtherInfo, item.OtherInfo)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("检验项目不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<MomQualityInspectionStandardItemRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static MomQualityInspectionStandardItem ToDomain(MomQualityInspectionStandardItemRecord x) => MomQualityInspectionStandardItem.Restore(x.Id, x.StandardId, x.LineNo, x.Code, x.Name, x.Requirement, x.Unit, x.MinValue, x.MaxValue, x.OtherInfo);
    private static MomQualityInspectionStandardItemRecord ToRecord(MomQualityInspectionStandardItem x) => new()
    {
        Id = x.Id, StandardId = x.StandardId, LineNo = x.LineNo, Code = x.Code, Name = x.Name, Requirement = x.Requirement,
        Unit = x.Unit, MinValue = x.MinValue, MaxValue = x.MaxValue, OtherInfo = x.OtherInfo
    };
}
