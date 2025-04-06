using LapTrinhWindows.Models;

namespace LapTrinhWindows.Repositories.CustomerRepository
{
    public interface ICustomerRepository
    {
        Task CreateCustomerAsync(Customer customer);
        //get user by phonenumber
        Task<Customer?> GetCustomerByPhoneNumberAsync(string phoneNumber);
        //Update customer password
        Task UpdateCustomerPasswordAsync(Guid Id, string newHashPassword);
        // Update customer information
        Task UpdateCustomerInformationAsync(Customer customer);
        //delete customer
        Task DeleteCustomerAsync(Guid customerId);
        // change customer status
        Task ChangeCustomerStatusAsync(Guid customerId, bool status);
        // get customer by id
        Task<Customer?> GetCustomerByIdAsync(Guid customerId);
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
        public async Task UpdateCustomerPasswordAsync(Guid id, string newHashPassword)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) 
                throw new InvalidOperationException($"Customer with ID '{id}' not found.");
            
            customer.HashPassword = newHashPassword;
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateCustomerInformationAsync(Customer customer)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));

            var existingCustomer = await _context.Customers.FindAsync(customer.CustomerID);
            if (existingCustomer == null) throw new InvalidOperationException($"Customer with ID '{customer.CustomerID}' not found.");

            existingCustomer.CustomerName = customer.CustomerName;
            existingCustomer.Address = customer.Address;
            existingCustomer.PhoneNumber = customer.PhoneNumber;
            

            _context.Customers.Update(existingCustomer);
            await _context.SaveChangesAsync();
        }
        public async Task DeleteCustomerAsync(Guid customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) throw new InvalidOperationException($"Customer with ID '{customerId}' not found.");

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
        }
        public async Task ChangeCustomerStatusAsync(Guid customerId, bool status)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) throw new InvalidOperationException($"Customer with ID '{customerId}' not found.");

            customer.Status = status;
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
        }
        public async Task<Customer?> GetCustomerByIdAsync(Guid customerId)
        {
            if (customerId == Guid.Empty) throw new ArgumentException("Customer ID cannot be empty", nameof(customerId));
            return await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);
        }
    }
}
