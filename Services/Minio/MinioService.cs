using LapTrinhWindows.Repositories.Minio;

namespace LapTrinhWindows.Services.Minio
{
    public interface IFileService
    {
        Task<string> UploadFileAsync(IFormFile file, string bucketName);
        Task<byte[]> DownloadFileAsync(string bucketName, string fileName);
        Task DeleteFileAsync(string bucketName, string fileName);
        Task UpdateFileAsync(IFormFile file, string bucketName, string existingFileName);
        Task<string> GetPresignedUrlAsync(string bucketName, string fileName, TimeSpan expiry);
        Task<string> ConvertAndUploadAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize);
        //Task<string> GetStaticPublicFileUrl(string bucketName, string objectName);
        Task<string> GetStaticPublicFileUrl(string bucketName, string objectName);
        //Task<string> ConvertAndUploadPublicFileAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize);
        Task<string> ConvertAndUploadPublicFileAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize);
    }

    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;

        public FileService(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
        }

        public Task<string> UploadFileAsync(IFormFile file, string bucketName)
        {
            return _fileRepository.UploadFileAsync(file, bucketName);
        }

        public Task<byte[]> DownloadFileAsync(string bucketName, string fileName)
        {
            return _fileRepository.DownloadFileAsync(bucketName, fileName);
        }

        public Task DeleteFileAsync(string bucketName, string fileName)
        {
            return _fileRepository.DeleteFileAsync(bucketName, fileName);
        }

        public Task UpdateFileAsync(IFormFile file, string bucketName, string existingFileName)
        {
            return _fileRepository.UpdateFileAsync(file, bucketName, existingFileName);
        }

        public Task<string> GetPresignedUrlAsync(string bucketName, string fileName, TimeSpan expiry)
        {
            return _fileRepository.GetPresignedUrlAsync(bucketName, fileName, expiry);
        }

        public Task<string> ConvertAndUploadAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize)
        {
            return _fileRepository.ConvertAndUploadAsJpgAsync(fileStream, bucketName, fileName, maxSize);
        }

        public Task<string> GetStaticPublicFileUrl(string bucketName, string objectName)
        {
            return _fileRepository.GetStaticPublicFileUrl(bucketName, objectName);
        }
        public Task<string> ConvertAndUploadPublicFileAsJpgAsync(Stream fileStream, string bucketName, string fileName, long maxSize)
        {
            return _fileRepository.ConvertAndUploadPublicFileAsJpgAsync(fileStream, bucketName, fileName, maxSize);
        }
    }
}