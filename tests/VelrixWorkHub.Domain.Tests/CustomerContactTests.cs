using VelrixWorkHub.Application.Contacts;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CustomerContactTests
{
    [Fact]
    public void BlankCustomerOrName_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CustomerContact(Guid.Empty, "联系人"));
        Assert.Throws<ArgumentException>(() => new CustomerContact(Guid.CreateVersion7(), " "));
    }

    [Fact]
    public void SetPrimary_ClearsPreviousPrimary()
    {
        var repository = new TestRepository();
        var service = new CustomerContactService(repository);
        var customerId = Guid.CreateVersion7();
        var first = service.Create(customerId, "林经理", null, null, null, true);
        var second = service.Create(customerId, "赵经理", null, null, null, false);

        service.SetPrimary(second);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Equal(2, repository.ClearCount);
    }

    private sealed class TestRepository : ICustomerContactRepository
    {
        private readonly List<CustomerContact> items = [];
        public int ClearCount { get; private set; }
        public IReadOnlyList<CustomerContact> List() => items;
        public void Add(CustomerContact contact) => items.Add(contact);
        public void Update(CustomerContact contact) { }
        public void ClearPrimary(Guid customerId, Guid exceptId) { foreach (var item in items.Where(item => item.CustomerId == customerId && item.Id != exceptId)) item.SetPrimary(false); ClearCount++; }
        public void Remove(Guid contactId) => items.RemoveAll(item => item.Id == contactId);
    }
}
