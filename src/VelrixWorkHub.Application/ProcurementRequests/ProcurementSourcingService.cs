using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.ProcurementRequests;

public interface IOaProcurementSourcingRepository
{
    IReadOnlyList<OaProcurementSourcing> List();
    OaProcurementSourcing? Get(Guid id);
    OaProcurementSourcing? GetByProcurementRequest(Guid procurementRequestId);
    void Add(OaProcurementSourcing item);
    void Update(OaProcurementSourcing item);
}

public interface IOaProcurementSourcingQuoteRepository
{
    IReadOnlyList<OaProcurementSourcingQuote> List(Guid sourcingId);
    OaProcurementSourcingQuote? Get(Guid id);
    void Add(OaProcurementSourcingQuote item);
}

public sealed class ProcurementSourcingService(
    IOaProcurementSourcingRepository sourcings,
    IOaProcurementSourcingQuoteRepository quotes,
    ISupplierRepository suppliers)
{
    public IReadOnlyList<OaProcurementSourcing> List()
        => sourcings.List().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.SourcingNo).ToArray();

    public IReadOnlyList<OaProcurementSourcingQuote> ListQuotes(Guid sourcingId)
        => quotes.List(sourcingId).OrderBy(x => x.QuoteAmount).ThenBy(x => x.DeliveryDays).ThenBy(x => x.CreatedAt).ToArray();

    public OaProcurementSourcingQuote? GetAwardedQuote(OaProcurementSourcing sourcing)
        => sourcing.AwardedQuoteId is Guid id ? quotes.Get(id) : null;

    public IReadOnlyList<OaProcurementRequest> ListEligibleRequests(IEnumerable<OaProcurementRequest> requests, bool canManage)
    {
        EnsureManagePermission(canManage);
        var activeRequestIds = sourcings.List().Where(x => x.Status != OaProcurementSourcingStatus.Cancelled)
            .Select(x => x.ProcurementRequestId).ToHashSet();
        return requests.Where(x => x.Status == OaProcurementRequestStatus.Approved
                && x.RequestType == OaProcurementRequestType.Sourcing
                && !activeRequestIds.Contains(x.Id))
            .OrderBy(x => x.RequiredDate).ThenBy(x => x.CreatedAt).ToArray();
    }

    public OaProcurementSourcing CreateForApprovedRequest(OaProcurementRequest request, string sourcingNo, string createdBy, string? otherInfo, bool canManage)
    {
        EnsureManagePermission(canManage);
        if (request.Status != OaProcurementRequestStatus.Approved || request.RequestType != OaProcurementRequestType.Sourcing)
            throw new InvalidOperationException("只有已批准的寻源需求申请可以创建寻源单。");
        var existing = sourcings.GetByProcurementRequest(request.Id);
        if (existing is not null && existing.Status != OaProcurementSourcingStatus.Cancelled)
            throw new InvalidOperationException("该寻源需求已经存在未撤销的寻源单。");
        if (sourcings.List().Any(x => x.SourcingNo.Equals(sourcingNo.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("寻源编号已存在。");
        var item = new OaProcurementSourcing(sourcingNo, request.Id, createdBy, otherInfo, DateTime.Now);
        sourcings.Add(item);
        return item;
    }

    public OaProcurementSourcingQuote AddQuote(OaProcurementSourcing sourcing, Guid supplierId, decimal quoteAmount,
        int deliveryDays, DateOnly validUntil, string? notes, string? otherInfo, bool canManage)
    {
        EnsureManagePermission(canManage);
        EnsureDraft(sourcing);
        var supplier = suppliers.List().FirstOrDefault(x => x.Id == supplierId) ?? throw new InvalidOperationException("供应商不存在。");
        if (supplier.Status != SupplierStatus.Active) throw new InvalidOperationException("供应商已停用，不能参与寻源报价。");
        if (supplier.QualificationStatus != SupplierQualificationStatus.Qualified) throw new InvalidOperationException("供应商未通过采购准入，不能参与寻源报价。");
        if (ListQuotes(sourcing.Id).Any(x => x.SupplierId == supplierId)) throw new InvalidOperationException("同一供应商不能重复报价。");
        if (validUntil < DateOnly.FromDateTime(DateTime.Today)) throw new InvalidOperationException("报价有效期不能早于今天。");
        var item = new OaProcurementSourcingQuote(sourcing.Id, supplierId, quoteAmount, deliveryDays, validUntil, notes, otherInfo, DateTime.Now);
        quotes.Add(item);
        return item;
    }

    public void Submit(OaProcurementSourcing sourcing, bool canManage)
    {
        EnsureManagePermission(canManage);
        sourcing.Submit(ListQuotes(sourcing.Id).Count);
        sourcings.Update(sourcing);
    }

    public void Award(OaProcurementSourcing sourcing, Guid quoteId, bool canManage)
    {
        EnsureManagePermission(canManage);
        var quote = quotes.Get(quoteId) ?? throw new InvalidOperationException("报价不存在。");
        if (quote.SourcingId != sourcing.Id) throw new InvalidOperationException("报价不属于当前寻源单。");
        if (quote.ValidUntil < DateOnly.FromDateTime(DateTime.Today)) throw new InvalidOperationException("中选报价已过有效期。");
        sourcing.Award(quote.Id);
        sourcings.Update(sourcing);
    }

    public void Cancel(OaProcurementSourcing sourcing, bool canManage)
    {
        EnsureManagePermission(canManage);
        sourcing.Cancel();
        sourcings.Update(sourcing);
    }

    private static void EnsureDraft(OaProcurementSourcing sourcing)
    {
        if (sourcing.Status != OaProcurementSourcingStatus.Draft) throw new InvalidOperationException("只有草稿寻源单可以录入报价。");
    }

    private static void EnsureManagePermission(bool canManage)
    {
        if (!canManage) throw new UnauthorizedAccessException("当前用户没有维护采购寻源的权限。");
    }
}
