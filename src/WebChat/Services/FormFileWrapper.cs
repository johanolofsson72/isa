using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;

namespace WebChat.Services;

/// <summary>
/// Wrapper to convert IBrowserFile to IFormFile for compatibility
/// </summary>
public class FormFileWrapper : IFormFile
{
    private readonly IBrowserFile _browserFile;

    public FormFileWrapper(IBrowserFile browserFile)
    {
        _browserFile = browserFile;
    }

    public string ContentType => _browserFile.ContentType;
    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{_browserFile.Name}\"";
    public IHeaderDictionary Headers => throw new NotImplementedException();
    public long Length => _browserFile.Size;
    public string Name => "file";
    public string FileName => _browserFile.Name;

    public Stream OpenReadStream()
    {
        return _browserFile.OpenReadStream();
    }

    public void CopyTo(Stream target)
    {
        using var stream = _browserFile.OpenReadStream();
        stream.CopyTo(target);
    }

    public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        using var stream = _browserFile.OpenReadStream();
        await stream.CopyToAsync(target, cancellationToken);
    }
}