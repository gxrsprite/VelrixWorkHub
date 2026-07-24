using VelrixWorkHub.Application.Opportunities;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SalesOpportunityTests
{
    [Fact]
    public void StageCanAdvanceAndLostRequiresReason()
    {
        var item = new SalesOpportunity(Guid.CreateVersion7(), "升级项目", 10000m);
        item.MoveTo(OpportunityStage.Proposal);
        Assert.Equal(OpportunityStage.Proposal, item.Stage);
        Assert.Throws<ArgumentException>(() => item.MoveTo(OpportunityStage.Lost));
        item.MoveTo(OpportunityStage.Lost, "预算冻结");
        Assert.Equal("预算冻结", item.LostReason);
    }

    [Fact]
    public void ServiceFiltersOpenAndWon()
    {
        var repository = new TestRepository();
        var service = new SalesOpportunityService(repository);
        var open = service.Create(Guid.CreateVersion7(), "开放商机", 1m, null);
        var won = service.Create(Guid.CreateVersion7(), "赢单商机", 2m, null);
        service.MoveTo(won, OpportunityStage.Won);

        Assert.Contains(open, service.List(filter: OpportunityFilter.Open));
        Assert.DoesNotContain(won, service.List(filter: OpportunityFilter.Open));
        Assert.Single(service.List(filter: OpportunityFilter.Won));
    }

    private sealed class TestRepository : ISalesOpportunityRepository
    {
        private readonly List<SalesOpportunity> items = [];
        public IReadOnlyList<SalesOpportunity> List() => items;
        public void Add(SalesOpportunity opportunity) => items.Add(opportunity);
        public void Update(SalesOpportunity opportunity) { }
        public void Remove(Guid opportunityId) => items.RemoveAll(item => item.Id == opportunityId);
    }
}
