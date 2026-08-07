using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

/// <summary>把项目变更的申请人字段暴露给通用流程审批人解析器。</summary>
public sealed class PmsProjectChangeWorkflowApproverSource(IPmsProjectChangeRepository repository) : IWorkflowBusinessApproverSource
{
    public bool CanHandle(WorkflowInstance instance) => instance.BusinessType.Equals(nameof(PmsProjectChange), StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> FindUsernames(WorkflowInstance instance, IReadOnlyCollection<string> fieldNames)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(fieldNames);
        var item = repository.List().FirstOrDefault(x => x.Id == instance.BusinessId)
            ?? throw new InvalidOperationException("流程关联的项目变更不存在或已被删除。");
        var result = new List<string>();
        foreach (var fieldName in fieldNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (fieldName.Equals(nameof(PmsProjectChange.RequesterName), StringComparison.OrdinalIgnoreCase))
                result.Add(item.RequesterName ?? string.Empty);
            else
                throw new InvalidOperationException($"项目变更不支持审批人业务字段“{fieldName}”。");
        }
        return result.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
