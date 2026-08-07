using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsRequirementServiceTests
{
    [Fact]
    public void RequirementService_ValidatesDatesAndUniqueNumber()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-REQ", "需求项目", null, null, today, today.AddDays(30));
        var repository = new RequirementRepository();
        var service = new PmsRequirementService(repository, new ProjectRepository(project));

        var item = service.Create(project.Id, null, null, " REQ-001 ", true, " 提出人 ", PmsRequirementPriority.High, PmsRequirementType.Functional, today, today.AddDays(7), today.AddDays(14), " 描述 ", " 背景价值 ", " 负责人 ", "{\"来源\":\"客户\"}");

        Assert.Equal("REQ-001", item.RequirementNo);
        Assert.Equal("提出人", item.Proposer);
        Assert.Equal("{\"来源\":\"客户\"}", item.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, null, null, "REQ-001", false, "提出人", PmsRequirementPriority.Low, PmsRequirementType.Other, today, null, null, "重复", null, null, "{}"));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, null, null, "REQ-002", false, "提出人", PmsRequirementPriority.Low, PmsRequirementType.Other, today, today.AddDays(-1), null, "日期错误", null, null, "{}"));
    }

    [Fact]
    public void RequirementStatus_UsesSequentialWorkflow()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var item = new PmsRequirement(Guid.CreateVersion7(), null, null, "REQ-STATUS", false, "提出人", PmsRequirementPriority.Medium, PmsRequirementType.Change, today, null, null, "状态流转", null, null, "{}");

        item.SetStatus(PmsRequirementStatus.Submitted);
        item.SetStatus(PmsRequirementStatus.Planned);
        item.SetStatus(PmsRequirementStatus.InProgress);
        item.SetStatus(PmsRequirementStatus.Completed);
        item.SetStatus(PmsRequirementStatus.Closed);

        Assert.Equal(PmsRequirementStatus.Closed, item.Status);
        Assert.Throws<InvalidOperationException>(() => item.SetStatus(PmsRequirementStatus.Draft));
    }

    private sealed class ProjectRepository(params PmsProject[] items) : IPmsProjectRepository
    {
        private readonly List<PmsProject> data = [.. items];
        public IReadOnlyList<PmsProject> List() => data;
        public void Add(PmsProject item) => data.Add(item);
        public void Update(PmsProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class RequirementRepository(params PmsRequirement[] items) : IPmsRequirementRepository
    {
        private readonly List<PmsRequirement> data = [.. items];
        public IReadOnlyList<PmsRequirement> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsRequirement item) => data.Add(item);
        public void Update(PmsRequirement item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
