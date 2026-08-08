using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityInspectionStandardItemRepository
{
    IReadOnlyList<MomQualityInspectionStandardItem> List();
    void Add(MomQualityInspectionStandardItem item);
    void Update(MomQualityInspectionStandardItem item);
    void Remove(Guid id);
}
