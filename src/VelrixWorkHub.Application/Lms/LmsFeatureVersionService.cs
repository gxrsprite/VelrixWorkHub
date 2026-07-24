using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public sealed class LmsFeatureVersionService(
    ILmsFeatureVersionRepository repository,
    LmsFeatureService features)
{
    public IReadOnlyList<LmsFeatureVersion> List(Guid? featureId = null, bool includeDisabled = true) =>
        repository.List()
            .Where(x => featureId is null || x.FeatureId == featureId)
            .Where(x => includeDisabled || x.Status == LmsFeatureVersionStatus.Active)
            .OrderBy(x => x.FeatureId)
            .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public LmsFeatureVersion Create(
        Guid featureId,
        string version,
        LmsFeatureLevel level,
        LmsFeatureScope scope,
        string? otherInfo)
    {
        var feature = features.List().SingleOrDefault(x => x.Id == featureId)
            ?? throw new InvalidOperationException("许可证特性不存在。");
        if (feature.Status != LmsFeatureStatus.Active)
        {
            throw new InvalidOperationException("停用的许可证特性不能新建版本。");
        }

        var item = new LmsFeatureVersion(featureId, version, level, scope, otherInfo, DateTime.Now);
        if (repository.List().Any(x => x.FeatureId == featureId && x.Version.Equals(item.Version, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("该特性版本号已存在。");
        }

        repository.Add(item);
        return item;
    }

    public void SetStatus(LmsFeatureVersion item, LmsFeatureVersionStatus status)
    {
        item.SetStatus(status);
        repository.Update(item);
    }
}
