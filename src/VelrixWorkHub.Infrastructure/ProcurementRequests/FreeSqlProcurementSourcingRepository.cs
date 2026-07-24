using FreeSql;
using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.ProcurementRequests;

public sealed class FreeSqlProcurementSourcingRepository(IFreeSql fsql) : IOaProcurementSourcingRepository
{
    public IReadOnlyList<OaProcurementSourcing> List() => fsql.Select<OaProcurementSourcingRecord>().ToList().Select(ToDomain).ToArray();
    public OaProcurementSourcing? Get(Guid id) => fsql.Select<OaProcurementSourcingRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public OaProcurementSourcing? GetByProcurementRequest(Guid procurementRequestId) => fsql.Select<OaProcurementSourcingRecord>().Where(x => x.ProcurementRequestId == procurementRequestId).OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaProcurementSourcing item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(OaProcurementSourcing item) => fsql.Update<OaProcurementSourcingRecord>().SetSource(ToRecord(item)).ExecuteAffrows();

    private static OaProcurementSourcing ToDomain(OaProcurementSourcingRecord x) => OaProcurementSourcing.Restore(x.Id, x.SourcingNo, x.ProcurementRequestId,
        x.CreatedBy, x.OtherInfo, x.Status, x.AwardedQuoteId, x.CreatedAt, x.AwardedAt);

    private static OaProcurementSourcingRecord ToRecord(OaProcurementSourcing x) => new()
    {
        Id = x.Id, SourcingNo = x.SourcingNo, ProcurementRequestId = x.ProcurementRequestId, CreatedBy = x.CreatedBy,
        OtherInfo = x.OtherInfo, Status = x.Status, AwardedQuoteId = x.AwardedQuoteId, CreatedAt = x.CreatedAt, AwardedAt = x.AwardedAt
    };
}

public sealed class FreeSqlProcurementSourcingQuoteRepository(IFreeSql fsql) : IOaProcurementSourcingQuoteRepository
{
    public IReadOnlyList<OaProcurementSourcingQuote> List(Guid sourcingId) => fsql.Select<OaProcurementSourcingQuoteRecord>().Where(x => x.SourcingId == sourcingId).ToList().Select(ToDomain).ToArray();
    public OaProcurementSourcingQuote? Get(Guid id) => fsql.Select<OaProcurementSourcingQuoteRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaProcurementSourcingQuote item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static OaProcurementSourcingQuote ToDomain(OaProcurementSourcingQuoteRecord x) => OaProcurementSourcingQuote.Restore(x.Id, x.SourcingId, x.SupplierId,
        x.QuoteAmount, x.DeliveryDays, DateOnly.FromDateTime(x.ValidUntil), x.Notes, x.OtherInfo, x.CreatedAt);

    private static OaProcurementSourcingQuoteRecord ToRecord(OaProcurementSourcingQuote x) => new()
    {
        Id = x.Id, SourcingId = x.SourcingId, SupplierId = x.SupplierId, QuoteAmount = x.QuoteAmount, DeliveryDays = x.DeliveryDays,
        ValidUntil = x.ValidUntil.ToDateTime(TimeOnly.MinValue), Notes = x.Notes, OtherInfo = x.OtherInfo, CreatedAt = x.CreatedAt
    };
}
