using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LapTrinhWindows.Repositories.Minio
{
    public interface IFileRepository
    {
        Task<string> UploadFileAsync(IFormFile file, string bucketName);
        Task<byte[]> DownloadFileAsync(string bucketName, string fileName);
        Task DeleteFileAsync(string bucketName, string fileName);
        Task UpdateFileAsync(IFormFile file, string bucketName, string existingFileName);
        Task<string> GetPresignedUrlAsync(string bucketName, string fileName, TimeSpan expiry); 
        Task<string> ConvertAndUploadAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize); 
        Task<string> GetStaticPublicFileUrl(string bucketName, string objectName);
        Task<string> ConvertAndUploadPublicFileAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize);
        
    }

    public class FileRepository : IFileRepository
    {
        private readonly IMinioClient _minioClient;
        private readonly string _minioPublicUrl;
        private readonly ILogger<FileRepository> _logger;

        public FileRepository(IMinioClient minioClient, IConfiguration configuration, ILogger<FileRepository> logger)
        {
            _minioClient = minioClient ?? throw new ArgumentNullException(nameof(minioClient));
            _minioPublicUrl = configuration["Minio:PublicUrl"] ?? throw new ArgumentNullException("Minio:PublicUrl is not configured");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> ConvertAndUploadAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize)
        {
            try
            {
                await EnsureBucketExists(bucketName);
                string jpgFileName = Path.ChangeExtension(fileName, ".jpg");

                if (fileStream.Length <= maxSize)
                {
                    using var outputStream = new MemoryStream();
                    using (var image = await Image.LoadAsync(fileStream))
                    {
                        image.SaveAsJpeg(outputStream, new JpegEncoder { Quality = 100 });
                    }
                    outputStream.Position = 0;

                    await _minioClient.PutObjectAsync(new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(jpgFileName)
                        .WithStreamData(outputStream)
                        .WithObjectSize(outputStream.Length)
                        .WithContentType("image/jpeg"));
                    _logger.LogInformation("File uploaded without compression: {FileName}", jpgFileName);
                    return jpgFileName;
                }

                using var tempStream = new MemoryStream();
                using (var image = await Image.LoadAsync(fileStream))
                {
                    int quality = 90;
                    long targetSize = maxSize;
                    long currentSize;

                    do
                    {
                        tempStream.SetLength(0);
                        image.SaveAsJpeg(tempStream, new JpegEncoder { Quality = quality });
                        currentSize = tempStream.Length;

                        if (currentSize > targetSize && quality > 10)
                        {
                            quality -= 10;
                        }
                        else if (currentSize < targetSize * 0.9 && quality < 90)
                        {
                            quality += 5;
                        }
                        else
                        {
                            break;
                        }
                    } while (quality > 0);

                    tempStream.Position = 0;
                    await _minioClient.PutObjectAsync(new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(jpgFileName)
                        .WithStreamData(tempStream)
                        .WithObjectSize(tempStream.Length)
                        .WithContentType("image/jpeg"));
                    _logger.LogInformation("File compressed to ~5MB and uploaded: {FileName}, Quality: {Quality}", jpgFileName, quality);
                }

                return jpgFileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting and uploading file to JPG");
                throw;
            }
        }

        public async Task DeleteFileAsync(string bucketName, string fileName)
        {
            // Kiểm tra tham số đầu vào
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty.", nameof(bucketName));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            try
            {
                var removeObjectArgs = new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName);

                await _minioClient.RemoveObjectAsync(removeObjectArgs);
            }
            catch (MinioException ex)
            {
                throw new InvalidOperationException($"Failed to delete object '{fileName}' from bucket '{bucketName}': {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"An unexpected error occurred while deleting object '{fileName}' from bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        public async Task<byte[]> DownloadFileAsync(string bucketName, string fileName)
        {
            using var memoryStream = new MemoryStream();
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));
            await _minioClient.GetObjectAsync(getObjectArgs);
            return memoryStream.ToArray();
        }

        public async Task<string> UploadFileAsync(IFormFile file, string bucketName)
        {
            await EnsureBucketExists(bucketName);
            string fileName = $"{Guid.NewGuid()}_{file.FileName}";
            using var stream = file.OpenReadStream();
            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
                .WithStreamData(stream)
                .WithObjectSize(file.Length)
                .WithContentType(file.ContentType));
            return fileName;
        }

        public async Task UpdateFileAsync(IFormFile file, string bucketName, string existingFileName)
        {
            await DeleteFileAsync(bucketName, existingFileName);
            await UploadFileAsync(file, bucketName);
        }

        public async Task<string> GetPresignedUrlAsync(string bucketName, string fileName, TimeSpan expiry)
        {
            var presignedUrlArgs = new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName)
                .WithExpiry((int)expiry.TotalSeconds);
            return await _minioClient.PresignedGetObjectAsync(presignedUrlArgs);
        }

        public async Task<string> ConvertAndUploadPublicFileAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize)
        {
            try
            {
                // Đảm bảo bucket tồn tại và công khai
                await EnsureBucketExists(bucketName);
                await SetPublicBucketPolicy(bucketName);

                string jpgFileName = Path.ChangeExtension(fileName, ".jpg");

                if (fileStream.Length <= maxSize)
                {
                    using var outputStream = new MemoryStream();
                    using (var image = await Image.LoadAsync(fileStream))
                    {
                        image.SaveAsJpeg(outputStream, new JpegEncoder { Quality = 100 });
                    }
                    outputStream.Position = 0;

                    await _minioClient.PutObjectAsync(new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(jpgFileName)
                        .WithStreamData(outputStream)
                        .WithObjectSize(outputStream.Length)
                        .WithContentType("image/jpeg"));
                    _logger.LogInformation("Public file uploaded without compression: {FileName}", jpgFileName);
                    return jpgFileName;
                }

                using var tempStream = new MemoryStream();
                using (var image = await Image.LoadAsync(fileStream))
                {
                    int quality = 90;
                    long targetSize = maxSize;
                    long currentSize;

                    do
                    {
                        tempStream.SetLength(0);
                        image.SaveAsJpeg(tempStream, new JpegEncoder { Quality = quality });
                        currentSize = tempStream.Length;

                        if (currentSize > targetSize && quality > 10)
                        {
                            quality -= 10;
                        }
                        else if (currentSize < targetSize * 0.9 && quality < 90)
                        {
                            quality += 5;
                        }
                        else
                        {
                            break;
                        }
                    } while (quality > 0);

                    tempStream.Position = 0;
                    await _minioClient.PutObjectAsync(new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(jpgFileName)
                        .WithStreamData(tempStream)
                        .WithObjectSize(tempStream.Length)
                        .WithContentType("image/jpeg"));
                    _logger.LogInformation("Public file compressed to ~{MaxSize}MB and uploaded: {FileName}, Quality: {Quality}", 
                        maxSize / (1024.0 * 1024.0), jpgFileName, quality);
                }

                return jpgFileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading public file to JPG");
                throw;
            }
        }

        public async Task<string> GetStaticPublicFileUrl(string bucketName, string objectName)
        {
            if (string.IsNullOrWhiteSpace(bucketName)) 
                throw new ArgumentException("Bucket name cannot be empty", nameof(bucketName));
            if (string.IsNullOrWhiteSpace(objectName)) 
                throw new ArgumentException("Object name cannot be empty", nameof(objectName));

            // Đảm bảo bucket tồn tại và công khai
            await EnsureBucketExists(bucketName);
            await SetPublicBucketPolicy(bucketName);

            // Tạo URL tĩnh
            var publicUrl = $"{_minioPublicUrl}/{bucketName}/{objectName}";
            _logger.LogInformation("Generated static public URL for {ObjectName}: {PublicUrl}", objectName, publicUrl);
            return publicUrl;
        }

        private async Task SetPublicBucketPolicy(string bucketName)
        {
            try
            {
                // Chính sách công khai cho phép đọc tất cả object trong bucket
                var policyJson = $@"{{
                    ""Version"": ""2012-10-17"",
                    ""Statement"": [
                        {{
                            ""Effect"": ""Allow"",
                            ""Principal"": {{""AWS"": ""*""}},
                            ""Action"": [""s3:GetObject""],
                            ""Resource"": [""arn:aws:s3:::{bucketName}/*""]
                        }}
                    ]
                }}";

                await _minioClient.SetPolicyAsync(new SetPolicyArgs()
                    .WithBucket(bucketName)
                    .WithPolicy(policyJson));

                _logger.LogInformation("Set public read policy for bucket: {BucketName}", bucketName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set public policy for bucket {BucketName}", bucketName);
                throw;
            }
        }

        private async Task EnsureBucketExists(string bucketName)
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
            bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(bucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
            }
        }
    }
}