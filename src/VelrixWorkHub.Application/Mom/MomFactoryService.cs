using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public sealed class MomFactoryService(IMomFactoryRepository repository)
{
    public IReadOnlyList<MomFactory> List(MomMasterDataStatus? status = null)
    {
        var query = repository.List().AsEnumerable(); if (status is MomMasterDataStatus selected) query = query.Where(x => x.Status == selected);
        return query.OrderBy(x => x.Code).ToArray();
    }

    public MomFactory Create(string code, string name, string? otherInfo = null)
    {
        var item = new MomFactory(code, name, otherInfo); EnsureUnique(item); repository.Add(item); return item;
    }

    public void SetActive(MomFactory item, bool active) { item.SetActive(active); repository.Update(item); }
    private void EnsureUnique(MomFactory item) { if (repository.List().Any(x => x.Id != item.Id && x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("工厂编码已存在。"); }
}
