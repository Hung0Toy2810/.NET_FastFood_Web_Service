using Minio;
using Minio.DataModel.Args;
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
    }

    public class MinioFileRepository : IFileRepository
    {
        private readonly IMinioClient _minioClient;
        private readonly ILogger<MinioFileRepository> _logger;

        public MinioFileRepository(IMinioClient minioClient, ILogger<MinioFileRepository> logger)
        {
            _minioClient = minioClient;
            _logger = logger;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string bucketName)
        {
            try
            {
                await EnsureBucketExists(bucketName);
                string fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                using var stream = file.OpenReadStream();
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType);

                await _minioClient.PutObjectAsync(putObjectArgs);
                _logger.LogInformation("File uploaded successfully: {FileName}", fileName);
                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                throw;
            }
        }

        public async Task<byte[]> DownloadFileAsync(string bucketName, string fileName)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName)
                    .WithCallbackStream(async (stream, cancellationToken) =>
                    {
                        await stream.CopyToAsync(memoryStream);
                    });

                await _minioClient.GetObjectAsync(getObjectArgs);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file: {FileName}", fileName);
                throw;
            }
        }

        public async Task DeleteFileAsync(string bucketName, string fileName)
        {
            try
            {
                var removeObjectArgs = new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName);

                await _minioClient.RemoveObjectAsync(removeObjectArgs);
                _logger.LogInformation("File deleted successfully: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FileName}", fileName);
                throw;
            }
        }

        public async Task UpdateFileAsync(IFormFile file, string bucketName, string existingFileName)
        {
            try
            {
                await DeleteFileAsync(bucketName, existingFileName);

                using var stream = file.OpenReadStream();
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(existingFileName)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType);

                await _minioClient.PutObjectAsync(putObjectArgs);
                _logger.LogInformation("File updated successfully: {FileName}", existingFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file: {FileName}", existingFileName);
                throw;
            }
        }

        public async Task<string> GetPresignedUrlAsync(string bucketName, string fileName, TimeSpan expiry)
        {
            try
            {
                var presignedUrl = await _minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName)
                    .WithExpiry((int)expiry.TotalSeconds));
                _logger.LogInformation("Generated presigned URL for {FileName}", fileName);
                return presignedUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating presigned URL for {FileName}", fileName);
                throw;
            }
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

        private async Task EnsureBucketExists(string bucketName)
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);

            bool exists = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            if (!exists)
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(bucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                _logger.LogInformation("Bucket created: {BucketName}", bucketName);
            }
        }
    }
}