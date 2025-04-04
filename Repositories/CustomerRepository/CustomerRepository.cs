using LapTrinhWindows.Models;

namespace LapTrinhWindows.Repositories.CustomerRepository
{
    public interface ICustomerRepository
    {
        Task CreateCustomerAsync(Customer customer);
        //get user by phonenumber
        Task<Customer?> GetCustomerByPhoneNumberAsync(string phoneNumber);
    }

    public class CustomerRepository : ICustomerRepository
    {
        private readonly ApplicationDbContext _context;

        public CustomerRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task CreateCustomerAsync(Customer customer)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));

            await _context.Customers.AddAsync(customer);
            await _context.SaveChangesAsync();
        }
        public async Task<Customer?> GetCustomerByPhoneNumberAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentException("Phone number cannot be null or empty", nameof(phoneNumber));

            return await _context.Customers
                .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);
        }
    }
}
