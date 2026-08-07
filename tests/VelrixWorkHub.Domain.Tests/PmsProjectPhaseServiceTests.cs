using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsProjectPhaseServiceTests
{
    [Fact]
    public void List_WithProjectId_ReturnsOnlyPhasesFromThatProject()
    {
        var first = new PmsProject("PRJ-001", "首个项目", null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var second = new PmsProject("PRJ-002", "另一个项目", null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var firstPhase = new PmsProjectPhase(first.Id, "需求与方案确认", PmsProjectPhaseKind.Phase, 1, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 7));
        var secondPhase = new PmsProjectPhase(second.Id, "另一个项目阶段", PmsProjectPhaseKind.Phase, 1, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 7));
        var service = new PmsProjectPhaseService(new PhaseRepository(firstPhase, secondPhase), new ProjectRepository(first, second));

        var result = service.List(first.Id);

        var only = Assert.Single(result);
        Assert.Equal(firstPhase.Id, only.Id);
        Assert.Equal("需求与方案确认", only.Name);
    }

    private sealed class PhaseRepository(params PmsProjectPhase[] items) : IPmsProjectPhaseRepository
    {
        private readonly List<PmsProjectPhase> data = [.. items];
        public IReadOnlyList<PmsProjectPhase> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmsProjectPhase item) => data.Add(item);
        public void Update(PmsProjectPhase item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class ProjectRepository(params PmsProject[] items) : IPmsProjectRepository
    {
        private readonly List<PmsProject> data = [.. items];
        public IReadOnlyList<PmsProject> List() => data;
        public void Add(PmsProject item) => data.Add(item);
        public void Update(PmsProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
