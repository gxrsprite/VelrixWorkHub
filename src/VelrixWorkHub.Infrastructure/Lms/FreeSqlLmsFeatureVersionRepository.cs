using FreeSql;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

public sealed class FreeSqlLmsFeatureVersionRepository(IFreeSql fsql) : ILmsFeatureVersionRepository
{
    public IReadOnlyList<LmsFeatureVersion> List() => fsql.Select<LmsFeatureVersionRecord>().ToList().Select(x =>
    {
        var item = new LmsFeatureVersion(x.FeatureId, x.Version, x.Level, x.Scope, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        item.SetStatus(x.Status);
        return item;
    }).ToArray();

    public void Add(LmsFeatureVersion item) => fsql.Insert(new LmsFeatureVersionRecord
    {
        Id = item.Id, FeatureId = item.FeatureId, Version = item.Version, Level = item.Level,
        Scope = item.Scope, Status = item.Status, OtherInfo = item.OtherInfo, CreatedAt = item.CreatedAt
    }).ExecuteAffrows();

    public void Update(LmsFeatureVersion item)
    {
        if (fsql.Update<LmsFeatureVersionRecord>().Set(x => x.Status, item.Status).Where(x => x.Id == item.Id).ExecuteAffrows() == 0)
        {
            throw new InvalidOperationException("特性版本不存在。");
        }
    }
}
