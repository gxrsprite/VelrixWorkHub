using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomManufacturingComponentRepository(IFreeSql fsql) : IMomManufacturingComponentRepository
{
    public IReadOnlyList<MomManufacturingComponent> List() => fsql.Select<MomManufacturingComponentRecord>().OrderBy(x => x.ManufacturingVersionId).OrderBy(x => x.LineNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomManufacturingComponent item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomManufacturingComponent item)
    {
        var rows = fsql.Update<MomManufacturingComponentRecord>()
            .Set(x => x.LineNo, item.LineNo)
            .Set(x => x.ComponentProductId, item.ComponentProductId)
            .Set(x => x.QuantityPer, item.QuantityPer)
            .Set(x => x.ScrapRatePercent, item.ScrapRatePercent)
            .Set(x => x.OperationSequence, item.OperationSequence)
            .Set(x => x.Notes, item.Notes)
            .Set(x => x.OtherInfo, item.OtherInfo)
            .Where(x => x.Id == item.Id)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("制造版本组件不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<MomManufacturingComponentRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static MomManufacturingComponent ToDomain(MomManufacturingComponentRecord x) => MomManufacturingComponent.Restore(x.Id, x.ManufacturingVersionId, x.LineNo, x.ComponentProductId, x.QuantityPer, x.ScrapRatePercent, x.OperationSequence, x.Notes, x.OtherInfo);
    private static MomManufacturingComponentRecord ToRecord(MomManufacturingComponent x) => new() { Id = x.Id, ManufacturingVersionId = x.ManufacturingVersionId, LineNo = x.LineNo, ComponentProductId = x.ComponentProductId, QuantityPer = x.QuantityPer, ScrapRatePercent = x.ScrapRatePercent, OperationSequence = x.OperationSequence, Notes = x.Notes, OtherInfo = x.OtherInfo };
}
