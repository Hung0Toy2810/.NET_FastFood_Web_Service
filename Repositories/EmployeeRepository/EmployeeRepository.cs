namespace LapTrinhWindows.Repositories.EmployeeRepository
{
    // interface IEmployeeRepository
    public interface IEmployeeRepository
    {
        Task<List<Employee>> GetAllEmployeesAsync(int offset, int limit);
        Task<Employee> GetEmployeeByIdAsync(Guid id);
        Task<bool> AddEmployeeAsync(Employee employee);
        Task<bool> UpdateEmployeeAsync(Employee employee);
        Task<bool> DeleteEmployeeAsync(Employee employee);
        Task<List<Guid>> SearchEmployeesAsync(string searchTerm);
        Task<List<Employee>> GetEmployeesByRoleAsync(string roleName, int offset, int limit);
        Task<List<Employee>> GetEmployeesByStatusAsync(string status);
        Task ChangeEmployeeStatusAsync(Guid employeeId, bool status);
        Task UnactiveEmployeeAsync(Guid employeeId);
        Task ActiveEmployeeAsync(Guid employeeId);
        //GetEmployeeByPhoneNumberAsync
        Task<Employee?> GetEmployeeByPhoneNumberAsync(string phoneNumber);
        //get emloyee rolename by id
        Task<EmployeeRole> GetEmployeeRoleByIdAsync(int id);
        // change password
        Task UpdateEmployeePasswordAsync(Guid employeeId, string newHashPassword);
        // change avtkey
        Task UpdateEmployeeAvtKeyAsync(Guid employeeId, string newAvtKey);
        Task<Employee?> GetEmployeeByEmailNumberAsync(string email);

    }
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly ApplicationDbContext _context;

        public EmployeeRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Employee>> GetAllEmployeesAsync(int offset, int limit)
        {
            if (offset < 0) throw new ArgumentException("Offset must be greater than or equal to 0.", nameof(offset));
            if (limit <= 0) throw new ArgumentException("Limit must be greater than 0.", nameof(limit));

            return await _context.Employees
                .Include(e => e.EmployeeRole) 
                .Skip(offset) 
                .Take(limit) 
                .ToListAsync();
        }

        public async Task<Employee> GetEmployeeByIdAsync(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentException("ID cannot be empty.", nameof(id));
            return await _context.Employees.Include(e => e.EmployeeRole).FirstOrDefaultAsync(e => e.EmployeeID == id) 
                   ?? throw new InvalidOperationException("Employee not found.");
        }

        public async Task<bool> AddEmployeeAsync(Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));

            await _context.Employees.AddAsync(employee);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateEmployeeAsync(Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));

            _context.Employees.Update(employee);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteEmployeeAsync(Employee employee)
        {
            if (employee == null) throw new ArgumentNullException(nameof(employee));

            _context.Employees.Remove(employee);
            return await _context.SaveChangesAsync() > 0;
        }
        

        public async Task<List<Guid>> SearchEmployeesAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                throw new ArgumentException("Search term cannot be empty.", nameof(searchTerm));

            return await _context.Employees
                .Where(e => e.FullName.Contains(searchTerm) || 
                        e.PhoneNumber.Contains(searchTerm) || 
                        e.Email.Contains(searchTerm))
                .Select(e => e.EmployeeID)
                .ToListAsync();
        }

        public async Task<List<Employee>> GetEmployeesByRoleAsync(string roleName, int offset, int limit)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name cannot be empty.", nameof(roleName));
            if (offset < 0)
                throw new ArgumentException("Offset must be greater than or equal to 0.", nameof(offset));
            if (limit <= 0)
                throw new ArgumentException("Limit must be greater than 0.", nameof(limit));

            return await _context.Employees
                .Where(e => e.EmployeeRole.RoleName == roleName)
                .Include(e => e.EmployeeRole) 
                .Skip(offset) 
                .Take(limit) 
                .ToListAsync();
        }

        public async Task<List<Employee>> GetEmployeesByStatusAsync(string status)
        {
            
            if (string.IsNullOrEmpty(status)) throw new ArgumentNullException(nameof(status));
            if (!Enum.TryParse(status, true, out EmployeeStatus employeeStatus))
                throw new ArgumentException("Invalid status value.", nameof(status));
            return await _context.Employees
                .Where(e => e.Status == employeeStatus)
                .Include(e => e.EmployeeRole)
                .ToListAsync();
        }
        public async Task ChangeEmployeeStatusAsync(Guid employeeId, bool status)
        {
            // use transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = await GetEmployeeByIdAsync(employeeId);
                if (employee == null) throw new InvalidOperationException("Employee not found.");

                employee.Status = status ? EmployeeStatus.Online : EmployeeStatus.Offline;
                _context.Employees.Update(employee);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task UnactiveEmployeeAsync(Guid employeeId)
        {
            // use transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = await GetEmployeeByIdAsync(employeeId);
                if (employee == null) throw new InvalidOperationException("Employee not found.");

                employee.AccountStatus = false;
                _context.Employees.Update(employee);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task ActiveEmployeeAsync(Guid employeeId)
        {
            // use transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = await GetEmployeeByIdAsync(employeeId);
                if (employee == null) throw new InvalidOperationException("Employee not found.");

                employee.AccountStatus = true;
                _context.Employees.Update(employee);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<Employee?> GetEmployeeByPhoneNumberAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("Phone number cannot be empty.", nameof(phoneNumber));

            return await _context.Employees
                .FirstOrDefaultAsync(e => e.PhoneNumber == phoneNumber);
        }
        public async Task<EmployeeRole> GetEmployeeRoleByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));
            return await _context.EmployeeRoles
                .FirstOrDefaultAsync(e => e.RoleID == id) 
                   ?? throw new InvalidOperationException("Employee role not found.");
        }
        public async Task UpdateEmployeePasswordAsync(Guid employeeId, string newHashPassword)
        {
            //use transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = await GetEmployeeByIdAsync(employeeId);
                if (employee == null) throw new InvalidOperationException("Employee not found.");

                employee.HashPassword = newHashPassword;
                _context.Employees.Update(employee);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task UpdateEmployeeAvtKeyAsync(Guid employeeId, string newAvtKey)
        {
            //use transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = await GetEmployeeByIdAsync(employeeId);
                if (employee == null) throw new InvalidOperationException("Employee not found.");

                employee.AvtKey = newAvtKey;
                _context.Employees.Update(employee);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<Employee?> GetEmployeeByEmailNumberAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty.", nameof(email));

            return await _context.Employees
                .Include(e => e.EmployeeRole)
                .FirstOrDefaultAsync(e => e.Email == email);
        }
    }
}