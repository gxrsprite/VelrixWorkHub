using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public interface IWorkflowDefinitionRepository
{
    IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null);
    void Add(WorkflowDefinition definition);
    bool TryAdd(WorkflowDefinition definition);
    void Update(WorkflowDefinition definition);
    void Remove(Guid id);
}
