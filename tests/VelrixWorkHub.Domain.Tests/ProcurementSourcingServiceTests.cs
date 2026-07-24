using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ProcurementSourcingServiceTests
{
    [Fact]
    public void ApprovedSourcingRequestCollectsQuotesAndAwardsOneSupplier()
    {
        var sourcingRepository = new SourcingRepository();
        var quoteRepository = new QuoteRepository();
        var supplierA = new Supplier("SUP-SOURCE-A", "供应商甲", null, null, null);
        var supplierB = new Supplier("SUP-SOURCE-B", "供应商乙", null, null, null);
        var service = new ProcurementSourcingService(sourcingRepository, quoteRepository, new SupplierRepository(supplierA, supplierB));
        var request = ApprovedSourcingRequest("CG-SOURCE-001");
        var sourcing = service.CreateForApprovedRequest(request, "SOURCE-001", "buyer", "{}", canManage: true);

        service.AddQuote(sourcing, supplierA.Id, 120, 7, Today.AddDays(14), "含税含运", "{}", canManage: true);
        service.AddQuote(sourcing, supplierB.Id, 110, 10, Today.AddDays(14), "交期较长", "{}", canManage: true);
        Assert.Throws<InvalidOperationException>(() => service.AddQuote(sourcing, supplierA.Id, 100, 5, Today.AddDays(14), null, "{}", true));

        service.Submit(sourcing, canManage: true);
        var selected = quoteRepository.List(sourcing.Id).Single(x => x.SupplierId == supplierB.Id);
        service.Award(sourcing, selected.Id, canManage: true);

        Assert.Equal(OaProcurementSourcingStatus.Awarded, sourcing.Status);
        Assert.Equal(selected.Id, sourcing.AwardedQuoteId);
        Assert.Equal(selected.Id, service.GetAwardedQuote(sourcing)!.Id);
    }

    [Fact]
    public void SubmissionRequiresTwoQuotesAndOnlyDraftCanReceiveQuotes()
    {
        var supplier = new Supplier("SUP-SOURCE-C", "供应商丙", null, null, null);
        var service = new ProcurementSourcingService(new SourcingRepository(), new QuoteRepository(), new SupplierRepository(supplier));
        var sourcing = service.CreateForApprovedRequest(ApprovedSourcingRequest("CG-SOURCE-002"), "SOURCE-002", "buyer", "{}", true);
        service.AddQuote(sourcing, supplier.Id, 80, 3, Today.AddDays(7), null, "{}", true);

        Assert.Throws<InvalidOperationException>(() => service.Submit(sourcing, canManage: true));
        Assert.Throws<UnauthorizedAccessException>(() => service.AddQuote(sourcing, Guid.CreateVersion7(), 80, 3, Today.AddDays(7), null, "{}", false));
    }

    [Fact]
    public void CancelledSourcingPreservesHistoryAndAllowsNewSourcingRound()
    {
        var sourcingRepository = new SourcingRepository();
        var supplier = new Supplier("SUP-SOURCE-D", "供应商丁", null, null, null);
        var service = new ProcurementSourcingService(sourcingRepository, new QuoteRepository(), new SupplierRepository(supplier));
        var request = ApprovedSourcingRequest("CG-SOURCE-003");
        var first = service.CreateForApprovedRequest(request, "SOURCE-003-A", "buyer", "{}", true);
        service.Cancel(first, canManage: true);

        Assert.Single(service.ListEligibleRequests([request], canManage: true));
        var second = service.CreateForApprovedRequest(request, "SOURCE-003-B", "buyer", "{}", true);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(OaProcurementSourcingStatus.Cancelled, first.Status);
    }

    [Fact]
    public void CreationRequiresApprovedSourcingRequestAndPermission()
    {
        var service = new ProcurementSourcingService(new SourcingRepository(), new QuoteRepository(), new SupplierRepository());
        var request = new OaProcurementRequest(Guid.CreateVersion7(), "申请人", "采购部", "Velrix", "CG-SOURCE-004",
            OaProcurementRequestType.ProductRelated, Today, Today, null, null, "采购", "{}", DateTime.Now);
        Assert.Throws<UnauthorizedAccessException>(() => service.CreateForApprovedRequest(request, "SOURCE-004", "buyer", "{}", false));
        Assert.Throws<InvalidOperationException>(() => service.CreateForApprovedRequest(request, "SOURCE-004", "buyer", "{}", true));
    }

    private static OaProcurementRequest ApprovedSourcingRequest(string documentNo)
    {
        var request = new OaProcurementRequest(Guid.CreateVersion7(), "申请人", "采购部", "Velrix", documentNo,
            OaProcurementRequestType.Sourcing, Today, Today.AddDays(7), null, null, "供应商寻源", "{}", DateTime.Now);
        request.Submit(DateTime.Now);
        request.Approve();
        return request;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private sealed class SourcingRepository : IOaProcurementSourcingRepository
    {
        private readonly List<OaProcurementSourcing> items = [];
        public IReadOnlyList<OaProcurementSourcing> List() => items;
        public OaProcurementSourcing? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public OaProcurementSourcing? GetByProcurementRequest(Guid procurementRequestId) => items.Where(x => x.ProcurementRequestId == procurementRequestId).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        public void Add(OaProcurementSourcing item) => items.Add(item);
        public void Update(OaProcurementSourcing item) { }
    }

    private sealed class QuoteRepository : IOaProcurementSourcingQuoteRepository
    {
        private readonly List<OaProcurementSourcingQuote> items = [];
        public IReadOnlyList<OaProcurementSourcingQuote> List(Guid sourcingId) => items.Where(x => x.SourcingId == sourcingId).ToArray();
        public OaProcurementSourcingQuote? Get(Guid id) => items.FirstOrDefault(x => x.Id == id);
        public void Add(OaProcurementSourcingQuote item) => items.Add(item);
    }

    private sealed class SupplierRepository(params Supplier[] initial) : ISupplierRepository
    {
        private readonly List<Supplier> items = [.. initial];
        public IReadOnlyList<Supplier> List() => items;
        public void Add(Supplier item) => items.Add(item);
        public void Update(Supplier item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }
}
