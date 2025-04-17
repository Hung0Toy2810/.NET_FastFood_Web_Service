namespace LapTrinhWindows.Repositories.CategoryRepository
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllCategoriesAsync();
        Task<Category?> GetCategoryByIdAsync(int id);
        Task<Category> CreateCategoryAsync(Category category);
        Task<Category> UpdateCategoryAsync(Category category);
        Task<bool> DeleteCategoryAsync(int id);
    }

    public class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationDbContext _context;

        public CategoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories.ToListAsync();
        }

        public async Task<Category?> GetCategoryByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));
            return await _context.Categories.FindAsync(id);
        }

        public async Task<Category> CreateCategoryAsync(Category category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));

            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _context.Categories.AddAsync(category);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return category;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task<Category> UpdateCategoryAsync(Category category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));

            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.Categories.Update(category);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return category;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));
            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var category = await _context.Categories.FindAsync(id);
                    if (category == null) throw new InvalidOperationException($"Category with ID '{id}' not found.");

                    _context.Categories.Remove(category);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
}