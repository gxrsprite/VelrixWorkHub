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

public sealed class OrderSettlementInvariantTests
{
    [Fact]
    public void SalesOrderWithActiveReceiptCannotBeCancelled()
    {
        var customer = new Customer("Aster 科技");
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var order = new SalesOrder("SO-GUARD-1", customer.Id, product.Id, Today, 1, 100);
        var settlement = new ErpSettlement("REC-GUARD-1", order.Id, customer.Id, ErpSettlementKind.Receivable, 50, Today);
        var service = new SalesOrderService(new SalesRepository(order), new CustomerRepository(customer), new ProductRepository(product), null!, null!, new SalesContractService(new ContractRepository()), new SettlementRepository(settlement));

        var error = Assert.Throws<InvalidOperationException>(() => service.SetStatus(order, SalesOrderStatus.Cancelled));

        Assert.Equal("销售订单已有有效收款核销，不能取消订单；请先撤销核销。", error.Message);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
    }

    [Fact]
    public void PurchaseOrderWithVoidedPaymentCanBeCancelled()
    {
        var supplier = new Supplier("SUP-GUARD-1", "华东供应链", null, null, null);
        var product = new Product("SKU-1", "标准包", "套", 10, null);
        var order = new PurchaseOrder("PO-GUARD-1", supplier.Id, product.Id, Today, 1, 100);
        var settlement = new ErpSettlement("PAY-GUARD-1", order.Id, supplier.Id, ErpSettlementKind.Payable, 50, Today);
        settlement.Void("测试撤销");
        var service = new PurchaseOrderService(new PurchaseRepository(order), new SupplierRepository(supplier), new ProductRepository(product), null!, null!, new SettlementRepository(settlement));

        service.SetStatus(order, PurchaseOrderStatus.Cancelled);

        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private sealed class SalesRepository(params SalesOrder[] items) : ISalesOrderRepository
    { public IReadOnlyList<SalesOrder> List() => items; public void Add(SalesOrder item) { } public void Update(SalesOrder item) { } }
    private sealed class PurchaseRepository(params PurchaseOrder[] items) : IPurchaseOrderRepository
    { public IReadOnlyList<PurchaseOrder> List() => items; public void Add(PurchaseOrder item) { } public void Update(PurchaseOrder item) { } }
    private sealed class CustomerRepository(params Customer[] items) : ICustomerRepository
    { public IReadOnlyList<Customer> List() => items; public void Add(Customer item) { } public void Update(Customer item) { } public void Remove(Guid id) { } }
    private sealed class SupplierRepository(params Supplier[] items) : ISupplierRepository
    { public IReadOnlyList<Supplier> List() => items; public void Add(Supplier item) { } public void Update(Supplier item) { } public void Remove(Guid id) { } }
    private sealed class ProductRepository(params Product[] items) : IProductRepository
    { public IReadOnlyList<Product> List() => items; public void Add(Product item) { } public void Update(Product item) { } public void Remove(Guid id) { } }
    private sealed class SettlementRepository(params ErpSettlement[] items) : ISettlementRepository
    { public IReadOnlyList<ErpSettlement> List() => items; public void Add(ErpSettlement item) { } public void Update(ErpSettlement item) { } }
    private sealed class ContractRepository : ISalesContractRepository
    { public IReadOnlyList<SalesContract> List() => []; public void Add(SalesContract item) { } public void Update(SalesContract item) { } public void Remove(Guid id) { } }
}
