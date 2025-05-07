// INSERT INTO EmployeeRoles (RoleName)
// VALUES 
//     ('Staff'),
//     ('Manager');

using LapTrinhWindows.Repositories.EmployeeRepository;
using LapTrinhWindows.Models;
using LapTrinhWindows.Models.dto.CustomerDTO;
using LapTrinhWindows.Repositories.CustomerRepository;
using Microsoft.EntityFrameworkCore.Storage;
using LapTrinhWindows.Services.Minio;
using System.Text.RegularExpressions;
using LapTrinhWindows.Models.dto.EmployeeDTO;
using System.Transactions;
using LapTrinhWindows.Repositories.RoleRepository;

namespace LapTrinhWindows.Services
{
    // interface 
    public interface IEmployeeService
    {
        // create employee
        Task<Employee> AddEmployeeAsync(CreateEmployeeDTO dto);
        Task<EmployeeProfileDTO> GetEmployeeProfileByIdAsync(Guid employeeId);
        //update employee profile
        Task UpdateEmployeeProfileAsync(Guid employeeId, UpdateEmployeeProfileDTO dto);
        // change password
        Task UpdateEmployeePasswordAsync(Guid employeeId, string oldPassword, string newPassword);
        // change avt
        Task UpdateEmployeeAvtAsync(Guid employeeId, IFormFile avtFile);
        //update employee status
        Task ChangeEmployeeStatusAsync(Guid employeeId, bool status);
        Task UnactiveEmployeeAsync(Guid employeeId);
        Task ActiveEmployeeAsync(Guid employeeId);
        // delete employee
        Task DeleteEmployeeAsync(Guid employeeId);
        // search employee
        //Task<List<EmployeeProfileDTO>> SearchEmployeesAsync(string searchTerm);
        //login service

    }
    public class EmployeeService : IEmployeeService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IFileService _fileService;
        private readonly ILogger<CustomerService> _logger;
        private const string CustomerBucketName = "customer-avatars";
        private readonly IRoleRepository _roleRepository;

        public EmployeeService(
            IEmployeeRepository employeeRepository,
            ApplicationDbContext context,
            IPasswordHasher passwordHasher,
            IFileService fileService,
            ILogger<CustomerService> logger,
            IRoleRepository roleRepository)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        }
        public async Task<Employee> AddEmployeeAsync(CreateEmployeeDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto), "Employee DTO is null");
            if (string.IsNullOrWhiteSpace(dto.Password)) throw new ArgumentException("Password cannot be empty", nameof(dto.Password));
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber)) throw new ArgumentException("PhoneNumber cannot be empty", nameof(dto.PhoneNumber));
            if (string.IsNullOrWhiteSpace(dto.RoleName)) throw new ArgumentException("RoleName cannot be empty", nameof(dto.RoleName));

            if (!IsPasswordStrong(dto.Password))
            {
                throw new ArgumentException("Password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, and one number.", nameof(dto.Password));
            }

            var existingEmployee = await _employeeRepository.GetEmployeeByPhoneNumberAsync(dto.PhoneNumber);
            if (existingEmployee != null)
            {
                throw new InvalidOperationException($"An employee with PhoneNumber '{dto.PhoneNumber}' already exists.");
            }

            // Tìm RoleID dựa trên RoleName
            var role = await _context.EmployeeRoles
                .FirstOrDefaultAsync(r => r.RoleName == dto.RoleName);
            if (role == null)
            {
                throw new InvalidOperationException($"Role with name '{dto.RoleName}' does not exist.");
            }

            string avtKey = dto.AvtKey;
            if (dto.AvtFile != null && dto.AvtFile.Length > 0)
            {
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
                RoleID = role.RoleID, 
                AvtKey = avtKey,
                Status = dto.Status,
                AccountStatus = true
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _employeeRepository.AddEmployeeAsync(employee);
                await transaction.CommitAsync();
                return employee;
            }
            catch (Exception ex)
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
                throw new InvalidOperationException($"Failed to add employee: {ex.Message}", ex);
            }
        }
        public async Task<EmployeeProfileDTO> GetEmployeeProfileByIdAsync(Guid employeeId)
        {
            var employee = await _employeeRepository.GetEmployeeByIdAsync(employeeId);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee with ID '{employeeId}' not found.");
            }

            string? avatarUrl = null;
            if (!string.IsNullOrEmpty(employee.AvtKey) &&
                !employee.AvtKey.StartsWith("https://www.gravatar.com"))
            {
                try
                {
                    avatarUrl = await _fileService.GetPresignedUrlAsync(
                        CustomerBucketName, 
                        employee.AvtKey, 
                        TimeSpan.FromHours(1) 
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate presigned URL for employee {EmployeeId} with AvtKey {AvtKey}", 
                        employeeId, employee.AvtKey);
                    throw new InvalidOperationException("Failed to generate presigned URL for employee avatar.", ex);
                }
            }

            var profile = new EmployeeProfileDTO
            {
                FullName = employee.FullName,
                Address = employee.Address,
                PhoneNumber = employee.PhoneNumber,
                Email = employee.Email,
                RoleName = employee.EmployeeRole?.RoleName, 
                AvatarUrl = avatarUrl,
                Status = employee.Status
            };

            return profile;
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
        public async Task UpdateEmployeeProfileAsync(Guid employeeId, UpdateEmployeeProfileDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "UpdateEmployeeProfileDTO is null");

            if (string.IsNullOrWhiteSpace(dto.EmployeeName))
                throw new ArgumentException("Employee name cannot be empty", nameof(dto.EmployeeName));

            if (string.IsNullOrWhiteSpace(dto.Address))
                throw new ArgumentException("Address cannot be empty", nameof(dto.Address));

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                throw new ArgumentException("Phone number cannot be empty", nameof(dto.PhoneNumber));

            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new ArgumentException("Email cannot be empty", nameof(dto.Email));

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = await _employeeRepository.GetEmployeeByIdAsync(employeeId);
                if (employee == null)
                    throw new KeyNotFoundException($"Employee with ID '{employeeId}' not found.");

                employee.FullName = dto.EmployeeName;
                employee.Address = dto.Address;
                employee.PhoneNumber = dto.PhoneNumber;
                employee.Email = dto.Email;

                var result = await _employeeRepository.UpdateEmployeeAsync(employee);
                if (!result)
                    throw new InvalidOperationException("Failed to update employee profile.");

                await transaction.CommitAsync();

                _logger.LogInformation("Employee profile updated successfully for employee {EmployeeId}", employeeId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update employee profile for employee {EmployeeId}", employeeId);
                throw;
            }
        }

        public async Task UpdateEmployeePasswordAsync(Guid employeeId, string oldPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(oldPassword))
                throw new ArgumentException("Old password cannot be empty", nameof(oldPassword));

            if (string.IsNullOrWhiteSpace(newPassword))
                throw new ArgumentException("New password cannot be empty", nameof(newPassword));

            if (newPassword == oldPassword)
                throw new ArgumentException("New password cannot be the same as old password", nameof(newPassword));

            if (!IsPasswordStrong(newPassword))
            {
                throw new ArgumentException("New password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, and one number.", nameof(newPassword));
            }
            var employee = await _employeeRepository.GetEmployeeByIdAsync(employeeId);
            if (employee == null)
                throw new KeyNotFoundException($"Employee with ID '{employeeId}' not found.");

            if (!_passwordHasher.VerifyPassword(oldPassword, employee.HashPassword))
                throw new UnauthorizedAccessException("Old password is incorrect.");

            var newHashPassword = _passwordHasher.HashPassword(newPassword);

            await _employeeRepository.UpdateEmployeePasswordAsync(employeeId, newHashPassword);

            _logger.LogInformation("Password updated successfully for employee {EmployeeId}", employeeId);
        }
        public async Task ChangeEmployeeStatusAsync(Guid employeeId, bool status)
        {
            if (employeeId == Guid.Empty)
                throw new ArgumentException("Employee ID cannot be empty.", nameof(employeeId));

            try 
            {
                await _employeeRepository.ChangeEmployeeStatusAsync(employeeId, status);
                _logger.LogInformation("Employee status updated successfully for employee {EmployeeId}", employeeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update status for employee {EmployeeId}", employeeId);
                throw;
            }
        }
        public async Task UnactiveEmployeeAsync(Guid employeeId)
        {
            if (employeeId == Guid.Empty)
                throw new ArgumentException("Employee ID cannot be empty.", nameof(employeeId));

            try 
            {
                await _employeeRepository.UnactiveEmployeeAsync(employeeId);
                _logger.LogInformation("Employee account deactivated successfully for employee {EmployeeId}", employeeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate account for employee {EmployeeId}", employeeId);
                throw;
            }
        }

        public async Task ActiveEmployeeAsync(Guid employeeId)
        {
            if (employeeId == Guid.Empty)
                throw new ArgumentException("Employee ID cannot be empty.", nameof(employeeId));

            try 
            {
                await _employeeRepository.ActiveEmployeeAsync(employeeId);
                _logger.LogInformation("Employee account activated successfully for employee {EmployeeId}", employeeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate account for employee {EmployeeId}", employeeId);
                throw;
            }
        }
        public async Task UpdateEmployeeAvtAsync(Guid employeeId, IFormFile avtFile)
        {
            if (avtFile == null || avtFile.Length == 0)
                throw new ArgumentNullException(nameof(avtFile), "Avatar file is null or empty");

            if (!await IsImageAsync(avtFile))
                throw new ArgumentException("Avatar file must be an image (JPEG, PNG, GIF, etc.).", nameof(avtFile));

            var employee = await _employeeRepository.GetEmployeeByIdAsync(employeeId);
            if (employee == null)
                throw new KeyNotFoundException($"Employee with ID '{employeeId}' not found.");

            string oldAvtKey = employee.AvtKey;
            string newAvtKey = string.Empty;
            const long maxSize = 5 * 1024 * 1024; // 5MB

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
                employee.AvtKey = newAvtKey;
                var result = await _employeeRepository.UpdateEmployeeAsync(employee);
                if (!result)
                    throw new InvalidOperationException("Failed to update employee avatar.");

                await transaction.CommitAsync();

                // Delete old avatar if it exists and is not the default gravatar
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

                _logger.LogInformation("Avatar updated successfully for employee {EmployeeId}", employeeId);
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

                        employee.AvtKey = oldAvtKey;
                    }
                    catch (Exception rollbackEx)
                    {
                        throw new InvalidOperationException("Rollback failed after an error occurred.", rollbackEx);
                    }
                }
                throw;
            }
        }
        // delete employee and remove avt if all procedures are failed, rollback include avt
        public async Task DeleteEmployeeAsync(Guid employeeId)
        {
            if (employeeId == Guid.Empty)
                throw new ArgumentException("Employee ID cannot be empty.", nameof(employeeId));

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = await _employeeRepository.GetEmployeeByIdAsync(employeeId);
                if (employee == null)
                    throw new KeyNotFoundException($"Employee with ID '{employeeId}' not found.");

                string avtKey = employee.AvtKey;

                // Delete from database first
                var result = await _employeeRepository.DeleteEmployeeAsync(employee);
                if (!result)
                    throw new InvalidOperationException("Failed to delete employee from database.");

                // If database deletion successful, try to delete avatar
                if (!string.IsNullOrEmpty(avtKey) && !avtKey.StartsWith("https://www.gravatar.com"))
                {
                    try
                    {
                        await _fileService.DeleteFileAsync(CustomerBucketName, avtKey);
                    }
                    catch (Exception ex)
                    {
                        // If avatar deletion fails, rollback the database changes
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Failed to delete avatar {AvtKey} for employee {EmployeeId}", avtKey, employeeId);
                        throw new InvalidOperationException("Failed to delete employee avatar. Transaction rolled back.", ex);
                    }
                }

                // If everything successful, commit the transaction
                await transaction.CommitAsync();
                _logger.LogInformation("Employee {EmployeeId} deleted successfully with avatar {AvtKey}", employeeId, avtKey);
            }
            catch (Exception ex)
            {
                // Rollback if any error occurs
                if (transaction != null && transaction.GetDbTransaction().Connection != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        _logger.LogInformation("Transaction rolled back due to error: {Error}", ex.Message);
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