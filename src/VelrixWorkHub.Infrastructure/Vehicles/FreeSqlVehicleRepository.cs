using FreeSql;
using VelrixWorkHub.Application.Vehicles;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Vehicles;

public sealed class FreeSqlVehicleRepository(IFreeSql fsql) : IOaVehicleRepository
{
    public IReadOnlyList<OaVehicle> List() => fsql.Select<OaVehicleRecord>().OrderBy(x => x.Status).ToList().OrderBy(x => x.PlateNumber).Select(ToDomain).ToArray();
    public OaVehicle? Get(Guid id) => fsql.Select<OaVehicleRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaVehicle vehicle) => fsql.Insert(ToRecord(vehicle)).ExecuteAffrows();
    public void Update(OaVehicle vehicle)
    {
        var rows = fsql.Update<OaVehicleRecord>().SetSource(ToRecord(vehicle)).Where(x => x.Id == vehicle.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("车辆不存在或已被删除。");
    }

    private static OaVehicle ToDomain(OaVehicleRecord x)
    {
        var vehicle = new OaVehicle(x.PlateNumber, x.VehicleType, x.BrandModel, x.SeatCount, x.ResponsibleUserId,
            x.AnnualInspectionExpiresOn is DateTime inspection ? DateOnly.FromDateTime(inspection) : null,
            x.InsuranceExpiresOn is DateTime insurance ? DateOnly.FromDateTime(insurance) : null, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        vehicle.SetStatus(x.Status);
        return vehicle;
    }

    private static OaVehicleRecord ToRecord(OaVehicle x) => new()
    {
        Id = x.Id, PlateNumber = x.PlateNumber, VehicleType = x.VehicleType, BrandModel = x.BrandModel, SeatCount = x.SeatCount,
        ResponsibleUserId = x.ResponsibleUserId, AnnualInspectionExpiresOn = x.AnnualInspectionExpiresOn?.ToDateTime(TimeOnly.MinValue),
        InsuranceExpiresOn = x.InsuranceExpiresOn?.ToDateTime(TimeOnly.MinValue), Status = x.Status, OtherInfo = x.OtherInfo, CreatedAt = x.CreatedAt
    };
}

public sealed class FreeSqlVehicleUseRequestRepository(IFreeSql fsql) : IOaVehicleUseRequestRepository
{
    public IReadOnlyList<OaVehicleUseRequest> List(Guid? applicantUserId = null, Guid? vehicleId = null)
    {
        var query = fsql.Select<OaVehicleUseRequestRecord>();
        if (applicantUserId is Guid applicant) query = query.Where(x => x.ApplicantUserId == applicant);
        if (vehicleId is Guid vehicle) query = query.Where(x => x.VehicleId == vehicle);
        return query.OrderByDescending(x => x.StartAt).ToList().Select(ToDomain).ToArray();
    }

    public OaVehicleUseRequest? Get(Guid id) => fsql.Select<OaVehicleUseRequestRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaVehicleUseRequest request) => fsql.Insert(ToRecord(request)).ExecuteAffrows();
    public void Update(OaVehicleUseRequest request)
    {
        var rows = fsql.Update<OaVehicleUseRequestRecord>().SetSource(ToRecord(request)).Where(x => x.Id == request.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("用车申请不存在或已被删除。");
    }

    private static OaVehicleUseRequest ToDomain(OaVehicleUseRequestRecord x)
    {
        var request = new OaVehicleUseRequest(x.VehicleId, x.ApplicantUserId, x.ApplicantName, x.DriverName, x.StartAt, x.EndAt,
            x.StartMileage, x.Destination, x.Purpose, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        switch (x.Status)
        {
            case OaVehicleUseRequestStatus.Submitted: request.Submit(x.SubmittedAt ?? x.CreatedAt); break;
            case OaVehicleUseRequestStatus.Rejected: request.Submit(x.SubmittedAt ?? x.CreatedAt); request.Reject(x.RejectionReason); break;
            case OaVehicleUseRequestStatus.Approved: request.Submit(x.SubmittedAt ?? x.CreatedAt); request.Approve(); break;
            case OaVehicleUseRequestStatus.Returned:
                request.Submit(x.SubmittedAt ?? x.CreatedAt); request.Approve(); request.Return(x.EndMileage, x.ReturnedAt ?? x.CreatedAt); break;
            case OaVehicleUseRequestStatus.Cancelled: request.Cancel(); break;
        }
        return request;
    }

    private static OaVehicleUseRequestRecord ToRecord(OaVehicleUseRequest x) => new()
    {
        Id = x.Id, VehicleId = x.VehicleId, ApplicantUserId = x.ApplicantUserId, ApplicantName = x.ApplicantName, DriverName = x.DriverName,
        StartAt = x.StartAt, EndAt = x.EndAt, StartMileage = x.StartMileage, EndMileage = x.EndMileage, Destination = x.Destination,
        Purpose = x.Purpose, OtherInfo = x.OtherInfo, Status = x.Status, RejectionReason = x.RejectionReason, CreatedAt = x.CreatedAt,
        SubmittedAt = x.SubmittedAt, ReturnedAt = x.ReturnedAt
    };
}
