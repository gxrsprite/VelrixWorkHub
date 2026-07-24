using FreeSql;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

public sealed class FreeSqlLmsCustomerFeatureRepository(IFreeSql fsql) : ILmsCustomerFeatureRepository
{
    public IReadOnlyList<LmsCustomerFeature> List() => fsql.Select<LmsCustomerFeatureRecord>().ToList().Select(x =>
    {
        var item = new LmsCustomerFeature(x.CustomerId, x.FeatureVersionId, x.ExpiresAt, x.Notes, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        item.SetStatus(x.Status);
        return item;
    }).ToArray();

    public void Add(LmsCustomerFeature item) => fsql.Insert(new LmsCustomerFeatureRecord
    {
        Id = item.Id, CustomerId = item.CustomerId, FeatureVersionId = item.FeatureVersionId, ExpiresAt = item.ExpiresAt,
        Notes = item.Notes, OtherInfo = item.OtherInfo, Status = item.Status, CreatedAt = item.CreatedAt
    }).ExecuteAffrows();

    public void Update(LmsCustomerFeature item)
    {
        if (fsql.Update<LmsCustomerFeatureRecord>().Set(x => x.ExpiresAt, item.ExpiresAt).Set(x => x.Notes, item.Notes)
            .Set(x => x.OtherInfo, item.OtherInfo).Set(x => x.Status, item.Status).Where(x => x.Id == item.Id).ExecuteAffrows() == 0)
        {
            throw new InvalidOperationException("客户特性不存在。");
        }
    }
}
