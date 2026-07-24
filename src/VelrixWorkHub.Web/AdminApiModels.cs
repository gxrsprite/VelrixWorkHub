namespace AdminBlazor;

internal static class AdminApiModels
{
    internal record LoginRequest(string Username, string Password, bool Remember);
    internal record ChangePasswordRequest(string OldPassword, string NewPassword, string ConfirmPassword);
    internal record UpdateProfileRequest(string? Nickname);
    internal record HealthDto(string Status, DateTimeOffset UtcNow, string Service);
    internal record ReadinessDto(
        string Status,
        DateTimeOffset UtcNow,
        bool Database,
        int SchedulerTaskCount,
        int HolidayCount,
        int WorkdayOverrideCount,
        bool IsTodayWorkingDay);
    internal record SaveParamRequest(
        string? Id,
        string? Title,
        bool? Enabled,
        int Sort,
        string? Value,
        string? Value2,
        string? Value3,
        string? Value4,
        string? Value5,
        string? Value6,
        string? Value7,
        string? Description);
    internal record ProfileDto(Guid Id, string? Username, string? Nickname, bool IsEnabled, DateTime LoginTime, IReadOnlyList<ProfileRoleDto> Roles);
    internal record SessionDto(
        Guid Id,
        string? Username,
        string? Nickname,
        bool IsEnabled,
        DateTime LoginTime,
        IReadOnlyList<ProfileRoleDto> Roles,
        IReadOnlyList<string?> MenuPaths,
        IReadOnlyList<string> ButtonPaths);
    internal record ProfileRoleDto(Guid Id, string? Name, bool IsAdministrator);
    internal record MenuDto(Guid Id, string? Label, string? Icon, string? Path, int Sort, IReadOnlyList<MenuDto> Children);
    internal record RuntimeConfigDto(bool AutoSyncStructure, long MaxUploadBytes, string MaxUploadSize, PasswordPolicyDto PasswordPolicy, LoginAttemptLimiterDto LoginAttemptLimiter);
    internal record PasswordPolicyDto(int MinimumLength, int MaximumLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit);
    internal record LoginAttemptLimiterDto(int MaxFailures, int FailureWindowMinutes, int BlockDurationMinutes);
    internal record ParamDto(string? Id, string? Title, bool Enabled, int Sort, string? Value, string? Value2, string? Description, DateTime? ModifiedTime);
    internal record ParamDetailDto(
        string? Id,
        string? Title,
        bool Enabled,
        int Sort,
        string? Value,
        string? Value2,
        string? Value3,
        string? Value4,
        string? Value5,
        string? Value6,
        string? Value7,
        string? Description,
        DateTime? CreatedTime,
        DateTime? ModifiedTime);
    internal record LoginLogDto(Guid Id, string? Username, string Type, DateTime LoginTime, string? Ip, string? City, string? OS, string? Language, string? UserAgent);
    internal record DictCategoryDto(Guid Id, string? Name, string? Description, bool Enabled, int Sort);
    internal record DictItemDto(
        Guid Id,
        Guid ParentId,
        string? Name,
        string? Value,
        string? Value2,
        string? Value3,
        string? Value4,
        string? Value5,
        string? Description,
        bool Enabled,
        int Sort);
    internal record DictTreeDto(Guid Id, string? Name, string? Description, bool Enabled, int Sort, IReadOnlyList<DictItemDto> Items);
    internal record SchedulerTaskDto(string Name, string Cron, DateTime NextFireTime, bool Enabled, bool SkipHolidays);
    internal record NotificationFailureDto(Guid Id, string Operation, string Recipient, string DedupeKey, string Error, DateTime OccurredAt, int RetryCount, DateTime? LastRetryAt);
    internal record NotificationFailureBatchRetryRequest(IReadOnlyList<Guid>? Ids);
    internal record FileInfoDto(Guid Id, string? OriginFileName, string? Extension, long Size, string? SizeFormat, string? LinkUrl, DateTime? CreatedTime);
    internal record AdminApiResponse(bool Ok, int? Code, string? Error, object? Data, string? Message);
}
