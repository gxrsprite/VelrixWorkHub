using VelrixWorkHub.Application.FollowUps;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CustomerFollowUpTests
{
    [Fact]
    public void BlankCustomerOrContent_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CustomerFollowUp(Guid.Empty, null, FollowUpType.Phone, "内容", null));
        Assert.Throws<ArgumentException>(() => new CustomerFollowUp(Guid.CreateVersion7(), null, FollowUpType.Phone, " ", null));
    }

    [Fact]
    public void ServiceFiltersUpcomingAndOverdue()
    {
        var repository = new TestRepository();
        var service = new CustomerFollowUpService(repository);
        service.Create(Guid.CreateVersion7(), null, FollowUpType.Phone, "未来跟进", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        service.Create(Guid.CreateVersion7(), null, FollowUpType.Visit, "逾期跟进", DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));

        Assert.Single(service.List(filter: FollowUpFilter.Upcoming));
        Assert.Single(service.List(filter: FollowUpFilter.Overdue));
    }

    private sealed class TestRepository : ICustomerFollowUpRepository
    {
        private readonly List<CustomerFollowUp> items = [];
        public IReadOnlyList<CustomerFollowUp> List() => items;
        public void Add(CustomerFollowUp followUp) => items.Add(followUp);
        public void Remove(Guid followUpId) => items.RemoveAll(item => item.Id == followUpId);
    }
}
