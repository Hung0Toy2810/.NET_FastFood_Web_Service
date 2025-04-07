using LapTrinhWindows.Models;
using LapTrinhWindows.Repositories.CustomerRepository;
using System.Security.Claims;

namespace LapTrinhWindows.Services
{
    public interface ICustomerLoginService
    {
        Task<string?> LoginAsync(string phoneNumber, string password);
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

        public async Task<string?> LoginAsync(string phoneNumber, string password)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("Phone number cannot be empty", nameof(phoneNumber));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));
            var customer = await _customerRepository.GetCustomerByPhoneNumberAsync(phoneNumber);
            if (customer == null)
                return null; 
            if(customer.Status == false)
                throw new UnauthorizedAccessException("Account is deleted");
            if (!_passwordHasher.VerifyPassword(password, customer.HashPassword))
                throw new UnauthorizedAccessException("Invalid password"); 

            var token = await _jwtTokenService.GenerateTokenAsync(
                customer.CustomerID.ToString(),
                customer.CustomerName,
                "Customer" 
            );

            return token; 
        }
    }
}