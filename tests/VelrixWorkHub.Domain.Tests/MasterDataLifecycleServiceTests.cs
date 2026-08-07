using VelrixWorkHub.Application.Contacts;
using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Application.FollowUps;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MasterDataLifecycleServiceTests
{
    [Fact]
    public void CustomerService_RejectsRemovalWhenHistoryExists()
    {
        var customer = new Customer("不可删除客户");
        var product = new Product("SKU-LIFECYCLE-CUSTOMER", "生命周期商品", "件", 1m, null);
        var order = new SalesOrder("SO-LIFECYCLE-CUSTOMER", customer.Id, product.Id, DateOnly.FromDateTime(DateTime.Today), 1m, 10m);
        var customerRepository = new CustomerRepository(customer);
        var service = new CustomerService(
            customerRepository,
            new ContactRepository(),
            new FollowUpRepository(),
            new ContractRepository(),
            new SalesOrderRepository(order),
            new ProjectRepository(),
            new SettlementRepository());

        var error = Assert.Throws<InvalidOperationException>(() => service.Remove(customer));

        Assert.Contains("不能删除", error.Message);
        Assert.Single(customerRepository.List());
    }

    [Fact]
    public void SupplierService_RejectsRemovalWhenHistoryExists()
    {
        var supplier = new Supplier("SUP-LIFECYCLE", "可删除供应商", null, null, null);
        var product = new Product("SKU-LIFECYCLE-SUPPLIER", "供应商生命周期商品", "件", 1m, null);
        var repository = new SupplierRepository(supplier);
        var service = new SupplierService(repository, new PurchaseOrderRepository(new PurchaseOrder("PO-LIFECYCLE-SUPPLIER", supplier.Id, product.Id, DateOnly.FromDateTime(DateTime.Today), 1m, 10m)), new SettlementRepository());

        var error = Assert.Throws<InvalidOperationException>(() => service.Remove(supplier));

        Assert.Contains("不能删除", error.Message);
        Assert.Single(repository.List());
    }

    [Fact]
    public void SupplierService_AllowsRemovalWhenNoHistoryExists()
    {
        var supplier = new Supplier("SUP-LIFECYCLE-EMPTY", "可删除供应商", null, null, null);
        var repository = new SupplierRepository(supplier);
        var service = new SupplierService(repository, new PurchaseOrderRepository(), new SettlementRepository());

        service.Remove(supplier);

        Assert.Empty(repository.List());
    }

    [Fact]
    public void ProductService_RejectsRemovalWhenInventoryHistoryExists()
    {
        var product = new Product("SKU-PRODUCT-GUARD", "商品删除保护", "件", 1m, null);
        var warehouse = new Warehouse("WH-PRODUCT-GUARD", "商品保护仓", null);
        var transaction = new InventoryTransaction(product.Id, warehouse.Id, InventoryTransactionKind.Inbound, 2m, "INV-PRODUCT-GUARD", DateOnly.FromDateTime(DateTime.Today), null);
        var repository = new ProductRepository(product);
        var service = new ProductService(repository, new PurchaseOrderRepository(), new SalesOrderRepository(), new InventoryRepository(transaction));

        var error = Assert.Throws<InvalidOperationException>(() => service.Remove(product));

        Assert.Contains("不能删除", error.Message);
        Assert.Single(repository.List());
    }

    [Fact]
    public void WarehouseService_RejectsRemovalWhenInventoryHistoryExists()
    {
        var product = new Product("SKU-WAREHOUSE-GUARD", "仓库删除保护商品", "件", 1m, null);
        var warehouse = new Warehouse("WH-WAREHOUSE-GUARD", "仓库删除保护", null);
        var transaction = new InventoryTransaction(product.Id, warehouse.Id, InventoryTransactionKind.Inbound, 2m, "INV-WAREHOUSE-GUARD", DateOnly.FromDateTime(DateTime.Today), null);
        var repository = new WarehouseRepository(warehouse);
        var service = new WarehouseService(repository, new InventoryRepository(transaction));

        var error = Assert.Throws<InvalidOperationException>(() => service.Remove(warehouse));

        Assert.Contains("不能删除", error.Message);
        Assert.Single(repository.List());
    }

    private sealed class CustomerRepository(params Customer[] items) : ICustomerRepository
    {
        private readonly List<Customer> data = [.. items];
        public IReadOnlyList<Customer> List() => data;
        public void Add(Customer item) => data.Add(item);
        public void Update(Customer item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class SupplierRepository(params Supplier[] items) : ISupplierRepository
    {
        private readonly List<Supplier> data = [.. items];
        public IReadOnlyList<Supplier> List() => data;
        public void Add(Supplier item) => data.Add(item);
        public void Update(Supplier item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class ProductRepository(params Product[] items) : IProductRepository
    {
        private readonly List<Product> data = [.. items];
        public IReadOnlyList<Product> List() => data;
        public void Add(Product item) => data.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class WarehouseRepository(params Warehouse[] items) : IWarehouseRepository
    {
        private readonly List<Warehouse> data = [.. items];
        public IReadOnlyList<Warehouse> List() => data;
        public void Add(Warehouse item) => data.Add(item);
        public void Update(Warehouse item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
        public void AddLocation(WarehouseLocation item) { }
        public void RemoveLocation(Guid id) { }
        public void UpsertLocationProductCapacity(WarehouseLocationProductCapacity item) { }
        public void RemoveLocationProductCapacity(Guid locationId, Guid productId) { }
    }

    private sealed class ContactRepository : ICustomerContactRepository
    {
        public IReadOnlyList<CustomerContact> List() => [];
        public void Add(CustomerContact item) { }
        public void Update(CustomerContact item) { }
        public void ClearPrimary(Guid customerId, Guid exceptId) { }
        public void Remove(Guid id) { }
    }

    private sealed class FollowUpRepository : ICustomerFollowUpRepository
    {
        public IReadOnlyList<CustomerFollowUp> List() => [];
        public void Add(CustomerFollowUp item) { }
        public void Remove(Guid id) { }
    }

    private sealed class ContractRepository : ISalesContractRepository
    {
        public IReadOnlyList<SalesContract> List() => [];
        public void Add(SalesContract item) { }
        public void Update(SalesContract item) { }
        public void Remove(Guid id) { }
    }

    private sealed class SalesOrderRepository(params SalesOrder[] items) : ISalesOrderRepository
    {
        private readonly List<SalesOrder> data = [.. items];
        public IReadOnlyList<SalesOrder> List() => data;
        public void Add(SalesOrder item) => data.Add(item);
        public void Update(SalesOrder item) { }
    }

    private sealed class ProjectRepository : IPmsProjectRepository
    {
        public IReadOnlyList<PmsProject> List() => [];
        public void Add(PmsProject item) { }
        public void Update(PmsProject item) { }
        public void Remove(Guid id) { }
    }

    private sealed class PurchaseOrderRepository(params PurchaseOrder[] items) : IPurchaseOrderRepository
    {
        private readonly List<PurchaseOrder> data = [.. items];
        public IReadOnlyList<PurchaseOrder> List() => data;
        public void Add(PurchaseOrder item) => data.Add(item);
        public void Update(PurchaseOrder item) { }
    }

    private sealed class InventoryRepository(params InventoryTransaction[] items) : IInventoryTransactionRepository
    {
        private readonly List<InventoryTransaction> data = [.. items];
        public IReadOnlyList<InventoryTransaction> List() => data;
        public void Add(InventoryTransaction item) => data.Add(item);
    }

    private sealed class SettlementRepository(params ErpSettlement[] items) : ISettlementRepository
    {
        private readonly List<ErpSettlement> data = [.. items];
        public IReadOnlyList<ErpSettlement> List() => data;
        public void Add(ErpSettlement item) => data.Add(item);
        public void Update(ErpSettlement item) { }
    }
}
