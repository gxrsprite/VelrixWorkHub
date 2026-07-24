using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpRequirementServiceTests
{
    [Fact]
    public void RequirementService_ValidatesDatesAndUniqueNumber()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-REQ", "需求项目", null, null, today, today.AddDays(30));
        var repository = new RequirementRepository();
        var service = new PmpRequirementService(repository, new ProjectRepository(project));

        var item = service.Create(project.Id, null, null, " REQ-001 ", true, " 提出人 ", PmpRequirementPriority.High, PmpRequirementType.Functional, today, today.AddDays(7), today.AddDays(14), " 描述 ", " 背景价值 ", " 负责人 ", "{\"来源\":\"客户\"}");

        Assert.Equal("REQ-001", item.RequirementNo);
        Assert.Equal("提出人", item.Proposer);
        Assert.Equal("{\"来源\":\"客户\"}", item.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, null, null, "REQ-001", false, "提出人", PmpRequirementPriority.Low, PmpRequirementType.Other, today, null, null, "重复", null, null, "{}"));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, null, null, "REQ-002", false, "提出人", PmpRequirementPriority.Low, PmpRequirementType.Other, today, today.AddDays(-1), null, "日期错误", null, null, "{}"));
    }

    [Fact]
    public void RequirementStatus_UsesSequentialWorkflow()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var item = new PmpRequirement(Guid.CreateVersion7(), null, null, "REQ-STATUS", false, "提出人", PmpRequirementPriority.Medium, PmpRequirementType.Change, today, null, null, "状态流转", null, null, "{}");

        item.SetStatus(PmpRequirementStatus.Submitted);
        item.SetStatus(PmpRequirementStatus.Planned);
        item.SetStatus(PmpRequirementStatus.InProgress);
        item.SetStatus(PmpRequirementStatus.Completed);
        item.SetStatus(PmpRequirementStatus.Closed);

        Assert.Equal(PmpRequirementStatus.Closed, item.Status);
        Assert.Throws<InvalidOperationException>(() => item.SetStatus(PmpRequirementStatus.Draft));
    }

    private sealed class ProjectRepository(params PmpProject[] items) : IPmpProjectRepository
    {
        private readonly List<PmpProject> data = [.. items];
        public IReadOnlyList<PmpProject> List() => data;
        public void Add(PmpProject item) => data.Add(item);
        public void Update(PmpProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class RequirementRepository(params PmpRequirement[] items) : IPmpRequirementRepository
    {
        private readonly List<PmpRequirement> data = [.. items];
        public IReadOnlyList<PmpRequirement> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmpRequirement item) => data.Add(item);
        public void Update(PmpRequirement item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
