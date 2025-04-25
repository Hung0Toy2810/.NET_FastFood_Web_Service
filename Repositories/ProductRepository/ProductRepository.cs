namespace LapTrinhWindows.Repositories.ProductRepository
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(int id);
        // get product by name
        Task<Product?> GetProductByNameAsync(string name);
        Task<bool> AddProductAsync(Product product);
        Task<bool> UpdateProductAsync(Product product);
        Task<bool> DeleteProductAsync(int id);
    }

    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _context.Products.ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));
            return await _context.Products.FindAsync(id);
        }

        public async Task<bool> AddProductAsync(Product product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            await _context.Products.AddAsync(product);
            var result = await _context.SaveChangesAsync() > 0;
            return result;       
        }
        

        public async Task<bool> UpdateProductAsync(Product product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.Products.Update(product);
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

        public async Task<bool> DeleteProductAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID must be greater than 0.", nameof(id));
            //begin transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var product = await _context.Products.FindAsync(id);
                    if (product == null) throw new InvalidOperationException($"Product with ID '{id}' not found.");

                    _context.Products.Remove(product);
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
        public async Task<Product?> GetProductByNameAsync(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name cannot be null or empty.", nameof(name));
            return await _context.Products.FirstOrDefaultAsync(p => p.ProductName == name);
        }
    }
}