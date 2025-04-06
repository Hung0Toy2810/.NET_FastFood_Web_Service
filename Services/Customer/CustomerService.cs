using LapTrinhWindows.Models;
using LapTrinhWindows.Models.dto.CustomerDTO;
using LapTrinhWindows.Repositories.CustomerRepository;
using Microsoft.EntityFrameworkCore.Storage;
using LapTrinhWindows.Services.Minio;
using System.Text.RegularExpressions;

namespace LapTrinhWindows.Services
{
    public interface ICustomerService
    {
        Task AddCustomerAsync(CreateCustomerDTO dto);
        Task<CustomerProfileDTO> GetCustomerProfileByIdAsync(Guid customerId);
        // change password
        Task ChangePasswordAsync(Guid Id, string newPassword);
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
                avtKey = await _fileService.UploadFileAsync(dto.AvtFile, CustomerBucketName);
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

        private bool IsPasswordStrong(string password)
        {
            var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
            return passwordRegex.IsMatch(password);
        }

        public async Task<CustomerProfileDTO> GetCustomerProfileByIdAsync(Guid customerId)
        {
            // Lấy thông tin khách hàng từ repository
            var customer = await _customerRepository.GetCustomerByIdAsync(customerId);
            if (customer == null)
            {
                throw new KeyNotFoundException($"Customer with ID '{customerId}' not found.");
            }

            byte[]? avtFileData = null;
            if (!string.IsNullOrEmpty(customer.AvtKey) && 
                !customer.AvtKey.StartsWith("https://www.gravatar.com"))
            {
                try
                {
                    avtFileData = await _fileService.DownloadFileAsync(CustomerBucketName, customer.AvtKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download avatar for customer {CustomerId} with AvtKey {AvtKey}", 
                        customerId, customer.AvtKey);
                }
            }

            var profile = new CustomerProfileDTO
            {
                CustomerName = customer.CustomerName,
                Address = customer.Address,
                PhoneNumber = customer.PhoneNumber,
                AvtFileData = avtFileData
            };

            return profile;
        }
        public async Task ChangePasswordAsync(Guid customerId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                throw new ArgumentException("New password cannot be empty", nameof(newPassword));

            if (!IsPasswordStrong(newPassword))
                throw new ArgumentException("Password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, and one number.", nameof(newPassword));
            var customer = await _customerRepository.GetCustomerByIdAsync(customerId);
            if (customer == null)
                throw new KeyNotFoundException($"Customer with ID '{customerId}' not found.");

            var newHashPassword = _passwordHasher.HashPassword(newPassword);
            
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _customerRepository.UpdateCustomerPasswordAsync(customerId, newHashPassword);
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    throw new InvalidOperationException("Rollback failed after an error occurred.", rollbackEx);
                }
                throw;
            }
        }
    }
}