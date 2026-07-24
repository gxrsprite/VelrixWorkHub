using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public sealed class PmpRequirementService(IPmpRequirementRepository repository, IPmpProjectRepository projectRepository, IProductRepository? productRepository = null)
{
    public IReadOnlyList<PmpRequirement> List(Guid? projectId = null, string? keyword = null, PmpRequirementStatus? status = null, PmpRequirementPriority? priority = null, bool? highlighted = null)
    {
        var query = repository.List(projectId).AsEnumerable();
        var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.RequirementNo.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Proposer.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.OwnerName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        if (status is not null) query = query.Where(x => x.Status == status);
        if (priority is not null) query = query.Where(x => x.Priority == priority);
        if (highlighted is not null) query = query.Where(x => x.IsHighlighted == highlighted);
        return query.OrderByDescending(x => x.IsHighlighted).ThenByDescending(x => x.Priority).ThenBy(x => x.Status).ThenBy(x => x.DesiredCompletionDate).ToArray();
    }

    public PmpRequirement Create(Guid projectId, Guid? productId, Guid? baselineId, string requirementNo, bool isHighlighted, string proposer, PmpRequirementPriority priority, PmpRequirementType requirementType, DateOnly proposedDate, DateOnly? desiredCompletionDate, DateOnly? plannedCompletionDate, string description, string? backgroundValue, string? ownerName, string? otherInfo)
    {
        EnsureProject(projectId); EnsureProduct(productId);
        var item = new PmpRequirement(projectId, productId, baselineId, requirementNo, isHighlighted, proposer, priority, requirementType, proposedDate, desiredCompletionDate, plannedCompletionDate, description, backgroundValue, ownerName, otherInfo);
        EnsureUnique(item); repository.Add(item); return item;
    }

    public void Edit(PmpRequirement item, Guid? productId, Guid? baselineId, string requirementNo, bool isHighlighted, string proposer, PmpRequirementPriority priority, PmpRequirementType requirementType, DateOnly proposedDate, DateOnly? desiredCompletionDate, DateOnly? plannedCompletionDate, string description, string? backgroundValue, string? ownerName, string? otherInfo)
    {
        EnsureProject(item.ProjectId); EnsureProduct(productId); EnsureUnique(item.ProjectId, item.Id, requirementNo);
        item.Edit(item.ProjectId, productId, baselineId, requirementNo, isHighlighted, proposer, priority, requirementType, proposedDate, desiredCompletionDate, plannedCompletionDate, description, backgroundValue, ownerName, otherInfo); repository.Update(item);
    }

    public void SetStatus(PmpRequirement item, PmpRequirementStatus status) { item.SetStatus(status); repository.Update(item); }
    public void Remove(PmpRequirement item) => repository.Remove(item.Id);

    private PmpProject EnsureProject(Guid id) => projectRepository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联项目不存在。");
    private void EnsureProduct(Guid? id) { if (id is Guid productId && productRepository is not null && !productRepository.List().Any(x => x.Id == productId)) throw new InvalidOperationException("关联产品不存在。"); }
    private void EnsureUnique(PmpRequirement item) => EnsureUnique(item.ProjectId, item.Id, item.RequirementNo);
    private void EnsureUnique(Guid projectId, Guid currentId, string requirementNo) { if (repository.List(projectId).Any(x => x.Id != currentId && x.RequirementNo.Equals(requirementNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同一项目下需求编号已存在。"); }
}
