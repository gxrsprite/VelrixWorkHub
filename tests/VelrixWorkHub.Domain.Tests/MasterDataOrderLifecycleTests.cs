using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MasterDataOrderLifecycleTests
{
    [Fact]
    public void SalesOrderCreate_RejectsInactiveCustomerWithoutWriting()
    {
        var customer = new Customer("停用客户");
        customer.SetActive(false);
        var product = new Product("SKU-SALES-LIFECYCLE", "销售生命周期商品", "件", 10m, null);
        var repository = new SalesOrderRepository();
        var service = new SalesOrderService(repository, new CustomerRepository(customer), new ProductRepository(product), null!, null!, new SalesContractService(new ContractRepository()), new SettlementRepository());

        var error = Assert.Throws<InvalidOperationException>(() => service.Create("SO-INACTIVE-CUSTOMER", customer.Id, product.Id, Today, 1m, 10m));

        Assert.Equal("客户已停用，不能创建销售订单。", error.Message);
        Assert.Empty(repository.List());

        customer.SetActive(true);
        var order = service.Create("SO-ACTIVE-CUSTOMER", customer.Id, product.Id, Today, 1m, 10m);

        Assert.Equal(customer.Id, order.CustomerId);
    }

    [Fact]
    public void PurchaseOrderCreate_RejectsInactiveSupplierWithoutWriting()
    {
        var supplier = new Supplier("SUP-INACTIVE", "停用供应商", null, null, null);
        supplier.SetActive(false);
        var product = new Product("SKU-PURCHASE-LIFECYCLE", "采购生命周期商品", "件", 10m, null);
        var repository = new PurchaseOrderRepository();
        var service = new PurchaseOrderService(repository, new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());

        var error = Assert.Throws<InvalidOperationException>(() => service.Create("PO-INACTIVE-SUPPLIER", supplier.Id, product.Id, Today, 1m, 10m));

        Assert.Equal("供应商已停用，不能创建采购订单。", error.Message);
        Assert.Empty(repository.List());

        supplier.SetActive(true);
        var order = service.Create("PO-ACTIVE-SUPPLIER", supplier.Id, product.Id, Today, 1m, 10m);

        Assert.Equal(supplier.Id, order.SupplierId);
    }

    [Fact]
    public void PurchaseOrderCreate_RejectsUnqualifiedSupplierWithoutWriting()
    {
        var supplier = new Supplier("SUP-PENDING", "待准入供应商", null, null, null);
        supplier.SetQualification(SupplierQualificationStatus.Suspended);
        var product = new Product("SKU-PURCHASE-QUALIFICATION", "准入校验商品", "件", 10m, null);
        var repository = new PurchaseOrderRepository();
        var service = new PurchaseOrderService(repository, new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());

        var error = Assert.Throws<InvalidOperationException>(() => service.Create("PO-UNQUALIFIED-SUPPLIER", supplier.Id, product.Id, Today, 1m, 10m));

        Assert.Equal("供应商未通过采购准入，不能创建采购订单。", error.Message);
        Assert.Empty(repository.List());

        supplier.SetQualification(SupplierQualificationStatus.Qualified);
        var order = service.Create("PO-QUALIFIED-SUPPLIER", supplier.Id, product.Id, Today, 1m, 10m);

        Assert.Equal(supplier.Id, order.SupplierId);
    }

    [Fact]
    public void PurchaseOrderCreate_RejectsQuantityAboveProductLimitWithoutWriting()
    {
        var supplier = new Supplier("SUP-MAX-QUANTITY", "限量供应商", null, null, null);
        var product = new Product("SKU-MAX-QUANTITY", "限量商品", "件", 10m, null, 5m);
        var repository = new PurchaseOrderRepository();
        var service = new PurchaseOrderService(repository, new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());

        var error = Assert.Throws<InvalidOperationException>(() => service.Create("PO-MAX-QUANTITY", supplier.Id, product.Id, Today, 6m, 10m));

        Assert.Equal("采购数量不能超过商品单次最大采购量 5.00。", error.Message);
        Assert.Empty(repository.List());

        var order = service.Create("PO-MAX-QUANTITY-OK", supplier.Id, product.Id, Today, 5m, 10m);
        Assert.Equal(5m, order.Quantity);
    }

    [Fact]
    public void PurchaseOrderCreate_PreservesSourceMetadata()
    {
        var supplier = new Supplier("SUP-SOURCE", "来源供应商", null, null, null);
        var product = new Product("SKU-SOURCE", "来源商品", "件", 10m, null);
        var repository = new PurchaseOrderRepository();
        var service = new PurchaseOrderService(repository, new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());

        var order = service.Create("PO-SOURCE-SERVICE", supplier.Id, product.Id, Today, 2m, 12m, PurchaseOrderSourceKind.Requisition, "REQ-2026-001");

        Assert.Equal(PurchaseOrderSourceKind.Requisition, order.SourceKind);
        Assert.Equal("REQ-2026-001", order.SourceDocumentNo);
        Assert.Same(order, Assert.Single(repository.List()));
    }

    [Fact]
    public void PurchaseOrderCreate_RejectsDuplicateActiveSourceDocumentButAllowsCancelledRetry()
    {
        var supplier = new Supplier("SUP-SOURCE-DUP", "来源防重供应商", null, null, null);
        var product = new Product("SKU-SOURCE-DUP", "来源防重商品", "件", 10m, null);
        var existing = new PurchaseOrder("PO-SOURCE-DUP-01", supplier.Id, product.Id, Today, 1m, 10m, PurchaseOrderSourceKind.Contract, "SC-DUP-2026");
        var repository = new PurchaseOrderRepository(existing);
        var service = new PurchaseOrderService(repository, new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());

        var error = Assert.Throws<InvalidOperationException>(() => service.Create("PO-SOURCE-DUP-02", supplier.Id, product.Id, Today, 2m, 12m, PurchaseOrderSourceKind.Contract, "SC-DUP-2026"));
        Assert.Equal("来源单据已生成采购订单，不能重复生单；如需重试请先取消原采购订单。", error.Message);

        existing.SetStatus(PurchaseOrderStatus.Cancelled);
        var retry = service.Create("PO-SOURCE-DUP-03", supplier.Id, product.Id, Today, 2m, 12m, PurchaseOrderSourceKind.Contract, "SC-DUP-2026");
        Assert.Equal(PurchaseOrderSourceKind.Contract, retry.SourceKind);
        Assert.Equal(2, repository.List().Count);
    }

    [Fact]
    public void PurchaseOrderList_SearchesSourceDocumentNo()
    {
        var supplier = new Supplier("SUP-SOURCE-SEARCH", "来源检索供应商", null, null, null);
        var product = new Product("SKU-SOURCE-SEARCH", "来源检索商品", "件", 10m, null);
        var order = new PurchaseOrder("PO-SOURCE-SEARCH", supplier.Id, product.Id, Today, 1m, 10m, PurchaseOrderSourceKind.Contract, "SC-SEARCH-2026");
        var service = new PurchaseOrderService(new PurchaseOrderRepository(order), new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());

        var result = service.List("SC-SEARCH");

        Assert.Same(order, Assert.Single(result));
    }

    [Fact]
    public void PurchaseOrderList_FiltersSourceKind()
    {
        var supplier = new Supplier("SUP-SOURCE-FILTER", "来源筛选供应商", null, null, null);
        var product = new Product("SKU-SOURCE-FILTER", "来源筛选商品", "件", 10m, null);
        var contractOrder = new PurchaseOrder("PO-SOURCE-CONTRACT", supplier.Id, product.Id, Today, 1m, 10m, PurchaseOrderSourceKind.Contract, "SC-FILTER-01");
        var requisitionOrder = new PurchaseOrder("PO-SOURCE-REQUISITION", supplier.Id, product.Id, Today, 1m, 10m, PurchaseOrderSourceKind.Requisition, "REQ-FILTER-01");
        var service = new PurchaseOrderService(new PurchaseOrderRepository(contractOrder, requisitionOrder), new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());

        var result = service.List(sourceKind: PurchaseOrderSourceKind.Contract);

        Assert.Same(contractOrder, Assert.Single(result));
    }

    [Fact]
    public void LockedPurchaseOrderCannotAdvanceUntilUnlocked()
    {
        var supplier = new Supplier("SUP-LOCK-GUARD", "锁定供应商", null, null, null);
        var product = new Product("SKU-LOCK-GUARD", "锁定商品", "件", 10m, null);
        var order = new PurchaseOrder("PO-LOCK-GUARD", supplier.Id, product.Id, Today, 1m, 10m);
        var service = new PurchaseOrderService(new PurchaseOrderRepository(order), new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository());

        service.SetLocked(order, true);

        Assert.True(order.IsLocked);
        Assert.Throws<InvalidOperationException>(() => service.SetStatus(order, PurchaseOrderStatus.Submitted));

        service.SetLocked(order, false);
        service.SetStatus(order, PurchaseOrderStatus.Submitted);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
    }

    [Fact]
    public void ReceivedPurchaseOrderCannotBeLocked()
    {
        var order = PurchaseOrder.Restore(Guid.CreateVersion7(), "PO-LOCK-RECEIVED", Guid.CreateVersion7(), Guid.CreateVersion7(), Today, 1m, 10m, PurchaseOrderStatus.Received);

        Assert.Throws<InvalidOperationException>(() => order.SetLocked(true));
        Assert.False(order.IsLocked);
    }

    [Fact]
    public void ExistingOrdersRemainQueryableAfterPartyIsDeactivated()
    {
        var customer = new Customer("历史客户");
        var product = new Product("SKU-HISTORY-LIFECYCLE", "历史商品", "件", 10m, null);
        var order = new SalesOrder("SO-HISTORY-LIFECYCLE", customer.Id, product.Id, Today, 1m, 10m);
        var repository = new SalesOrderRepository(order);
        var service = new SalesOrderService(repository, new CustomerRepository(customer), new ProductRepository(product), null!, null!, new SalesContractService(new ContractRepository()), new SettlementRepository());

        customer.SetActive(false);

        Assert.Same(order, Assert.Single(service.List("SO-HISTORY-LIFECYCLE")));
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private sealed class SalesOrderRepository(params SalesOrder[] items) : ISalesOrderRepository
    {
        private readonly List<SalesOrder> data = [.. items];
        public IReadOnlyList<SalesOrder> List() => data;
        public void Add(SalesOrder item) => data.Add(item);
        public void Update(SalesOrder item) { }
    }

    private sealed class PurchaseOrderRepository(params PurchaseOrder[] items) : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> data = [.. items];
        public IReadOnlyList<PurchaseOrder> List() => data;
        public void Add(PurchaseOrder item) => data.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class CustomerRepository(params Customer[] items) : ICustomerRepository
    {
        public IReadOnlyList<Customer> List() => items;
        public void Add(Customer item) { }
        public void Update(Customer item) { }
        public void Remove(Guid id) { }
    }

    private sealed class SupplierRepository(params Supplier[] items) : ISupplierRepository
    {
        public IReadOnlyList<Supplier> List() => items;
        public void Add(Supplier item) { }
        public void Update(Supplier item) { }
        public void Remove(Guid id) { }
    }

    private sealed class ProductRepository(params Product[] items) : IProductRepository
    {
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) { }
        public void Update(Product item) { }
        public void Remove(Guid id) { }
    }

    private sealed class ContractRepository : ISalesContractRepository
    {
        public IReadOnlyList<SalesContract> List() => [];
        public void Add(SalesContract item) { }
        public void Update(SalesContract item) { }
        public void Remove(Guid id) { }
    }

    private sealed class SettlementRepository : ISettlementRepository
    {
        public IReadOnlyList<ErpSettlement> List() => [];
        public void Add(ErpSettlement item) { }
        public void Update(ErpSettlement item) { }
    }
}
