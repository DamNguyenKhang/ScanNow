using CloudinaryDotNet;
using Microsoft.Extensions.Configuration;
using ScanNow.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using CloudinaryDotNet.Actions;
using ScanNow.Domain.Abstractions.External;

namespace ScanNow.Infrastructure.External
{
    public class CloudinaryStorageService : IFileStorageService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryStorageService(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrEmpty(cloudName) ||
                string.IsNullOrEmpty(apiKey) ||
                string.IsNullOrEmpty(apiSecret))
            {
                throw new Exception("Cloudinary configuration is missing");
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<List<string>> UploadImageAsync(List<IFormFile> files)
        {
            if (files == null || !files.Any())
                throw new NotFoundException("File not found");

            var imageUrls = new List<string>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                await using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "ScanNow/images",
                    Transformation = new Transformation()
                        .Quality("auto")
                        .FetchFormat("auto")
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new ExternalServiceException("email", "errors");

                imageUrls.Add(result.SecureUrl.ToString());
            }

            return imageUrls;
        }
    }
}
