using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsProjectInitiationTests
{
    [Fact]
    public void DetailedProject_NormalizesFieldsAndJson()
    {
        var project = new PmsProject(" PRJ-INIT ", "立项项目", null, " 项目经理 ", new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1), PmsProjectInitiationMode.FormalInitiation, " 别称 ", " 中文名 ", " English ", " 产品 ", " 开发 ", " 产品线 ", " 软件 ", " 平台 ", " PLAT ", " 正式版 ", " V1.0 ", new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 3), " 自研 ", " 研发部 ", " 领域经理 ", " 业务发起方 ", " 项目概况 ", " 项目目标 ", "{\"行业\":\"制造\"}");

        Assert.Equal(PmsProjectInitiationMode.FormalInitiation, project.InitiationMode);
        Assert.Equal("别称", project.ProjectAlias);
        Assert.Equal("产品线", project.ProductLine);
        Assert.Equal("{\"行业\":\"制造\"}", project.OtherInfo);
        Assert.Throws<ArgumentException>(() => project.EditDetails(project.InitiationMode, null, null, null, null, null, null, null, null, null, null, null, new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 4), null, null, null, null, null, null, "{}"));
    }

    [Fact]
    public void StatusChange_UpdatesProjectAndAddsHistory()
    {
        var project = new PmsProject("PRJ-STATUS", "状态项目", null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 2));
        var repository = new InMemoryProjectRepository(project);
        var history = new InMemoryStatusHistoryRepository();
        var service = new PmsProjectService(repository, history);

        service.ChangeStatus(project, PmsProjectStatus.Active, "完成立项评审", "alice");

        Assert.Equal(PmsProjectStatus.Active, project.Status);
        var record = Assert.Single(history.Items);
        Assert.Equal(PmsProjectStatus.Draft, record.FromStatus);
        Assert.Equal("alice", record.ActorName);
        Assert.Throws<ArgumentException>(() => service.ChangeStatus(project, PmsProjectStatus.Active, "重复状态", "alice"));
    }

    private sealed class InMemoryProjectRepository(params PmsProject[] items) : IPmsProjectRepository
    {
        private readonly List<PmsProject> _items = [.. items];
        public IReadOnlyList<PmsProject> List() => _items;
        public void Add(PmsProject item) => _items.Add(item);
        public void Update(PmsProject item) { }
        public void Remove(Guid id) => _items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryStatusHistoryRepository : IPmsProjectStatusHistoryRepository
    {
        public List<PmsProjectStatusHistory> Items { get; } = [];
        public IReadOnlyList<PmsProjectStatusHistory> List(Guid projectId) => Items.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsProjectStatusHistory history) => Items.Add(history);
    }
}
