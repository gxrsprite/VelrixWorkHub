namespace VelrixWorkHub.Domain;

public enum OaVehicleStatus
{
    Available,
    InUse,
    Maintenance,
    Retired
}

public enum OaVehicleUseRequestStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Cancelled,
    Returned
}

public sealed class OaVehicle
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string PlateNumber { get; private set; } = string.Empty;
    public string VehicleType { get; private set; } = string.Empty;
    public string BrandModel { get; private set; } = string.Empty;
    public int SeatCount { get; private set; }
    public Guid? ResponsibleUserId { get; private set; }
    public DateOnly? AnnualInspectionExpiresOn { get; private set; }
    public DateOnly? InsuranceExpiresOn { get; private set; }
    public OaVehicleStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }

    public OaVehicle(string plateNumber, string vehicleType, string brandModel, int seatCount, Guid? responsibleUserId,
        DateOnly? annualInspectionExpiresOn, DateOnly? insuranceExpiresOn, string? otherInfo, DateTime createdAt)
    {
        CreatedAt = createdAt;
        Edit(plateNumber, vehicleType, brandModel, seatCount, responsibleUserId, annualInspectionExpiresOn, insuranceExpiresOn, otherInfo);
        Status = OaVehicleStatus.Available;
    }

    public void Edit(string plateNumber, string vehicleType, string brandModel, int seatCount, Guid? responsibleUserId,
        DateOnly? annualInspectionExpiresOn, DateOnly? insuranceExpiresOn, string? otherInfo)
    {
        PlateNumber = Required(plateNumber, "车牌号");
        VehicleType = Required(vehicleType, "车辆类型");
        BrandModel = Required(brandModel, "品牌型号");
        if (seatCount <= 0) throw new ArgumentOutOfRangeException(nameof(seatCount), "座位数必须大于 0。");
        SeatCount = seatCount;
        ResponsibleUserId = responsibleUserId;
        AnnualInspectionExpiresOn = annualInspectionExpiresOn;
        InsuranceExpiresOn = insuranceExpiresOn;
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void MarkInUse()
    {
        if (Status != OaVehicleStatus.Available) throw new InvalidOperationException("只有可用车辆才能出车。");
        Status = OaVehicleStatus.InUse;
    }

    public void MarkAvailable()
    {
        if (Status != OaVehicleStatus.InUse) throw new InvalidOperationException("只有使用中的车辆才能归还。");
        Status = OaVehicleStatus.Available;
    }

    public void SetStatus(OaVehicleStatus status)
    {
        if (status == OaVehicleStatus.InUse) MarkInUse();
        else if (status == OaVehicleStatus.Available && Status == OaVehicleStatus.InUse) MarkAvailable();
        else if (Status == OaVehicleStatus.InUse) throw new InvalidOperationException("使用中的车辆必须先归还，不能直接维护或报废。");
        else Status = status;
    }

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}

public sealed class OaVehicleUseRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid VehicleId { get; private set; }
    public Guid ApplicantUserId { get; init; }
    public string ApplicantName { get; private set; } = string.Empty;
    public string DriverName { get; private set; } = string.Empty;
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public decimal? StartMileage { get; private set; }
    public decimal? EndMileage { get; private set; }
    public string Destination { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string OtherInfo { get; private set; } = "{}";
    public OaVehicleUseRequestStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? ReturnedAt { get; private set; }

    public OaVehicleUseRequest(Guid vehicleId, Guid applicantUserId, string applicantName, string driverName,
        DateTime startAt, DateTime endAt, decimal? startMileage, string destination, string purpose, string? otherInfo, DateTime createdAt)
    {
        if (vehicleId == Guid.Empty) throw new ArgumentException("车辆不能为空。", nameof(vehicleId));
        if (applicantUserId == Guid.Empty) throw new ArgumentException("申请人不能为空。", nameof(applicantUserId));
        VehicleId = vehicleId;
        ApplicantUserId = applicantUserId;
        CreatedAt = createdAt;
        Edit(applicantName, driverName, startAt, endAt, startMileage, destination, purpose, otherInfo);
        Status = OaVehicleUseRequestStatus.Draft;
    }

    public void Edit(string applicantName, string driverName, DateTime startAt, DateTime endAt, decimal? startMileage,
        string destination, string purpose, string? otherInfo)
    {
        ApplicantName = Required(applicantName, "申请人");
        DriverName = Required(driverName, "驾驶员");
        if (endAt <= startAt) throw new ArgumentException("结束时间必须晚于开始时间。", nameof(endAt));
        if (startMileage is < 0) throw new ArgumentOutOfRangeException(nameof(startMileage), "起始里程不能为负数。");
        Destination = Required(destination, "目的地");
        Purpose = Required(purpose, "用车事由");
        StartAt = startAt;
        EndAt = endAt;
        StartMileage = startMileage is null ? null : decimal.Round(startMileage.Value, 2);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void Submit(DateTime submittedAt)
    {
        if (Status is not (OaVehicleUseRequestStatus.Draft or OaVehicleUseRequestStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回用车申请才能提交。");
        Status = OaVehicleUseRequestStatus.Submitted;
        RejectionReason = null;
        SubmittedAt = submittedAt;
    }

    public void Approve()
    {
        if (Status != OaVehicleUseRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的用车申请才能批准。");
        Status = OaVehicleUseRequestStatus.Approved;
    }

    public void Reject(string? reason = null)
    {
        if (Status != OaVehicleUseRequestStatus.Submitted) throw new InvalidOperationException("只有已提交的用车申请才能驳回。");
        Status = OaVehicleUseRequestStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Cancel()
    {
        if (Status is not (OaVehicleUseRequestStatus.Draft or OaVehicleUseRequestStatus.Submitted)) throw new InvalidOperationException("当前状态不能撤回用车申请。");
        Status = OaVehicleUseRequestStatus.Cancelled;
    }

    public void Return(decimal? endMileage, DateTime returnedAt)
    {
        if (Status != OaVehicleUseRequestStatus.Approved) throw new InvalidOperationException("只有已批准的用车申请才能归还。");
        if (endMileage is < 0) throw new ArgumentOutOfRangeException(nameof(endMileage), "结束里程不能为负数。");
        if (StartMileage is decimal start && endMileage is decimal end && end < start) throw new InvalidOperationException("结束里程不能小于起始里程。");
        EndMileage = endMileage is null ? null : decimal.Round(endMileage.Value, 2);
        ReturnedAt = returnedAt;
        Status = OaVehicleUseRequestStatus.Returned;
    }

    public bool Overlaps(DateTime startAt, DateTime endAt) => StartAt < endAt && startAt < EndAt;
    public void SetVehicleForEdit(Guid vehicleId)
    {
        if (vehicleId == Guid.Empty) throw new ArgumentException("车辆不能为空。", nameof(vehicleId));
        VehicleId = vehicleId;
    }
    public void SetStatus(OaVehicleUseRequestStatus status) => Status = status;
    public void SetReturnData(decimal? endMileage, DateTime? returnedAt) { EndMileage = endMileage; ReturnedAt = returnedAt; }

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value.Trim();
}
