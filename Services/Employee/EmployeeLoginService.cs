using LapTrinhWindows.Models;
using LapTrinhWindows.Repositories.EmployeeRepository;
using System.Security.Claims;

namespace LapTrinhWindows.Services
{
    public interface IEmployeeLoginService
    {
        Task<EmployeeLoginResult?> LoginAsync(string email, string password, string clientIp);
    }

    public class EmployeeLoginResult
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public int ExpiresIn { get; set; } 
    }

    public class EmployeeLoginService : IEmployeeLoginService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;

        public EmployeeLoginService(
            IEmployeeRepository employeeRepository,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        }

        public async Task<EmployeeLoginResult?> LoginAsync(string email, string password, string clientIp)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty", nameof(email));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));
            if (string.IsNullOrWhiteSpace(clientIp))
                throw new ArgumentException("Client IP cannot be empty", nameof(clientIp));

            var employee = await _employeeRepository.GetEmployeeByEmailNumberAsync(email);
            if (employee == null)
                return null;
            if (!employee.AccountStatus)
                throw new UnauthorizedAccessException("Account is inactive");
            if (!_passwordHasher.VerifyPassword(password, employee.HashPassword))
                throw new UnauthorizedAccessException("Invalid password");

            var (accessToken, refreshToken) = await _jwtTokenService.GenerateTokensAsync(
                employee.EmployeeID.ToString(),
                employee.FullName,
                employee.EmployeeRole.RoleName,
                clientIp
            );

            return new EmployeeLoginResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 900 
            };
        }
    }
}