using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomWorkOrderOperationReportCorrectionRepository
{
    IReadOnlyList<MomWorkOrderOperationReportCorrection> List();
    void Add(MomWorkOrderOperationReportCorrection item);
}
