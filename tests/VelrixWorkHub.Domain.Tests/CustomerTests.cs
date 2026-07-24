using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CustomerTests
{
    [Fact]
    public void NewCustomer_IsActiveAndTrimsOptionalValues()
    {
        var customer = new Customer(" Aster 科技 ", " 林经理 ", " 13800001234 ");

        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Equal("Aster 科技", customer.Name);
        Assert.Equal("林经理", customer.ContactName);
        Assert.Equal("13800001234", customer.Phone);
    }

    [Fact]
    public void BlankName_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Customer(" "));
    }

    [Fact]
    public void Customer_PreservesOtherInfoAndRejectsNonObjectJson()
    {
        var customer = new Customer("扩展客户", otherInfo: "{\"industry\":\"software\"}");

        Assert.Equal("{\"industry\":\"software\"}", customer.OtherInfo);
        Assert.Throws<ArgumentException>(() => new Customer("错误扩展客户", otherInfo: "[]"));
    }

    [Fact]
    public void ServiceSearchesAndChangesStatus()
    {
        var repository = new TestRepository();
        var service = new CustomerService(repository);
        var customer = service.Create("Aster 科技", "林经理", "13800001234", null, null);
        service.Create("Beta 贸易", "王经理", "13900005678", null, null);

        Assert.Single(service.List("林经理"));
        service.SetActive(customer, false);
        var activeCustomers = service.List(filter: CustomerFilter.Active);
        Assert.DoesNotContain(customer, activeCustomers);
        Assert.Single(activeCustomers);
        Assert.Single(service.List(filter: CustomerFilter.Inactive));
        Assert.Equal(1, repository.UpdatedCount);
    }

    private sealed class TestRepository : ICustomerRepository
    {
        private readonly List<Customer> items = [];
        public int UpdatedCount { get; private set; }
        public IReadOnlyList<Customer> List() => items;
        public void Add(Customer customer) => items.Add(customer);
        public void Update(Customer customer) => UpdatedCount++;
        public void Remove(Guid customerId) => items.RemoveAll(item => item.Id == customerId);
    }
}
