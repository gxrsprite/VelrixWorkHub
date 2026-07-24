using VelrixWorkHub.Application.Attachments;

namespace VelrixWorkHub.Infrastructure.Attachments;

public sealed class LocalAttachmentContentStore(string contentRootPath) : IAttachmentContentStore
{
    private string Root => Path.Combine(contentRootPath, "App_Data", "attachments");

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.CreateVersion7():N}.tmp";
        try
        {
            await using (var target = File.Create(temporaryPath))
            {
                await content.CopyToAsync(target, cancellationToken);
                await target.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        Stream stream = File.OpenRead(Resolve(storageKey));
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(Root, normalized));
        var root = Path.GetFullPath(Root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("附件存储路径越界。");
        return path;
    }
}
