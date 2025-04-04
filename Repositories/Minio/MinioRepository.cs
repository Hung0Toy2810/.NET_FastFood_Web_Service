using Minio;
using Minio.DataModel.Args;
namespace LapTrinhWindows.Repositories.Minio
{
    public interface IFileRepository
    {
        Task<string> UploadFileAsync(IFormFile file, string bucketName);
        Task<byte[]> DownloadFileAsync(string bucketName, string fileName);
        Task DeleteFileAsync(string bucketName, string fileName);
        Task UpdateFileAsync(IFormFile file, string bucketName, string existingFileName);
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