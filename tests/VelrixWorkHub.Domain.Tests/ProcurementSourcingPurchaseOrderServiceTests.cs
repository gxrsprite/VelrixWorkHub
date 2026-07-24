using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ProcurementSourcingPurchaseOrderServiceTests
{
    [Fact]
    public void AwardedQuoteCreatesPurchaseOrderWithSupplierPriceAndStableSource()
    {
        var supplier = new Supplier("SUP-AWARDED", "中选供应商", null, null, null);
        var otherSupplier = new Supplier("SUP-AWARDED-OTHER", "备选供应商", null, null, null);
        var product = new Product("SKU-AWARDED", "寻源商品", "件", null, null);
        var sourcingRepository = new SourcingRepository();
        var quoteRepository = new QuoteRepository();
        var sourcingService = new ProcurementSourcingService(sourcingRepository, quoteRepository, new SupplierRepository(supplier, otherSupplier));
        var request = new OaProcurementRequest(Guid.CreateVersion7(), "申请人", "采购部", "Velrix", "CG-AWARDED-001",
            OaProcurementRequestType.Sourcing, Today, Today.AddDays(7), null, null, "比价采购", "{}", DateTime.Now);
        request.Submit(DateTime.Now);
        request.Approve();
        var sourcing = sourcingService.CreateForApprovedRequest(request, "SOURCE-AWARDED-001", "buyer", "{}", true);
        sourcingService.AddQuote(sourcing, supplier.Id, 128.50m, 5, Today.AddDays(14), "含税", "{}", true);
        var backup = sourcingService.AddQuote(sourcing, otherSupplier.Id, 135m, 3, Today.AddDays(14), "交期快", "{}", true);
        sourcingService.Submit(sourcing, true);
        var selected = quoteRepository.List(sourcing.Id).Single(x => x.SupplierId == supplier.Id);
        sourcingService.Award(sourcing, selected.Id, true);

        var orders = new PurchaseOrderRepository();
        var purchaseOrders = new PurchaseOrderService(orders, new SupplierRepository(supplier, otherSupplier), new ProductRepository(product),
            new InventoryRepository(), new WarehouseRepository(new Warehouse("WH-AWARDED", "采购仓", null)), new SettlementRepository());
        var service = new ProcurementSourcingPurchaseOrderService(sourcingService, purchaseOrders);

        var order = service.CreateFromAwardedQuote(sourcing, "PO-SOURCE-AWARDED-001", product.Id, 6m, Today.AddDays(30), true);

        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Equal(PurchaseOrderSourceKind.Sourcing, order.SourceKind);
        Assert.Equal(sourcing.SourcingNo, order.SourceDocumentNo);
        Assert.Equal(supplier.Id, order.SupplierId);
        Assert.Equal(product.Id, order.ProductId);
        Assert.Equal(128.50m, order.UnitPrice);
        Assert.Equal(6m, order.Quantity);
        var retry = service.CreateFromAwardedQuote(sourcing, "PO-SOURCE-AWARDED-RETRY", product.Id, 9m, Today.AddDays(30), true);
        Assert.Equal(order.Id, retry.Id);
        Assert.Single(orders.List());
        Assert.Equal(backup.SupplierId, quoteRepository.List(sourcing.Id).Single(x => x.Id != selected.Id).SupplierId);
    }

    [Fact]
    public void AwardedQuoteConversionRequiresAwardAndPermission()
    {
        var supplier = new Supplier("SUP-AWARDED-GUARD", "准入供应商", null, null, null);
        var product = new Product("SKU-AWARDED-GUARD", "保护商品", "件", null, null);
        var sourcingRepository = new SourcingRepository();
        var quoteRepository = new QuoteRepository();
        var sourcingService = new ProcurementSourcingService(sourcingRepository, quoteRepository, new SupplierRepository(supplier));
        var request = new OaProcurementRequest(Guid.CreateVersion7(), "申请人", "采购部", "Velrix", "CG-AWARDED-002",
            OaProcurementRequestType.Sourcing, Today, Today.AddDays(7), null, null, "比价采购", "{}", DateTime.Now);
        request.Submit(DateTime.Now);
        request.Approve();
        var sourcing = sourcingService.CreateForApprovedRequest(request, "SOURCE-AWARDED-002", "buyer", "{}", true);
        var purchaseOrders = new PurchaseOrderService(new PurchaseOrderRepository(), new SupplierRepository(supplier), new ProductRepository(product),
            new InventoryRepository(), new WarehouseRepository(new Warehouse("WH-AWARDED-GUARD", "采购仓", null)), new SettlementRepository());
        var service = new ProcurementSourcingPurchaseOrderService(sourcingService, purchaseOrders);

        Assert.Throws<UnauthorizedAccessException>(() => service.CreateFromAwardedQuote(sourcing, "PO-GUARD", product.Id, 1m, Today.AddDays(30), false));
        Assert.Throws<InvalidOperationException>(() => service.CreateFromAwardedQuote(sourcing, "PO-GUARD", product.Id, 1m, Today.AddDays(30), true));
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

    private sealed class SupplierRepository(params Supplier[] seed) : ISupplierRepository
    {
        private readonly List<Supplier> items = [.. seed];
        public IReadOnlyList<Supplier> List() => items;
        public void Add(Supplier item) => items.Add(item);
        public void Update(Supplier item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class ProductRepository(params Product[] seed) : IProductRepository
    {
        private readonly List<Product> items = [.. seed];
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class PurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> items = [];
        public IReadOnlyList<PurchaseOrder> List() => items;
        public void Add(PurchaseOrder item) => items.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class InventoryRepository : IInventoryTransactionRepository
    {
        public IReadOnlyList<InventoryTransaction> List() => [];
        public void Add(InventoryTransaction item) { }
    }

    private sealed class WarehouseRepository(params Warehouse[] seed) : IWarehouseRepository
    {
        private readonly List<Warehouse> items = [.. seed];
        public IReadOnlyList<Warehouse> List() => items;
        public void Add(Warehouse item) => items.Add(item);
        public void Update(Warehouse item) { }
        public void Remove(Guid id) { }
        public void AddLocation(WarehouseLocation item) { }
        public void RemoveLocation(Guid id) { }
    }

    private sealed class SettlementRepository : ISettlementRepository
    {
        public IReadOnlyList<ErpSettlement> List() => [];
        public void Add(ErpSettlement item) { }
        public void Update(ErpSettlement item) { }
    }
}
