using LapTrinhWindows.Repositories.EmployeeRepository;
using LapTrinhWindows.Models;
using LapTrinhWindows.Models.dto.CustomerDTO;
using LapTrinhWindows.Repositories.CustomerRepository;
using Microsoft.EntityFrameworkCore.Storage;
using LapTrinhWindows.Services.Minio;
using System.Text.RegularExpressions;
using LapTrinhWindows.Models.dto.EmployeeDTO;
using System.Transactions;
namespace LapTrinhWindows.Services
{
    // interface 
    public interface IEmployeeService
    {
        // create employee
        Task<Employee> AddEmployeeAsync(CreateEmployeeDTO dto);
    }
    public class EmployeeService : IEmployeeService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IFileService _fileService;
        private readonly ILogger<CustomerService> _logger;
        private const string CustomerBucketName = "customer-avatars";

        public EmployeeService(
            EmployeeRepository employeeRepository,
            ApplicationDbContext context,
            IPasswordHasher passwordHasher,
            IFileService fileService,
            ILogger<CustomerService> logger)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public async Task<Employee> AddEmployeeAsync(CreateEmployeeDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "Employee DTO is null");
            if (string.IsNullOrWhiteSpace(dto.Password)) throw new ArgumentException("Password cannot be empty", nameof(dto.Password));
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber)) throw new ArgumentException("PhoneNumber cannot be empty", nameof(dto.PhoneNumber));

            if (!IsPasswordStrong(dto.Password))
            {
                throw new ArgumentException("Password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, and one number.", nameof(dto.Password));
            }

            
            var existingEmployee = await _employeeRepository.GetEmployeeByPhoneNumberAsync(dto.PhoneNumber);
            if (existingEmployee != null)
            {
                throw new InvalidOperationException($"An employee with PhoneNumber '{dto.PhoneNumber}' already exists.");
            }

            string avtKey = dto.AvtKey;
            if (dto.AvtFile != null && dto.AvtFile.Length > 0)
            {
                // Kiểm tra xem file có phải là hình ảnh không
                if (!await IsImageAsync(dto.AvtFile))
                {
                    throw new ArgumentException("Avatar file must be an image (JPEG, PNG, GIF, etc.).", nameof(dto.AvtFile));
                }
                const long maxSize = 5 * 1024 * 1024;
                using var stream = dto.AvtFile.OpenReadStream();
                avtKey = await _fileService.ConvertAndUploadAsJpgAsync(
                    stream,
                    CustomerBucketName,
                    $"{Guid.NewGuid()}.jpg",
                    maxSize
                );
            }

            var employee = new Employee
            {
                EmployeeID = Guid.NewGuid(),
                FullName = dto.FullName,
                Address = dto.Address,
                PhoneNumber = dto.PhoneNumber,
                Email = dto.Email,
                HashPassword = _passwordHasher.HashPassword(dto.Password),
                RoleID = dto.RoleID,
                AvtKey = avtKey,
                Status = dto.Status,
                AccountStatus = true // Mặc định là true
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _employeeRepository.AddEmployee(employee);
                await transaction.CommitAsync();
                return employee;
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
    }
}