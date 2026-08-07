namespace VelrixWorkHub.Domain;

public enum PmsProjectStatus { Draft, Active, OnHold, Completed, Cancelled }
public enum PmsProjectInitiationMode { PreInitiation, FormalInitiation }

public sealed class PmsProject
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public string? ManagerName { get; private set; }
    public PmsProjectInitiationMode InitiationMode { get; private set; }
    public string? ProjectAlias { get; private set; }
    public string? ProjectChineseName { get; private set; }
    public string? ProjectEnglishName { get; private set; }
    public string? ProductName { get; private set; }
    public string? ProjectStage { get; private set; }
    public string? ProductLine { get; private set; }
    public string? ProjectCategory { get; private set; }
    public string? ProjectSubcategory { get; private set; }
    public string? ProjectSubcategoryCode { get; private set; }
    public string? VersionType { get; private set; }
    public string? ProjectVersion { get; private set; }
    public DateOnly? ExpectedInitiationDate { get; private set; }
    public DateOnly? ActualInitiationDate { get; private set; }
    public string? DevelopmentMode { get; private set; }
    public string? DepartmentName { get; private set; }
    public string? DomainManagerName { get; private set; }
    public string? BusinessInitiatorName { get; private set; }
    public string? Overview { get; private set; }
    public string? Objective { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public DateOnly PlannedStart { get; private set; }
    public DateOnly PlannedEnd { get; private set; }
    public int PercentComplete { get; private set; }
    public PmsProjectStatus Status { get; private set; }

    public PmsProject(string code, string name, Guid? customerId, string? managerName, DateOnly plannedStart, DateOnly plannedEnd)
        : this(code, name, customerId, managerName, plannedStart, plannedEnd, PmsProjectInitiationMode.PreInitiation, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
    {
    }

    public PmsProject(string code, string name, Guid? customerId, string? managerName, DateOnly plannedStart, DateOnly plannedEnd,
        PmsProjectInitiationMode initiationMode, string? projectAlias, string? projectChineseName, string? projectEnglishName,
        string? productName, string? projectStage, string? productLine, string? projectCategory, string? projectSubcategory,
        string? projectSubcategoryCode, string? versionType, string? projectVersion, DateOnly? expectedInitiationDate,
        DateOnly? actualInitiationDate, string? developmentMode, string? departmentName, string? domainManagerName,
        string? businessInitiatorName, string? overview, string? objective, string? otherInfo)
    {
        Edit(code, name, customerId, managerName, plannedStart, plannedEnd);
        EditDetails(initiationMode, projectAlias, projectChineseName, projectEnglishName, productName, projectStage, productLine, projectCategory, projectSubcategory, projectSubcategoryCode, versionType, projectVersion, expectedInitiationDate, actualInitiationDate, developmentMode, departmentName, domainManagerName, businessInitiatorName, overview, objective, otherInfo);
        Status = PmsProjectStatus.Draft;
    }

    public void Edit(string code, string name, Guid? customerId, string? managerName, DateOnly plannedStart, DateOnly plannedEnd)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("项目编号不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("项目名称不能为空。", nameof(name));
        if (plannedEnd < plannedStart) throw new ArgumentException("计划结束日期不能早于开始日期。", nameof(plannedEnd));
        Code = code.Trim(); Name = name.Trim(); CustomerId = customerId; ManagerName = string.IsNullOrWhiteSpace(managerName) ? null : managerName.Trim(); PlannedStart = plannedStart; PlannedEnd = plannedEnd;
    }

    public void EditDetails(PmsProjectInitiationMode initiationMode, string? projectAlias, string? projectChineseName, string? projectEnglishName,
        string? productName, string? projectStage, string? productLine, string? projectCategory, string? projectSubcategory,
        string? projectSubcategoryCode, string? versionType, string? projectVersion, DateOnly? expectedInitiationDate,
        DateOnly? actualInitiationDate, string? developmentMode, string? departmentName, string? domainManagerName,
        string? businessInitiatorName, string? overview, string? objective, string? otherInfo)
    {
        if (actualInitiationDate is DateOnly actual && expectedInitiationDate is DateOnly expected && actual < expected)
            throw new ArgumentException("实际立项日期不能早于预计立项日期。", nameof(actualInitiationDate));
        InitiationMode = initiationMode;
        ProjectAlias = Clean(projectAlias); ProjectChineseName = Clean(projectChineseName); ProjectEnglishName = Clean(projectEnglishName);
        ProductName = Clean(productName); ProjectStage = Clean(projectStage); ProductLine = Clean(productLine);
        ProjectCategory = Clean(projectCategory); ProjectSubcategory = Clean(projectSubcategory); ProjectSubcategoryCode = Clean(projectSubcategoryCode);
        VersionType = Clean(versionType); ProjectVersion = Clean(projectVersion); ExpectedInitiationDate = expectedInitiationDate; ActualInitiationDate = actualInitiationDate;
        DevelopmentMode = Clean(developmentMode); DepartmentName = Clean(departmentName); DomainManagerName = Clean(domainManagerName);
        BusinessInitiatorName = Clean(businessInitiatorName); Overview = Clean(overview); Objective = Clean(objective);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }
    public void SetStatus(PmsProjectStatus status) => Status = status;
    public void SetPercentComplete(int percent)
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent), "完成百分比必须在 0 到 100 之间。");
        PercentComplete = percent;
        if (percent == 100 && Status == PmsProjectStatus.Active) Status = PmsProjectStatus.Completed;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PmsProjectStatusHistory
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public PmsProjectStatus FromStatus { get; private set; }
    public PmsProjectStatus ToStatus { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ActorName { get; private set; } = string.Empty;
    public DateTime ChangedAt { get; private set; }

    public PmsProjectStatusHistory(Guid projectId, PmsProjectStatus fromStatus, PmsProjectStatus toStatus, string reason, string actorName, DateTime changedAt)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("项目不能为空。", nameof(projectId));
        if (fromStatus == toStatus) throw new ArgumentException("状态没有发生变化。", nameof(toStatus));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("状态变更说明不能为空。", nameof(reason));
        if (string.IsNullOrWhiteSpace(actorName)) throw new ArgumentException("操作者不能为空。", nameof(actorName));
        ProjectId = projectId; FromStatus = fromStatus; ToStatus = toStatus; Reason = reason.Trim(); ActorName = actorName.Trim(); ChangedAt = changedAt;
    }
}
