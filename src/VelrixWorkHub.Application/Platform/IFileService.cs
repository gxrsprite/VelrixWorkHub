using BootstrapBlazor.Components;

namespace VelrixWorkHub.Application.Platform;

/// <summary>
/// 文件存储用例契约。UI 和 HTTP 层只依赖该契约，不直接依赖文件系统或 FreeSql。
/// </summary>
public interface IFileService
{
    Task<SysFile> UploadFileAsync(byte[] fileBytes, string fileName, string fileDirectory = "");

    Task DeleteAsync(Guid id);
}

public static class FileStoragePathPolicy
{
    public static string NormalizeUploadDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return "uploads";

        var value = directory.Trim();
        if (Path.IsPathRooted(value))
            return "uploads";

        value = value.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(value) || value.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(value))
            return "uploads";

        var safeParts = value
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'))
            .ToArray();

        return safeParts.Length == 0 ? "uploads" : string.Join('/', safeParts);
    }
}
