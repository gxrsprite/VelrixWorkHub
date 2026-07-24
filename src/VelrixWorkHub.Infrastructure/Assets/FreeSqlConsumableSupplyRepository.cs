using FreeSql;
using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

public sealed class FreeSqlConsumableSupplyRepository(IFreeSql fsql) : IOaConsumableSupplyRepository, IOaConsumableTransactionRepository
{
    public IReadOnlyList<OaConsumableSupply> List() => fsql.Select<OaConsumableSupplyRecord>().OrderBy(item => item.Code).ToList().Select(ToDomain).ToArray();
    public OaConsumableSupply? Get(Guid id) => fsql.Select<OaConsumableSupplyRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaConsumableSupply supply) => fsql.Insert(ToRecord(supply)).ExecuteAffrows();
    public void Update(OaConsumableSupply supply) => fsql.Update<OaConsumableSupplyRecord>().SetSource(ToRecord(supply)).Where(item => item.Id == supply.Id).ExecuteAffrows();

    public IReadOnlyList<OaConsumableTransaction> List(Guid? supplyId = null, Guid? recipientUserId = null)
    {
        var query = fsql.Select<OaConsumableTransactionRecord>();
        if (supplyId is Guid supply) query = query.Where(item => item.SupplyId == supply);
        if (recipientUserId is Guid recipient) query = query.Where(item => item.RecipientUserId == recipient);
        return query.OrderByDescending(item => item.OccurredAt).ToList().Select(item => new OaConsumableTransaction(item.SupplyId, item.Kind, item.Quantity, item.RecipientUserId, item.SourceNo, item.ActorName, item.Notes, item.OccurredAt) { Id = item.Id }).ToArray();
    }

    public void Add(OaConsumableTransaction transaction) => fsql.Insert(new OaConsumableTransactionRecord
    {
        Id = transaction.Id, SupplyId = transaction.SupplyId, Kind = transaction.Kind, Quantity = transaction.Quantity,
        RecipientUserId = transaction.RecipientUserId, SourceNo = transaction.SourceNo, ActorName = transaction.ActorName,
        Notes = transaction.Notes, OccurredAt = transaction.OccurredAt
    }).ExecuteAffrows();

    private static OaConsumableSupply ToDomain(OaConsumableSupplyRecord item)
    {
        var supply = new OaConsumableSupply(item.Code, item.Name, item.Unit, item.Location, item.OtherInfo, item.CreatedAt) { Id = item.Id };
        supply.SetActive(item.IsActive, item.UpdatedAt);
        return supply;
    }
    private static OaConsumableSupplyRecord ToRecord(OaConsumableSupply item) => new()
    {
        Id = item.Id, Code = item.Code, Name = item.Name, Unit = item.Unit, Location = item.Location, IsActive = item.IsActive,
        OtherInfo = item.OtherInfo, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
    };
}
