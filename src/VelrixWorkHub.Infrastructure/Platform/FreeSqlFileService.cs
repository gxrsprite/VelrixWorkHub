using BootstrapBlazor.Components;
using FreeSql;
using Microsoft.AspNetCore.Hosting;
using VelrixWorkHub.Application.Platform;

namespace VelrixWorkHub.Infrastructure.Platform;

public sealed class FreeSqlFileService : IFileService
{
    private readonly IBaseRepository<SysFile, Guid> _fileRepository;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public FreeSqlFileService(IBaseRepository<SysFile, Guid> fileRepository, IWebHostEnvironment webHostEnvironment)
    {
        _fileRepository = fileRepository;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<SysFile> UploadFileAsync(byte[] fileBytes, string fileName, string fileDirectory = "")
    {
        var safeDirectory = FileStoragePathPolicy.NormalizeUploadDirectory(fileDirectory);
        var safeFileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        var fileGuid = Guid.NewGuid();
        var saveFileName = fileGuid.ToString();
        var relativePath = Path.Combine(safeDirectory, saveFileName + extension).Replace('\\', '/');
        var webRoot = GetWebRoot();
        var fullDirectory = Path.Combine(webRoot, safeDirectory);
        Directory.CreateDirectory(fullDirectory);
        await File.WriteAllBytesAsync(Path.Combine(webRoot, relativePath), fileBytes);

        var fileEntity = new SysFile
        {
            FileGuid = fileGuid,
            OriginFileName = safeFileName,
            SaveFileName = saveFileName,
            Extension = extension,
            FileDirectory = safeDirectory,
            Size = fileBytes.Length,
            SizeFormat = FileSize.Format(fileBytes.Length),
            LinkUrl = "/" + relativePath,
            Sha256 = Sha256Encrypt.Encrypt(fileBytes),
        };
        return await _fileRepository.InsertAsync(fileEntity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var file = await _fileRepository.GetAsync(id);
        if (file == null)
            return;

        var webRoot = GetWebRoot();
        var directory = FileStoragePathPolicy.NormalizeUploadDirectory(file.FileDirectory);
        var saveFileName = Path.GetFileName(file.SaveFileName ?? string.Empty);
        var extension = Path.GetExtension(file.Extension ?? string.Empty);
        var filePath = Path.GetFullPath(Path.Combine(webRoot, directory, saveFileName + extension));
        if (!filePath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file path.");

        if (File.Exists(filePath))
            File.Delete(filePath);
        await _fileRepository.DeleteAsync(file.Id);
    }

    private string GetWebRoot()
    {
        return Path.GetFullPath(_webHostEnvironment.WebRootPath ?? "wwwroot")
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }
}
