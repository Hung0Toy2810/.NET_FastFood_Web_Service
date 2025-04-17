using LapTrinhWindows.Services.DbContextFactory;
namespace LapTrinhWindows.Repositories.RoleRepository
{
    public interface IRoleRepository
    {
        // add role
        Task<bool> AddRoleAsync(EmployeeRole role);
        // get all roles
        Task<IEnumerable<EmployeeRole>> GetAllRolesAsync();
        // get role by id
        Task<EmployeeRole?> GetRoleByIdAsync(int id);
        // update role
        Task<bool> UpdateRoleAsync(EmployeeRole role);
        // delete role
        Task<bool> DeleteRoleAsync(int id);
        // get role by name
        Task<EmployeeRole?> GetRoleByRoleNameAsync(string roleName);
    }
    // db context factory
    
    public class RoleRepository : IRoleRepository
    {
        private readonly ApplicationDbContext _context;

        public RoleRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        public async Task<bool> AddRoleAsync(EmployeeRole role)
        {
            // use transaction
            if (role == null) throw new ArgumentNullException(nameof(role));
            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _context.EmployeeRoles.AddAsync(role);
                    var result = await _context.SaveChangesAsync() > 0;
                    await transaction.CommitAsync();
                    return result;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
        public async Task<IEnumerable<EmployeeRole>> GetAllRolesAsync()
        {
            return await _context.EmployeeRoles.ToListAsync();
        }
        public async Task<EmployeeRole?> GetRoleByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));

            return await _context.EmployeeRoles.FindAsync(id);
        }
        public async Task<bool> UpdateRoleAsync(EmployeeRole role)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));

            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.EmployeeRoles.Update(role);
                    var result = await _context.SaveChangesAsync() > 0;
                    await transaction.CommitAsync();
                    return result;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
        public async Task<bool> DeleteRoleAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));

            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var role = await GetRoleByIdAsync(id);
                    if (role == null) throw new InvalidOperationException("Role not found.");

                    _context.EmployeeRoles.Remove(role);
                    var result = await _context.SaveChangesAsync() > 0;
                    await transaction.CommitAsync();
                    return result;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
        public async Task<EmployeeRole?> GetRoleByRoleNameAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName)) throw new ArgumentException("Role name cannot be null or empty", nameof(roleName));

            return await _context.EmployeeRoles
                .FirstOrDefaultAsync(r => r.RoleName == roleName);
        }
    }
}