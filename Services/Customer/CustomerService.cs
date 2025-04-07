using LapTrinhWindows.Models;
using LapTrinhWindows.Models.dto.CustomerDTO;
using LapTrinhWindows.Repositories.CustomerRepository;
using Microsoft.EntityFrameworkCore.Storage;
using LapTrinhWindows.Services.Minio;
using System.Text.RegularExpressions;
using System.Transactions;

namespace LapTrinhWindows.Services
{
    public interface ICustomerService
    {
        Task AddCustomerAsync(CreateCustomerDTO dto);
        Task<CustomerProfileDTO> GetCustomerProfileByIdAsync(Guid customerId);
        // change password
        Task ChangePasswordAsync(Guid customerId, ChangePasswordDTO dto);
        // update customer
        Task UpdateCustomerInformationAsync(Guid CustomerId, UpdateCustomerProfileDTO dto);
        //update avt image
        Task UpdateCustomerAvtAsync(Guid customerId, IFormFile avtFile);
        //delete customer
        Task DeleteCustomerAsync(Guid customerId);
        //activate customer
        Task ActivateCustomerAsync(Guid customerId);
    }

    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IFileService _fileService;
        private readonly ILogger<CustomerService> _logger;
        private const string CustomerBucketName = "customer-avatars";

        public CustomerService(
            ICustomerRepository customerRepository,
            ApplicationDbContext context,
            IPasswordHasher passwordHasher,
            IFileService fileService,
            ILogger<CustomerService> logger)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AddCustomerAsync(CreateCustomerDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "Customer DTO is null");
            if (string.IsNullOrWhiteSpace(dto.Password)) throw new ArgumentException("Password cannot be empty", nameof(dto.Password));
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber)) throw new ArgumentException("PhoneNumber cannot be empty", nameof(dto.PhoneNumber));

            if (!IsPasswordStrong(dto.Password))
            {
                throw new ArgumentException("Password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, and one number.", nameof(dto.Password));
            }

            var existingCustomer = await _customerRepository.GetCustomerByPhoneNumberAsync(dto.PhoneNumber);
            if (existingCustomer != null)
            {
                throw new InvalidOperationException($"A customer with PhoneNumber '{dto.PhoneNumber}' already exists.");
            }

            string avtKey = dto.AvtKey;
            if (dto.AvtFile != null && dto.AvtFile.Length > 0)
            {
                // Kiểm tra xem file có phải là hình ảnh không
                if (!await IsImageAsync(dto.AvtFile))
                {
                    throw new ArgumentException("Avatar file must be an image (JPEG, PNG, GIF, etc.).", nameof(dto.AvtFile));
                }

                // Giới hạn kích thước tối đa 5MB
                const long maxSize = 5 * 1024 * 1024; // 5MB in bytes
                using var stream = dto.AvtFile.OpenReadStream();
                avtKey = await _fileService.ConvertAndUploadAsJpgAsync(
                    stream,
                    CustomerBucketName,
                    $"{Guid.NewGuid()}.jpg",
                    maxSize 
                );
            }

            var customer = new Customer
            {
                CustomerID = Guid.NewGuid(),
                CustomerName = dto.CustomerName,
                Address = dto.Address,
                PhoneNumber = dto.PhoneNumber,
                HashPassword = _passwordHasher.HashPassword(dto.Password),
                AvtKey = avtKey
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _customerRepository.CreateCustomerAsync(customer);
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                if (transaction != null && transaction.GetDbTransaction().Connection != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
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

        // Hàm kiểm tra ảnh (giữ nguyên từ trước)
        private async Task<bool> IsImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;

            var validImageTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/bmp" };
            if (!validImageTypes.Contains(file.ContentType.ToLower()))
            {
                return false;
            }

            using var stream = file.OpenReadStream();
            byte[] buffer = new byte[4];
            try
            {
                await stream.ReadExactlyAsync(buffer, 0, 4);
            }
            catch (EndOfStreamException)
            {
                return false;
            }

            if (buffer[0] == 0xFF && buffer[1] == 0xD8) return true; // JPEG
            if (buffer[0] == 0x89 && buffer[1] == 0x50) return true; // PNG
            if (buffer[0] == 0x47 && buffer[1] == 0x49) return true; // GIF
            if (buffer[0] == 0x42 && buffer[1] == 0x4D) return true; // BMP

            return false;
        }

        private bool IsPasswordStrong(string password)
        {
            var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
            return passwordRegex.IsMatch(password);
        }

        public async Task<CustomerProfileDTO> GetCustomerProfileByIdAsync(Guid customerId)
        {
            var customer = await _customerRepository.GetCustomerByIdAsync(customerId);
            if (customer == null)
            {
                throw new KeyNotFoundException($"Customer with ID '{customerId}' not found.");
            }

            string? avatarUrl = null;
            if (!string.IsNullOrEmpty(customer.AvtKey) && 
                !customer.AvtKey.StartsWith("https://www.gravatar.com"))
            {
                try
                {
                    avatarUrl = await _fileService.GetPresignedUrlAsync(
                        CustomerBucketName, 
                        customer.AvtKey, 
                        TimeSpan.FromHours(1) 
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate presigned URL for customer {CustomerId} with AvtKey {AvtKey}", 
                        customerId, customer.AvtKey);
                    throw new InvalidOperationException("Failed to generate presigned URL for customer avatar.", ex);
                }
            }

            var profile = new CustomerProfileDTO
            {
                CustomerName = customer.CustomerName,
                Address = customer.Address,
                PhoneNumber = customer.PhoneNumber,
                AvatarUrl = avatarUrl
            };

            return profile;
        }
        public async Task ChangePasswordAsync(Guid customerId, ChangePasswordDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "ChangePasswordDTO is null");

            if (string.IsNullOrWhiteSpace(dto.OldPassword))
                throw new ArgumentException("Old password cannot be empty", nameof(dto.OldPassword));

            if (string.IsNullOrWhiteSpace(dto.NewPassword))
                throw new ArgumentException("New password cannot be empty", nameof(dto.NewPassword));

            if (dto.NewPassword == dto.OldPassword)
                throw new ArgumentException("New password cannot be the same as old password", nameof(dto.NewPassword));

            if (!IsPasswordStrong(dto.NewPassword))
            {
                throw new ArgumentException("New password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, and one number.", nameof(dto.NewPassword));
            }
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var customer = await _customerRepository.GetCustomerByIdAsync(customerId);
                if (customer == null)
                    throw new KeyNotFoundException($"Customer with ID '{customerId}' not found.");
                if (!_passwordHasher.VerifyPassword(dto.OldPassword, customer.HashPassword))
                    throw new UnauthorizedAccessException("Old password is incorrect.");
                var newHashPassword = _passwordHasher.HashPassword(dto.NewPassword);
                await _customerRepository.UpdateCustomerPasswordAsync(customerId, newHashPassword);

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                // Rollback transaction nếu có lỗi
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task UpdateCustomerInformationAsync(Guid customerId, UpdateCustomerProfileDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "UpdateCustomerProfileDTO is null");

            if (string.IsNullOrWhiteSpace(dto.CustomerName))
                throw new ArgumentException("Customer name cannot be empty", nameof(dto.CustomerName));

            if (string.IsNullOrWhiteSpace(dto.Address))
                throw new ArgumentException("Address cannot be empty", nameof(dto.Address));

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                throw new ArgumentException("Phone number cannot be empty", nameof(dto.PhoneNumber));

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var customer = await _customerRepository.GetCustomerByIdAsync(customerId);
                if (customer == null)
                    throw new KeyNotFoundException($"Customer with ID '{customerId}' not found.");

                customer.CustomerName = dto.CustomerName;
                customer.Address = dto.Address;
                customer.PhoneNumber = dto.PhoneNumber;

                await _customerRepository.UpdateCustomerInformationAsync(customer);

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task UpdateCustomerAvtAsync(Guid customerId, IFormFile avtFile)
        {
            if (avtFile == null || avtFile.Length == 0)
                throw new ArgumentNullException(nameof(avtFile), "Avatar file is null or empty");

            if (!await IsImageAsync(avtFile))
                throw new ArgumentException("Avatar file must be an image (JPEG, PNG, GIF, etc.).", nameof(avtFile));

            var customer = await _customerRepository.GetCustomerByIdAsync(customerId);
            if (customer == null)
                throw new KeyNotFoundException($"Customer with ID '{customerId}' not found.");

            string oldAvtKey = customer.AvtKey; 
            string newAvtKey = string.Empty;
            const long maxSize = 5 * 1024 * 1024; 
            using var stream = avtFile.OpenReadStream();
            newAvtKey = await _fileService.ConvertAndUploadAsJpgAsync(
                stream,
                CustomerBucketName,
                $"{Guid.NewGuid()}.jpg",
                maxSize
            );
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                customer.AvtKey = newAvtKey;
                await _customerRepository.UpdateCustomerInformationAsync(customer);

                await transaction.CommitAsync();

                if (!string.IsNullOrEmpty(oldAvtKey) && !oldAvtKey.StartsWith("https://www.gravatar.com"))
                {
                    try
                    {
                        await _fileService.DeleteFileAsync(CustomerBucketName, oldAvtKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old avatar {OldAvtKey} after successful update", oldAvtKey);
                    }
                }
            }
            catch (Exception ex)
            {
                if (transaction != null && transaction.GetDbTransaction().Connection != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        _logger.LogInformation("Transaction rolled back due to error: {Error}", ex.Message);
                        if (!string.IsNullOrEmpty(newAvtKey))
                        {
                            try
                            {
                                await _fileService.DeleteFileAsync(CustomerBucketName, newAvtKey);
                                _logger.LogInformation("Deleted new avatar {NewAvtKey} during rollback", newAvtKey);
                            }
                            catch (Exception rollbackEx)
                            {
                                _logger.LogError(rollbackEx, "Failed to delete new avatar {NewAvtKey} during rollback", newAvtKey);
                            }
                        }

                        customer.AvtKey = oldAvtKey;
                    }
                    catch (Exception rollbackEx)
                    {
                        throw new InvalidOperationException("Rollback failed after an error occurred.", rollbackEx);
                    }
                }
                throw; 
            }
        }
        //just ChangeCustomerStatusAsync not delete customer
        public async Task DeleteCustomerAsync(Guid customerId)
        {
            await _customerRepository.ChangeCustomerStatusAsync(customerId, false);
        }
        public async Task ActivateCustomerAsync(Guid customerId)
        {
            await _customerRepository.ChangeCustomerStatusAsync(customerId, true);
        }
    }
}