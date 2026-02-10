using Microsoft.AspNetCore.Http;

namespace ScanNow.Domain.Abstractions.External
{
    public interface IFileStorageService
    {
        Task<List<string>> UploadImageAsync(List<IFormFile> files);
    }
}
