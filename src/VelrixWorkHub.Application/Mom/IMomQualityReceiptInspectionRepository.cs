using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomQualityReceiptInspectionRepository
{
    IReadOnlyList<MomQualityReceiptInspection> List();
    void Add(MomQualityReceiptInspection item);
}
