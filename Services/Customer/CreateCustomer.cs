using LapTrinhWindows.Models;
using LapTrinhWindows.Models.dto.CustomerDTO;
using LapTrinhWindows.Repositories.CustomerRepository;
using Microsoft.EntityFrameworkCore.Storage;
using LapTrinhWindows.Services.Minio;

namespace LapTrinhWindows.Services
{
    public interface ICustomerService
    {
        Task AddCustomerAsync(CreateCustomerDTO dto);
    }

    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IFileService _fileService; // Thêm IFileService
        private const string CustomerBucketName = "customer-avatars"; // Bucket cho avatar

        public CustomerService(
            ICustomerRepository customerRepository,
            ApplicationDbContext context,
            IPasswordHasher passwordHasher,
            IFileService fileService) // Thêm dependency
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        }

        public async Task AddCustomerAsync(CreateCustomerDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "Customer DTO is null");
            if (string.IsNullOrWhiteSpace(dto.Password)) throw new ArgumentException("Password cannot be empty", nameof(dto.Password));
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber)) throw new ArgumentException("PhoneNumber cannot be empty", nameof(dto.PhoneNumber));

            // Kiểm tra xem khách hàng đã tồn tại chưa
            var existingCustomer = await _customerRepository.GetCustomerByPhoneNumberAsync(dto.PhoneNumber);
            if (existingCustomer != null)
            {
                throw new InvalidOperationException($"A customer with PhoneNumber '{dto.PhoneNumber}' already exists.");
            }

            // Upload avatar nếu có file
            string avtKey = dto.AvtKey; // Default avatar key
            if (dto.AvtFile != null && dto.AvtFile.Length > 0)
            {
                avtKey = await _fileService.UploadFileAsync(dto.AvtFile, CustomerBucketName);
            }

            // Tạo đối tượng khách hàng mới
            var customer = new Customer
            {
                CustomerID = Guid.NewGuid(),
                CustomerName = dto.CustomerName,
                Address = dto.Address,
                PhoneNumber = dto.PhoneNumber,
                HashPassword = _passwordHasher.HashPassword(dto.Password),
                AvtKey = avtKey 
            };

            // Bắt đầu giao dịch
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Thêm khách hàng vào cơ sở dữ liệu
                await _customerRepository.CreateCustomerAsync(customer);

                // Commit giao dịch
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                // Rollback giao dịch nếu có lỗi
                if (transaction != null && transaction.GetDbTransaction().Connection != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        
                        // Nếu upload file thành công nhưng DB thất bại, xóa file đã upload
                        if (dto.AvtFile != null && !string.Equals(avtKey, dto.AvtKey))
                        {
                            await _fileService.DeleteFileAsync(CustomerBucketName, avtKey);
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        throw new InvalidOperationException("Rollback failed after an error occurred.", rollbackEx);
                    }
                }
                throw;
            }
        }
    }
}

