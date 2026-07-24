using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Workflow;

public sealed class WorkflowDefinitionService(IWorkflowDefinitionRepository repository)
{
    private readonly object cacheLock = new();
    private IReadOnlyList<WorkflowDefinition>? immutableDefinitions;

    public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null)
    {
        var source = status is WorkflowDefinitionStatus.Published or WorkflowDefinitionStatus.Archived
            ? GetImmutableDefinitions().Where(x => status is null || x.Status == status.Value)
            : repository.List(code, status);
        return source
            .Where(x => string.IsNullOrWhiteSpace(code) || x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Code)
            .ThenByDescending(x => x.VersionNumber)
            .ToArray();
    }

    public WorkflowDefinition? GetVersion(string code, int versionNumber)
        => GetImmutableDefinitions().FirstOrDefault(x => x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase) && x.VersionNumber == versionNumber);

    public WorkflowDefinition CreateDraft(string code, string name, string? description = null, DateTime? createdAt = null)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var version = repository.List(code).Select(x => x.VersionNumber).DefaultIfEmpty(0).Max() + 1;
            var definition = new WorkflowDefinition(code, name, version, description, createdAt);
            if (repository.TryAdd(definition)) return definition;
        }

        throw new WorkflowDefinitionVersionConflictException(code);
    }

    public void SaveDraft(WorkflowDefinition definition)
    {
        if (definition.Status != WorkflowDefinitionStatus.Draft) throw new InvalidOperationException("只有草稿流程可以保存。");
        repository.Update(definition);
    }

    public void DeleteDraft(WorkflowDefinition definition)
    {
        if (definition.Status != WorkflowDefinitionStatus.Draft) throw new InvalidOperationException("只有草稿流程可以删除。");
        repository.Remove(definition.Id);
    }

    public void Publish(WorkflowDefinition definition, DateTime? publishedAt = null)
    {
        definition.Publish(publishedAt);
        repository.Update(definition);
        InvalidateImmutableDefinitions();
    }

    public void Archive(WorkflowDefinition definition)
    {
        definition.Archive();
        repository.Update(definition);
        InvalidateImmutableDefinitions();
    }

    private IReadOnlyList<WorkflowDefinition> GetImmutableDefinitions()
    {
        lock (cacheLock)
        {
            return immutableDefinitions ??= repository.List()
                .Where(x => x.Status is WorkflowDefinitionStatus.Published or WorkflowDefinitionStatus.Archived)
                .ToArray();
        }
    }

    private void InvalidateImmutableDefinitions()
    {
        lock (cacheLock) immutableDefinitions = null;
    }
}

public sealed class WorkflowDefinitionVersionConflictException(string code)
    : InvalidOperationException($"流程“{code}”的版本正在被其他请求创建，请重试。")
{
}
