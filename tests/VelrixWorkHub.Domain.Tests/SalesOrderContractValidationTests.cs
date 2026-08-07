using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SalesOrderContractValidationTests
{
    [Fact]
    public void Create_RejectsContractForAnotherCustomerOrNonActiveContract()
    {
        var customer = new Customer("Aster 科技");
        var anotherCustomer = new Customer("Beta 贸易");
        var product = new Product("SKU-CT", "合同服务", "件", 100m, null);
        var contractRepository = new ContractRepository();
        var contractService = new SalesContractService(contractRepository);
        var foreignContract = contractService.Create(anotherCustomer.Id, null, "CT-FOREIGN", "异客户合同", 100m, Today, Today.AddDays(1));
        contractService.Activate(foreignContract);
        var draftContract = contractService.Create(customer.Id, null, "CT-DRAFT", "草稿合同", 100m, Today, Today.AddDays(1));
        var service = new SalesOrderService(new OrderRepository(), new CustomerRepository(customer, anotherCustomer), new ProductRepository(product), null!, null!, contractService, null!);

        var customerMismatch = Assert.Throws<InvalidOperationException>(() => service.Create("SO-FOREIGN", customer.Id, product.Id, Today, 1, 100m, foreignContract.Id));
        var inactiveContract = Assert.Throws<InvalidOperationException>(() => service.Create("SO-DRAFT", customer.Id, product.Id, Today, 1, 100m, draftContract.Id));

        Assert.Equal("关联合同不属于所选客户。", customerMismatch.Message);
        Assert.Equal("只有生效合同可以关联销售订单。", inactiveContract.Message);
    }

    [Fact]
    public void Create_RejectsOrderDateOutsideActiveContractPeriod()
    {
        var customer = new Customer("Aster 科技");
        var product = new Product("SKU-CT-DATE", "合同服务", "件", 100m, null);
        var contractRepository = new ContractRepository();
        var contractService = new SalesContractService(contractRepository);
        var contract = contractService.Create(customer.Id, null, "CT-DATE", "期限合同", 100m, Today.AddDays(-5), Today.AddDays(-1));
        contractService.Activate(contract);
        var service = new SalesOrderService(new OrderRepository(), new CustomerRepository(customer), new ProductRepository(product), null!, null!, contractService, null!);

        var error = Assert.Throws<InvalidOperationException>(() => service.Create("SO-DATE", customer.Id, product.Id, Today, 1, 100m, contract.Id));

        Assert.Equal("销售订单日期不在合同有效期内。", error.Message);
    }

    [Fact]
    public void Create_ValidatesProjectCustomerAndStoresProjectReference()
    {
        var customer = new Customer("Aster 科技");
        var anotherCustomer = new Customer("Beta 贸易");
        var product = new Product("SKU-PMS", "项目服务", "件", 100m, null);
        var project = new PmsProject("PRJ-ORDER-01", "Aster 项目", customer.Id, "项目经理", Today, Today.AddDays(30));
        var foreignProject = new PmsProject("PRJ-ORDER-02", "Beta 项目", anotherCustomer.Id, "项目经理", Today, Today.AddDays(30));
        var service = new SalesOrderService(new OrderRepository(), new CustomerRepository(customer, anotherCustomer), new ProductRepository(product), null!, null!, new SalesContractService(new ContractRepository()), null!, new ProjectRepository(project, foreignProject));

        var order = service.Create("SO-PMS-01", customer.Id, product.Id, Today, 1, 100m, null, project.Id);
        var error = Assert.Throws<InvalidOperationException>(() => service.Create("SO-PMS-02", customer.Id, product.Id, Today, 1, 100m, null, foreignProject.Id));

        Assert.Equal(project.Id, order.PmsProjectId);
        Assert.Equal("关联项目不属于所选客户。", error.Message);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private sealed class OrderRepository : ISalesOrderRepository
    {
        public IReadOnlyList<SalesOrder> List() => [];
        public void Add(SalesOrder item) { }
        public void Update(SalesOrder item) { }
    }

    private sealed class CustomerRepository(params Customer[] items) : ICustomerRepository
    {
        public IReadOnlyList<Customer> List() => items;
        public void Add(Customer customer) { }
        public void Update(Customer customer) { }
        public void Remove(Guid customerId) { }
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
        private readonly List<SalesContract> items = [];
        public IReadOnlyList<SalesContract> List() => items;
        public void Add(SalesContract contract) => items.Add(contract);
        public void Update(SalesContract contract) { }
        public void Remove(Guid contractId) => items.RemoveAll(x => x.Id == contractId);
    }

    private sealed class ProjectRepository(params PmsProject[] items) : IPmsProjectRepository
    {
        public IReadOnlyList<PmsProject> List() => items;
        public void Add(PmsProject item) { }
        public void Update(PmsProject item) { }
        public void Remove(Guid id) { }
    }
}
