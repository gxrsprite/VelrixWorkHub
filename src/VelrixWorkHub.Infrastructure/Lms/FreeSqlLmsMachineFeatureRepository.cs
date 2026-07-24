using FreeSql;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

public sealed class FreeSqlLmsMachineFeatureRepository(IFreeSql fsql) : ILmsMachineFeatureRepository
{
    public IReadOnlyList<LmsMachineFeature> List() => fsql.Select<LmsMachineFeatureRecord>().ToList().Select(x =>
    {
        var item = new LmsMachineFeature(x.CustomerMachineId, x.FeatureVersionId, x.ExpiresAt, x.Notes, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        item.SetStatus(x.Status);
        return item;
    }).ToArray();
    public void Add(LmsMachineFeature item) => fsql.Insert(new LmsMachineFeatureRecord { Id = item.Id, CustomerMachineId = item.CustomerMachineId, FeatureVersionId = item.FeatureVersionId, ExpiresAt = item.ExpiresAt, Notes = item.Notes, OtherInfo = item.OtherInfo, Status = item.Status, CreatedAt = item.CreatedAt }).ExecuteAffrows();
    public void Update(LmsMachineFeature item)
    {
        if (fsql.Update<LmsMachineFeatureRecord>().Set(x => x.ExpiresAt, item.ExpiresAt).Set(x => x.Notes, item.Notes).Set(x => x.OtherInfo, item.OtherInfo).Set(x => x.Status, item.Status).Where(x => x.Id == item.Id).ExecuteAffrows() == 0) throw new InvalidOperationException("机台特性不存在。");
    }
}
