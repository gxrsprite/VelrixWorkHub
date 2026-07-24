namespace VelrixWorkHub.Application.Platform;

public interface IPlatformCatalogService
{
    Task<IReadOnlyList<PlatformParameterSummary>> QueryParametersAsync(string? search, bool? enabled, int take);

    Task<PlatformParameterDetail?> GetParameterAsync(string id);

    Task<PlatformParameterSaveResult> SaveParameterAsync(
        string routeId,
        PlatformParameterRequest? request,
        Guid actorId,
        string? actorName);

    Task<bool> DeleteParameterAsync(string id);

    Task<IReadOnlyList<PlatformDictionaryCategory>> QueryDictionaryCategoriesAsync(bool? enabled);

    Task<PlatformDictionaryItemsResult> QueryDictionaryItemsAsync(
        Guid? categoryId,
        string? categoryName,
        bool? enabled);

    Task<IReadOnlyList<PlatformDictionaryTree>> QueryDictionaryTreeAsync(bool? enabled);
}

public sealed record PlatformParameterRequest(
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

public sealed record PlatformParameterSummary(
    string? Id,
    string? Title,
    bool Enabled,
    int Sort,
    string? Value,
    string? Value2,
    string? Description,
    DateTime? ModifiedTime);

public sealed record PlatformParameterDetail(
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

public sealed record PlatformParameterSaveResult(
    bool Success,
    bool Created,
    string? Error,
    PlatformParameterDetail? Value);

public sealed record PlatformDictionaryCategory(Guid Id, string? Name, string? Description, bool Enabled, int Sort);

public sealed record PlatformDictionaryItem(
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

public sealed record PlatformDictionaryTree(
    Guid Id,
    string? Name,
    string? Description,
    bool Enabled,
    int Sort,
    IReadOnlyList<PlatformDictionaryItem> Items);

public sealed record PlatformDictionaryItemsResult(
    IReadOnlyList<PlatformDictionaryItem>? Items,
    string? Error);
