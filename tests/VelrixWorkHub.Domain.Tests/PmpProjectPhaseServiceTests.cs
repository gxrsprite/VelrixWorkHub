using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpProjectPhaseServiceTests
{
    [Fact]
    public void List_WithProjectId_ReturnsOnlyPhasesFromThatProject()
    {
        var first = new PmpProject("PRJ-001", "首个项目", null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var second = new PmpProject("PRJ-002", "另一个项目", null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var firstPhase = new PmpProjectPhase(first.Id, "需求与方案确认", PmpProjectPhaseKind.Phase, 1, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 7));
        var secondPhase = new PmpProjectPhase(second.Id, "另一个项目阶段", PmpProjectPhaseKind.Phase, 1, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 7));
        var service = new PmpProjectPhaseService(new PhaseRepository(firstPhase, secondPhase), new ProjectRepository(first, second));

        var result = service.List(first.Id);

        var only = Assert.Single(result);
        Assert.Equal(firstPhase.Id, only.Id);
        Assert.Equal("需求与方案确认", only.Name);
    }

    private sealed class PhaseRepository(params PmpProjectPhase[] items) : IPmpProjectPhaseRepository
    {
        private readonly List<PmpProjectPhase> data = [.. items];
        public IReadOnlyList<PmpProjectPhase> List(Guid? projectId = null) => projectId is null ? data : data.Where(x => x.ProjectId == projectId).ToArray();
        public void Add(PmpProjectPhase item) => data.Add(item);
        public void Update(PmpProjectPhase item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class ProjectRepository(params PmpProject[] items) : IPmpProjectRepository
    {
        private readonly List<PmpProject> data = [.. items];
        public IReadOnlyList<PmpProject> List() => data;
        public void Add(PmpProject item) => data.Add(item);
        public void Update(PmpProject item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }
}
