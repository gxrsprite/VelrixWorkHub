using FreeSql;
using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

public sealed class FreeSqlPaymentExecutionRepository(IFreeSql fsql) : IOaPaymentExecutionRepository
{
    public IReadOnlyList<OaPaymentExecution> List() => fsql.Select<OaPaymentExecutionRecord>().ToList().Select(ToDomain).ToArray();
    public OaPaymentExecution? Get(Guid id) => fsql.Select<OaPaymentExecutionRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public OaPaymentExecution? GetByPaymentRequest(Guid paymentRequestId) => fsql.Select<OaPaymentExecutionRecord>().Where(x => x.PaymentRequestId == paymentRequestId).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaPaymentExecution item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static OaPaymentExecution ToDomain(OaPaymentExecutionRecord x) => OaPaymentExecution.Restore(x.Id, x.PaymentRequestId, x.ExecutionNo,
        DateOnly.FromDateTime(x.PaidOn), x.Amount, x.Currency, x.Channel, x.ExternalReference, x.Notes, x.ErpSettlementId, x.Operator, x.CreatedAt);

    private static OaPaymentExecutionRecord ToRecord(OaPaymentExecution x) => new()
    {
        Id = x.Id, PaymentRequestId = x.PaymentRequestId, ExecutionNo = x.ExecutionNo, PaidOn = x.PaidOn.ToDateTime(TimeOnly.MinValue),
        Amount = x.Amount, Currency = x.Currency, Channel = x.Channel, ExternalReference = x.ExternalReference, Notes = x.Notes,
        ErpSettlementId = x.ErpSettlementId, Operator = x.Operator, CreatedAt = x.CreatedAt
    };
}
