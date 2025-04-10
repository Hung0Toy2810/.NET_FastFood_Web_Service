using LapTrinhWindows.Models;
using LapTrinhWindows.Repositories.CustomerRepository;
using System.Security.Claims;

namespace LapTrinhWindows.Services
{
    // Cập nhật interface để trả về đối tượng chứa cả hai token
    public interface ICustomerLoginService
    {
        Task<LoginResult?> LoginAsync(string phoneNumber, string password, string clientIp); // Xóa '?'
    }

    public class LoginResult
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public int ExpiresIn { get; set; } 
    }

    public class CustomerLoginService : ICustomerLoginService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;

        public CustomerLoginService(
            ICustomerRepository customerRepository,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        }

        public async Task<LoginResult?> LoginAsync(string phoneNumber, string password, string clientIp)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("Phone number cannot be empty", nameof(phoneNumber));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));
            if (string.IsNullOrWhiteSpace(clientIp))
                throw new ArgumentException("Client IP cannot be empty", nameof(clientIp));

            var customer = await _customerRepository.GetCustomerByPhoneNumberAsync(phoneNumber);
            if (customer == null)
                return null;
            if (customer.Status == false)
                throw new UnauthorizedAccessException("Account is deleted");
            if (!_passwordHasher.VerifyPassword(password, customer.HashPassword))
                throw new UnauthorizedAccessException("Invalid password");

            var (accessToken, refreshToken) = await _jwtTokenService.GenerateTokensAsync(
                customer.CustomerID.ToString(),
                customer.CustomerName,
                "Customer",
                clientIp
            );

            return new LoginResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 900
            };
        }
    }
}