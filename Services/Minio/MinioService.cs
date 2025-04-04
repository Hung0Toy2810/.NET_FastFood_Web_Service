using LapTrinhWindows.Repositories.Minio;
namespace LapTrinhWindows.Services.Minio
{
    public interface IFileService
    {
        Task<string> UploadFileAsync(IFormFile file, string bucketName);
        Task<byte[]> DownloadFileAsync(string bucketName, string fileName);
        Task DeleteFileAsync(string bucketName, string fileName);
        Task UpdateFileAsync(IFormFile file, string bucketName, string existingFileName);
    }

    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;

        public FileService(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository;
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
    }
}