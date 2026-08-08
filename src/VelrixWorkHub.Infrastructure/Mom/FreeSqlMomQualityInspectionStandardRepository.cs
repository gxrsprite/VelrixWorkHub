using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomQualityInspectionStandardRepository(IFreeSql fsql) : IMomQualityInspectionStandardRepository
{
    public IReadOnlyList<MomQualityInspectionStandard> List() => fsql.Select<MomQualityInspectionStandardRecord>()
        .OrderBy(x => x.InspectionType).OrderBy(x => x.Code).OrderByDescending(x => x.Version).ToList().Select(ToDomain).ToArray();
    public void Add(MomQualityInspectionStandard item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomQualityInspectionStandard item)
    {
        var rows = fsql.Update<MomQualityInspectionStandardRecord>().Set(x => x.ProductId, item.ProductId)
            .Set(x => x.InspectionType, item.InspectionType).Set(x => x.Code, item.Code).Set(x => x.Name, item.Name)
            .Set(x => x.Version, item.Version).Set(x => x.Status, item.Status).Set(x => x.OtherInfo, item.OtherInfo)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("质量标准不存在或已被删除。");
    }
    private static MomQualityInspectionStandard ToDomain(MomQualityInspectionStandardRecord x) => MomQualityInspectionStandard.Restore(x.Id, x.ProductId, x.InspectionType, x.Code, x.Name, x.Version, x.Status, x.OtherInfo);
    private static MomQualityInspectionStandardRecord ToRecord(MomQualityInspectionStandard x) => new()
    {
        Id = x.Id, ProductId = x.ProductId, InspectionType = x.InspectionType, Code = x.Code, Name = x.Name,
        Version = x.Version, Status = x.Status, OtherInfo = x.OtherInfo
    };
}
